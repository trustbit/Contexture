module Contexture.Api.Tests.EventStore

open System
open System.Collections.Concurrent
open System.Diagnostics.Tracing
open System.Threading
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Infrastructure.Storage.NStoreBased
open Contexture.Api.Tests.SqlDockerSupport
open DotNet.Testcontainers.Builders
open DotNet.Testcontainers.Configurations
open DotNet.Testcontainers.Containers
open Microsoft.FSharp.Control
open NStore.Core.Logging
open NStore.Core.Streams
open NStore.Persistence.MsSql
open Xunit

module Fixture =
    let environment = EnvironmentSimulation.FixedTimeEnvironment.FromSystemClock()
    type TestStream = | TestEvent
    let streamKind = StreamKind.Of<TestStream>()

    let createEvent source =
        let event = TestEvent

        let metadata =
            { Source = source
              RecordedAt = environment.Time() }

        { Metadata = metadata; Event = event }

    let generateEvent () =
        let eventSource = environment |> EnvironmentSimulation.PseudoRandom.guid
        createEvent eventSource

let private oneTheoryTestCase (items: obj seq) = items |> Seq.toArray

let private waitForResult (timeout: int) (receivedEvents: TaskCompletionSource<_>) =
    let cancelTask () : Task =
        task {
            do! Task.Delay(timeout)

            if not receivedEvents.Task.IsCompleted then
                receivedEvents.SetCanceled()

            return ()
        }

    Task.Run(cancelTask)

let waitForEventsOnSubscription (eventStore: EventStore) action eventCallback =
    task {
        let receivedEvents =
            TaskCompletionSource<EventEnvelope<Fixture.TestStream> list>(
                TaskCreationOptions.RunContinuationsAsynchronously
            )

        let subscription events =
            receivedEvents.SetResult events
            Async.Sleep(0)

        eventStore.Subscribe(subscription)

        do! Async.StartAsTask(action ())

        let cancelledTask = waitForResult 1000 receivedEvents
        let! events = receivedEvents.Task

        eventCallback events

        do! cancelledTask

        if not receivedEvents.Task.IsCompletedSuccessfully then
            failwith "Not completed successfully"
    }

module Then =
    let assertAll (result: EventEnvelope<Fixture.TestStream> list) (sources: EventSource list) =
        Assert.NotEmpty result
        Assert.Equal(sources.Length, result.Length)
        Assert.All(result, (fun s -> Assert.Equal(Fixture.streamKind, StreamKind.Of(s.Event))))
        Assert.Equal(sources |> List.sort, result |> List.map (fun s -> s.Metadata.Source) |> List.sort |> List.toArray)

    let assertSingle (result: EventEnvelope<Fixture.TestStream> list) (source: EventSource) =
        Assert.NotEmpty result
        let item: EventEnvelope<Fixture.TestStream> = Assert.Single result
        Assert.Equal(Fixture.streamKind, StreamKind.Of(item.Event))
        Assert.Equal(source, item.Metadata.Source)

type Given =
    static member anEmptyEventStore() =
        seq { oneTheoryTestCase <| [ EventStore.Empty ] }

    static member anEventStoreWithStreamsAndEvents(count: int) =
        let data = Seq.init count (fun _ -> Fixture.generateEvent ()) |> Seq.toList

        seq {
            oneTheoryTestCase
            <| seq {
                data |> List.map EventEnvelope.box |> EventStore.With
                data |> List.map (fun e -> e.Metadata.Source)
            }
        }

