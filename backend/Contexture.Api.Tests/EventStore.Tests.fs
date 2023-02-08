module Contexture.Api.Tests.EventStore

open System.Diagnostics.Tracing
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open Microsoft.FSharp.Control
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

[<Theory>]
[<MemberData(nameof Given.anEmptyEventStore, MemberType = typeof<Given>)>]
let canReadFromAnEmptyStore (eventStore: EventStore) =
    task {
        let! result = eventStore.AllStreams()
        Assert.Empty result
    }

[<Theory>]
[<MemberData(nameof Given.anEventStoreWithStreamsAndEvents, 1, MemberType = typeof<Given>)>]
let canReadFromAnStoreWithOneStreamAndOneEvent (eventStore: EventStore, sources: EventSource list) =
    task {
        let! result = eventStore.AllStreams()

        Then.assertAll result sources

        let source = sources.Head
        let! stream = eventStore.Stream(source)
        Then.assertSingle stream source

        let! allStreams = eventStore.All(List.map EventEnvelope.unbox)
        Then.assertAll allStreams [ source ]
        Assert.NotEmpty allStreams
    }

[<Theory>]
[<MemberData(nameof Given.anEventStoreWithStreamsAndEvents, 3, MemberType = typeof<Given>)>]
let canReadFromAnStoreWithMultipleStreamsAndMultipleEvents (eventStore: EventStore, sources: EventSource list) =
    task {
        let! (result: EventEnvelope<Fixture.TestStream> list) = eventStore.AllStreams()

        Then.assertAll result sources

        for source in sources do
            let! stream = eventStore.Stream(source)
            Then.assertSingle stream source

        let! (allStreams: EventEnvelope<Fixture.TestStream> list) = eventStore.All(List.map EventEnvelope.unbox)
        Then.assertAll allStreams sources
    }

[<Theory>]
[<MemberData(nameof Given.anEmptyEventStore, MemberType = typeof<Given>)>]
let canWriteIntoEmptyEventStoreAndReread (eventStore: EventStore) =
    task {
        let event = Fixture.generateEvent ()
        do! eventStore.Append [ event ]

        let! result = eventStore.AllStreams()
        Then.assertAll result [ event.Metadata.Source ]

        let! (stream: EventEnvelope<Fixture.TestStream> list) = eventStore.Stream event.Metadata.Source

        Then.assertSingle stream event.Metadata.Source
    }

[<Theory>]
[<MemberData(nameof Given.anEmptyEventStore, MemberType = typeof<Given>)>]
let canWriteIntoEmptyEventStoreAndReceiveEventViaSubscription (eventStore: EventStore) =
    task {
        let event = Fixture.generateEvent ()

        do!
            waitForEventsOnSubscription
                eventStore
                (fun () -> eventStore.Append [ event ])
                (fun events -> Then.assertSingle events event.Metadata.Source)
    }

[<Theory>]
[<MemberData(nameof Given.anEventStoreWithStreamsAndEvents, 1, MemberType = typeof<Given>)>]
let canAppendToAnExistingStreamAndReceiveOnlyEventViaSubscription (eventStore: EventStore, sources: EventSource list) =
    task {
        let event = Fixture.createEvent sources.Head

        do!
            waitForEventsOnSubscription
                eventStore
                (fun () -> eventStore.Append [ event ])
                (fun events -> Then.assertSingle events event.Metadata.Source)

    }
