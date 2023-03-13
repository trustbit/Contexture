module Contexture.Api.Tests.EventStore

open System
open System.Threading
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Subscriptions
open Contexture.Api.Infrastructure.Storage
open FsToolkit.ErrorHandling
open Microsoft.FSharp.Control
open NStore.Core.Logging
open NStore.Core.Streams
open NStore.Persistence.MsSql
open Xunit
open Assertions

module Fixture =
    let environment = EnvironmentSimulation.FixedTimeEnvironment.FromSystemClock()
    type TestStream = TestEvent of int
    let streamKind = StreamKind.Of<TestStream>()

    let createEvent source =
        let event = TestEvent(environment.NextId())

        source,event

    let generateEvent () =
        let eventSource = environment |> EnvironmentSimulation.PseudoRandom.guid
        createEvent eventSource


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

        let subscriptionHandler _ events =
            if not (List.isEmpty events) then
                receivedEvents.SetResult events

            Async.Sleep(0)

        let! subscription = eventStore.Subscribe "UnitTestSubscription" start subscriptionHandler
        do! Runtime.waitUntilCaughtUp [ subscription ]

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
            let! version, result = eventStore.AllStreams()
            Assert.Empty result
            Assert.Equal(Position.start, version)
        }

    [<Fact>]
    member this.canReadFromAnStoreWithOneStreamAndOneEvent() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents (1)
            let! position, result = eventStore.AllStreams()

            let expectedVersion = Version.from 1
            let expectedPosition = Position.from 1
            Then.assertAll result sources
            Assert.Equal(expectedPosition, position)

            let source = sources.Head
            let! streamVersion, stream = eventStore.Stream source Version.start |> Then.resultOrFail

            Then.assertSingle stream source
            Assert.Equal(expectedVersion, streamVersion)

            let! allPosition, allStreams = eventStore.All EventEnvelope.tryUnbox
            Then.assertAll allStreams [ source ]
            Assert.NotEmpty allStreams
            Assert.Equal(allPosition |> Position.value, streamVersion |> Version.value)
        }

    [<Fact>]
    member this.canReadFromAnStoreWithMultipleStreamsAndMultipleEvents() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents 3
            let! (version, result: EventEnvelope<Fixture.TestStream> list) = eventStore.AllStreams()
            let expectedPosition = Position.from 3
            Then.assertAll result sources
            Assert.Equal(expectedPosition, version)

            for source in sources do
                let! _, stream = eventStore.Stream source Version.start |> Then.resultOrFail
                Then.assertSingle stream source

            let! (version, allStreams: EventEnvelope<Fixture.TestStream> list) =
                eventStore.All EventEnvelope.tryUnbox

            Then.assertAll allStreams sources
            Assert.Equal(expectedPosition, version)
        }

    [<Fact>]
    member this.Stream_WriteIntoEmptyEventStore_RereadWrittenEvent() =
        task {
            let! eventStore = this.anEmptyEventStore ()
            let source,event = Fixture.generateEvent ()
            do! eventStore.Append source Empty [ event ] |> Then.expectOk

            let expectedPosition = Position.from 1
            let expectedVersion = Version.from 1

            let! position, result = eventStore.AllStreams()
            Then.assertAll result [ source ]
            Assert.Equal(expectedPosition, position)

            let! (version, stream: EventEnvelope<Fixture.TestStream> list) =
                eventStore.Stream source Version.start |> Then.resultOrFail

            Then.assertSingle stream source
            Assert.Equal(expectedVersion, version)
        }

    [<Fact>]
    member this.Subscribe_WriteIntoEmptyEventStore_ReceiveEventViaSubscription() =
        task {
            let! eventStore = this.anEmptyEventStore ()
            let source, event = Fixture.generateEvent ()

            do!
                waitForEventsOnSubscription
                    Start
                    eventStore
                    (fun () -> eventStore.Append source Empty [ event ] |> Then.expectOk)
                    (fun events -> Then.assertSingle events source)
        }

    [<Fact>]
    member this.Subscribe_AppendToAnExistingStream_ReceiveOnlyLatestEventViaSubscription() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents 1
            let source,event = Fixture.createEvent sources.Head

            do!
                waitForEventsOnSubscription
                    (From(Position.from 1))
                    eventStore
                    (fun () ->
                        eventStore.Append source (AtVersion(Version.from 1)) [ event ]
                        |> Then.expectOk)
                    (fun events ->
                        Then.assertSingle events source
                        let single = events.Head
                        Assert.Equal(event, single.Event))
        }

    [<Fact>]
    member this.Subscribe_FromStartOfExistingStream_ReturnsAllEvents() =
        task {
            let! eventStore, sources = this.anEventStoreWithStreamsAndEvents 1
            let source,_ = Fixture.createEvent sources.Head

            do!
                waitForEventsOnSubscription
                    Start
                    eventStore
                    (fun () -> async { return () })
                    (fun events -> Then.assertSingle events source)
        }
        
    [<Fact>]
    member this.Subscribe_FromSpecificPositionOfExistingStream_ReturnsOnlyEventsAfterTheStartPosition() =
        task {
            let! eventStore, _ = this.anEventStoreWithStreamsAndEvents 3
            let startingPosition = Position.from 2
            do!
                waitForEventsOnSubscription
                    (From startingPosition)
                    eventStore
                    (fun () -> async { return () })
                    (fun events ->
                        Then.NotEmpty (List.toSeq events)
                        let minimumPosition =
                            events
                            |> List.minBy(fun e -> e.Metadata.Position)
                            
                        Assert.Equal(Position.nextPosition startingPosition , minimumPosition.Metadata.Position)
                        ignore <| Assert.Single events 
                    )
        }

