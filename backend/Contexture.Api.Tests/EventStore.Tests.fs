module Contexture.Api.Tests.EventStore

open System
open System.Collections.Concurrent
open System.Diagnostics.Tracing
open System.Threading
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Infrastructure.Storage.NStoreBased
open DotNet.Testcontainers.Builders
open DotNet.Testcontainers.Configurations
open DotNet.Testcontainers.Containers
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Internal
open Microsoft.FSharp.Control
open NStore.Core.Logging
open NStore.Core.Streams
open NStore.Persistence.MsSql
open Xunit

module Fixture =
    let environment = EnvironmentSimulation.FixedTimeEnvironment.FromSystemClock()
    type TestStream = | TestEvent of int
    let streamKind = StreamKind.Of<TestStream>()

    let createEvent source =
        let event = TestEvent (environment.NextId())

        let metadata =
            { Source = source
              RecordedAt = environment.Time() }

        { Metadata = metadata; Event = event }

    let generateEvent () =
        let eventSource = environment |> EnvironmentSimulation.PseudoRandom.guid
        createEvent eventSource

let private oneTheoryTestCase (items: obj seq) = items |> Seq.toArray

let private expectOk (result: Async<Result<'r,_>>) : Async<unit> =
    async {
        match! result with
        | Ok _ -> return ()
        | Error e -> return failwithf "Expected an Ok result but got Error:\n%O" e
    }
let private resultOrFail (result: Async<Result<'r,_>>) : Async<'r> =
    async {
        match! result with
        | Ok r -> return r
        | Error e -> return failwithf "Expected an Ok result but got Error:\n%O" e
    }

let private waitForResult (timeout: int) (receivedEvents: TaskCompletionSource<_>) =
    let cancelTask () : Task =
        task {
            do! Task.Delay(timeout)

            if not receivedEvents.Task.IsCompleted then
                receivedEvents.SetCanceled()

            return ()
        }

    Task.Run(cancelTask)

let waitForEventsOnSubscription start (eventStore: EventStore) action eventCallback =
    task {
        let receivedEvents =
            TaskCompletionSource<EventEnvelope<Fixture.TestStream> list>(
                TaskCreationOptions.RunContinuationsAsynchronously
            )

        let subscription events =
            receivedEvents.SetResult events
            Async.Sleep(0)

        do! eventStore.Subscribe start (subscription)

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

[<AbstractClass>]
type EventStoreBehavior() =

    abstract member anEmptyEventStore: unit -> Task<EventStore>

    abstract member anEventStoreWithStreamsAndEvents: int -> Task<EventStore * EventSource list>

    [<Fact>]
    member this.canReadFromAnEmptyStore() =
        task {
            let! eventStore = this.anEmptyEventStore ()
            let! version,result = eventStore.AllStreams()
            Assert.Empty result
            Assert.Equal(Version.start, version)
        }

    [<Fact>]
    member this.canReadFromAnStoreWithOneStreamAndOneEvent() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents (1)
            let! version,result = eventStore.AllStreams()

            let expectedVersion = Version.from 1
            Then.assertAll result sources
            Assert.Equal (expectedVersion, version)

            let source = sources.Head
            let! streamVersion,stream =
                eventStore.Stream source  Version.start
                |> resultOrFail
                
            Then.assertSingle stream source
            Assert.Equal (expectedVersion, streamVersion)

            let! allVersion,allStreams = eventStore.All(List.map EventEnvelope.unbox)
            Then.assertAll allStreams [ source ]
            Assert.NotEmpty allStreams
            Assert.Equal (allVersion, streamVersion)
        }

    [<Fact>]
    member this.canReadFromAnStoreWithMultipleStreamsAndMultipleEvents() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents (3)
            let! (version,result: EventEnvelope<Fixture.TestStream> list) = eventStore.AllStreams()
            let expectedVersion = Version.from 3
            Then.assertAll result sources
            Assert.Equal(expectedVersion, version)

            for source in sources do
                let! _,stream =
                    eventStore.Stream source Version.start
                    |> resultOrFail
                Then.assertSingle stream source

            let! (version,allStreams: EventEnvelope<Fixture.TestStream> list) = eventStore.All(List.map EventEnvelope.unbox)
            Then.assertAll allStreams sources
            Assert.Equal(expectedVersion, version)
        }

    [<Fact>]
    member this.canWriteIntoEmptyEventStoreAndReread() =
        task {
            let! eventStore = this.anEmptyEventStore ()
            let event = Fixture.generateEvent ()
            do! eventStore.Append event.Metadata.Source Empty [ event.Event ]
                |> expectOk

            let expectedVersion = Version.from 1
            let! version,result = eventStore.AllStreams()
            Then.assertAll result [ event.Metadata.Source ]
            Assert.Equal (expectedVersion, version)

            let! (version, stream: EventEnvelope<Fixture.TestStream> list) =
                eventStore.Stream event.Metadata.Source Version.start
                |> resultOrFail

            Then.assertSingle stream event.Metadata.Source
            Assert.Equal (expectedVersion, version)
        }

    [<Fact>]
    member this.canWriteIntoEmptyEventStoreAndReceiveEventViaSubscription() =
        task {
            let! eventStore = this.anEmptyEventStore ()
            let event = Fixture.generateEvent ()

            do!
                waitForEventsOnSubscription
                    Position.start
                    eventStore
                    (fun () -> eventStore.Append event.Metadata.Source Empty [ event.Event ] |> expectOk)
                    (fun events -> Then.assertSingle events event.Metadata.Source)
        }

    [<Fact>]
    member this.canAppendToAnExistingStreamAndReceiveOnlyEventViaSubscriptionFromLatest() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents 1
            let event = Fixture.createEvent sources.Head

            do!
                waitForEventsOnSubscription
                    (Position.from 1)
                    eventStore
                    (fun () -> eventStore.Append event.Metadata.Source (AtVersion (Version.from 0)) [ event.Event ] |> expectOk)
                    (fun events ->
                        Then.assertSingle events event.Metadata.Source
                        let single = events.Head
                        Assert.Equal(event.Event, single.Event)
                        )
        }
        
    [<Fact>]
    member this.canSubscribeFromStartOfExistingStream() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents 1
            let event = Fixture.createEvent sources.Head

            do!
                waitForEventsOnSubscription
                    Position.start
                    eventStore
                    (fun () -> async { return () })
                    (fun events -> Then.assertSingle events event.Metadata.Source)
        }