[<AbstractClass>]
type EventStoreBehavior() =

    abstract member anEmptyEventStore: unit -> Task<EventStore>

    abstract member anEventStoreWithStreamsAndEvents: int -> Task<EventStore * EventSource list>

    [<Fact>]
    // [<MemberData(nameof Given.anEmptyEventStore, MemberType = typeof<Given>)>]
    member this.canReadFromAnEmptyStore() =
        task {
            let! eventStore = this.anEmptyEventStore ()
            let! result = eventStore.AllStreams()
            Assert.Empty result
        }

    [<Fact>]
    // [<MemberData(nameof Given.anEventStoreWithStreamsAndEvents, 1, MemberType = typeof<Given>)>]
    member this.canReadFromAnStoreWithOneStreamAndOneEvent() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents (1)
            let! result = eventStore.AllStreams()

            Then.assertAll result sources

            let source = sources.Head
            let! stream = eventStore.Stream(source)
            Then.assertSingle stream source

            let! allStreams = eventStore.All(List.map EventEnvelope.unbox)
            Then.assertAll allStreams [ source ]
            Assert.NotEmpty allStreams
        }

    [<Fact>]
    [<MemberData(nameof Given.anEventStoreWithStreamsAndEvents, 3, MemberType = typeof<Given>)>]
    member this.canReadFromAnStoreWithMultipleStreamsAndMultipleEvents() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents (3)
            let! (result: EventEnvelope<Fixture.TestStream> list) = eventStore.AllStreams()

            Then.assertAll result sources

            for source in sources do
                let! stream = eventStore.Stream(source)
                Then.assertSingle stream source

            let! (allStreams: EventEnvelope<Fixture.TestStream> list) = eventStore.All(List.map EventEnvelope.unbox)
            Then.assertAll allStreams sources
        }

    [<Fact>]
    // [<MemberData(nameof Given.anEmptyEventStore, MemberType = typeof<Given>)>]
    member this.canWriteIntoEmptyEventStoreAndReread() =
        task {
            let! eventStore = this.anEmptyEventStore ()
            let event = Fixture.generateEvent ()
            do! eventStore.Append [ event ]

            let! result = eventStore.AllStreams()
            Then.assertAll result [ event.Metadata.Source ]

            let! (stream: EventEnvelope<Fixture.TestStream> list) = eventStore.Stream event.Metadata.Source

            Then.assertSingle stream event.Metadata.Source
        }

    [<Fact>]
    // [<MemberData(nameof Given.anEmptyEventStore, MemberType = typeof<Given>)>]
    member this.canWriteIntoEmptyEventStoreAndReceiveEventViaSubscription() =
        task {
            let! eventStore = this.anEmptyEventStore ()
            let event = Fixture.generateEvent ()

            do!
                waitForEventsOnSubscription
                    eventStore
                    (fun () -> eventStore.Append [ event ])
                    (fun events -> Then.assertSingle events event.Metadata.Source)
        }

    [<Fact>]
    // [<MemberData(nameof Given.anEventStoreWithStreamsAndEvents, 1, MemberType = typeof<Given>)>]
    member this.canAppendToAnExistingStreamAndReceiveOnlyEventViaSubscription() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents 1
            let event = Fixture.createEvent sources.Head

            do!
                waitForEventsOnSubscription
                    eventStore
                    (fun () -> eventStore.Append [ event ])
                    (fun events -> Then.assertSingle events event.Metadata.Source)

        }

type InMemoryEventStore() =
    inherit EventStoreBehavior()

    override this.anEmptyEventStore() = Task.FromResult EventStore.Empty

    override this.anEventStoreWithStreamsAndEvents(count) =
        let data = Seq.init count (fun _ -> Fixture.generateEvent ()) |> Seq.toList

        Task.FromResult(
            data |> List.map EventEnvelope.box |> EventStore.With,
            data |> List.map (fun e -> e.Metadata.Source)
        )

type MsSqlFixture() =
    let container =
        let containerConfiguration =
            ContainerBuilder<MsSqlTestcontainer>()
                .WithDatabase(new MsSqlTestcontainerConfiguration(Password = "localdevpassword#123"))
                .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
                .WithName("MS-SQL-Integration-Tests")
                .WithCleanUp(false)
                .WithAutoRemove(true)
        let instance =
            containerConfiguration
                .Build()
        instance
        
    member _.Container = container
        
    interface IAsyncLifetime with
        member this.DisposeAsync() = container.StopAsync()
        member this.InitializeAsync() = container.StartAsync()
        
    interface IAsyncDisposable with
        member this.DisposeAsync() = container.DisposeAsync()    

type MsSqlBackedEventStore(msSql:MsSqlFixture) =
    inherit EventStoreBehavior()
    
    let counter = ref 0L

    member private this.initializePersistence ()=
        task {
            let config =
                MsSqlPersistenceOptions(
                    NStoreNullLoggerFactory.Instance,
                    ConnectionString = msSql.Container.ConnectionString,
                    StreamsTableName = $"streams_{Interlocked.Increment(counter)}_{this.GetType().Name}",
                    Serializer = Storage.NStoreBased.JsonMsSqlSerializer.Default
                )

            let persistence = NStore.Persistence.MsSql.MsSqlPersistence(config)
            do! persistence.DestroyAllAsync(CancellationToken.None)
            do! persistence.InitAsync(CancellationToken.None)
            return persistence
            }
    
    override this.anEmptyEventStore() =
        task {
            let! persistence = this.initializePersistence() 
            let storage = Storage.NStoreBased.Storage(persistence)
            return EventStore(storage, ConcurrentDictionary())
        }

    override this.anEventStoreWithStreamsAndEvents(count) =
        task {
            let data = Seq.init count (fun _ -> Fixture.generateEvent ()) |> Seq.toList

            let! persistence = this.initializePersistence()
            let streamsFactory = StreamsFactory(persistence)
            for (identifier,events) in data |> List.map EventEnvelope.box |> List.groupBy(fun x ->x.Metadata.Source,x.StreamKind) do
                let stream = streamsFactory.Open(StreamIdentifier.name identifier)
                for event in events do
                    let! _ = stream.AppendAsync(event)
                    ()

            let storage = Storage.NStoreBased.Storage(persistence)
            return
                EventStore(storage, ConcurrentDictionary()),
                data |> List.map (fun e -> e.Metadata.Source)
        }

    
    interface IClassFixture<MsSqlFixture> with