type InMemoryEventStore() =
    inherit EventStoreBehavior()

    override this.anEmptyEventStore() =
        EventStore.With (InMemory.emptyEventStore Fixture.environment.Time) 
        |> Task.FromResult

    override this.anEventStoreWithStreamsAndEvents(count) =
        let data = Seq.init count (fun _ -> Fixture.generateEvent ()) |> Seq.toList

        let storage =
            data
            |> List.map (fun (source, event) -> EventDefinition.from source event)
            |> InMemory.eventStoreWith Fixture.environment.Time
            
        Task.FromResult(
            EventStore.With storage,
            data |> List.map (fun (source,_) -> source)
        )

type MsSqlBackedEventStore(msSql: MsSqlFixture) =
    inherit EventStoreBehavior()

    let loggerFactory = NStoreNullLoggerFactory.Instance
    let counter = ref 0L

    member private this.initializePersistence() =
        task {
            let config =
                MsSqlPersistenceOptions(
                    loggerFactory,
                    ConnectionString = msSql.Container.ConnectionString,
                    StreamsTableName = $"streams_{Interlocked.Increment(counter)}_{this.GetType().Name}",
                    Serializer = NStoreBased.JsonMsSqlSerializer.Default
                )

            let persistence = NStore.Persistence.MsSql.MsSqlPersistence(config)
            do! persistence.DestroyAllAsync(CancellationToken.None)
            do! persistence.InitAsync(CancellationToken.None)
            return persistence
        }

    override this.anEmptyEventStore() =
        task {
            let! persistence = this.initializePersistence ()
            let storage = Storage.NStoreBased.Storage(persistence, (fun () -> DateTimeOffset.Now), loggerFactory)
            return EventStore(storage)
        }

    override this.anEventStoreWithStreamsAndEvents(count) =
        task {
            let data = Seq.init count (fun _ -> Fixture.generateEvent ()) |> Seq.toList

            let! persistence = this.initializePersistence ()
            let streamsFactory = StreamsFactory(persistence)

            let grouping =
                data
                |> List.groupBy (fun (source,event) -> StreamIdentifier.from source (StreamKind.Of event))
            for (identifier, events) in grouping do
                let stream = streamsFactory.Open(StreamIdentifier.name identifier)
                let eventDefinitions =
                    events
                    |> List.map (fun (s,e) ->
                        {
                            Event = e
                            Source = s
                            StreamKind = StreamKind.Of e
                            EventType = e.GetType()
                        })
                let payload =
                    {
                        NStoreBased.Events = eventDefinitions
                        NStoreBased.RecordedAt = Fixture.environment.Time()
                    }
                let! _ = stream.AppendAsync(payload)
                ()

            let storage = Storage.NStoreBased.Storage(persistence,(fun () -> DateTimeOffset.Now), loggerFactory)
            return EventStore(storage), data |> List.map (fun (source,_) -> source)
        }

    interface IClassFixture<MsSqlFixture> with