type InMemoryEventStore() =
    inherit EventStoreBehavior()

    override this.anEmptyEventStore() =
        InMemoryStorage.empty()
        |> EventStore.With
        |> Task.FromResult 

    override this.anEventStoreWithStreamsAndEvents(count) =
        let data = Seq.init count (fun _ -> Fixture.generateEvent ()) |> Seq.toList

        Task.FromResult(
            data |> List.map EventEnvelope.box |>  InMemoryStorage.initialize |> EventStore.With,
            data |> List.map (fun e -> e.Metadata.Source)
        )

#nowarn "44" // ContainerBuilder<MsSqlTestcontainer>() is deprecated but does not provide a clear guidance yet
type MsSqlFixture() =
    let container =
        let containerConfiguration =
            ContainerBuilder<MsSqlTestcontainer>()
                .WithDatabase(new MsSqlTestcontainerConfiguration(Password = "localdevpassword#123"))
                .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
                .WithName("MS-SQL-Integration-Tests")
                .WithCleanUp(false)
                .WithAutoRemove(true)

        let instance = containerConfiguration.Build()
        instance

    member _.Container = container

    interface IAsyncLifetime with
        member this.DisposeAsync() = container.StopAsync()
        member this.InitializeAsync() = container.StartAsync()

    interface IAsyncDisposable with
        member this.DisposeAsync() = container.DisposeAsync()

type MsSqlBackedEventStore(msSql: MsSqlFixture) =
    inherit EventStoreBehavior()

    let counter = ref 0L

    member private this.initializePersistence() =
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
            let! persistence = this.initializePersistence ()
            let storage = Storage.NStoreBased.Storage(persistence)
            return EventStore(storage, (fun () -> DateTime.Now))
        }

    override this.anEventStoreWithStreamsAndEvents(count) =
        task {
            let data = Seq.init count (fun _ -> Fixture.generateEvent ()) |> Seq.toList

            let! persistence = this.initializePersistence ()
            let streamsFactory = StreamsFactory(persistence)

            for (identifier, events) in
                data
                |> List.map EventEnvelope.box
                |> List.groupBy (fun x -> StreamIdentifier.from x.Metadata.Source x.StreamKind) do
                let stream = streamsFactory.Open(StreamIdentifier.name identifier)
                let! _ = stream.AppendAsync(events)
                ()

            let storage = Storage.NStoreBased.Storage(persistence)
            return EventStore(storage,  (fun () -> DateTime.Now)), data |> List.map (fun e -> e.Metadata.Source)
        }

    interface IClassFixture<MsSqlFixture> with

