namespace Contexture.Api.Infrastructure.Storage

open System
open Contexture.Api.Infrastructure

type StreamIdentifier = private StreamIdentifier of StreamKind * EventSource

module StreamKind =
    let asIdentifier source kind = StreamIdentifier(kind, source)

module StreamIdentifier =
    let name (StreamIdentifier(kind, source)) =
        $"{StreamKind.toString kind}/{source.ToString()}"

    let from (eventSource: EventSource) (kind: StreamKind) = StreamIdentifier(kind, eventSource)
    let source (StreamIdentifier(_, source)) = source
    let kind (StreamIdentifier(kind, _)) = kind

type SubscriptionDefinition =
    | FromAll of SubscriptionStartingPosition
    | FromKind of StreamKind * SubscriptionStartingPosition
    | FromStream of StreamIdentifier * Version option

and SubscriptionStartingPosition =
    | Start
    | From of Position
    | End

type Subscription =
    inherit IAsyncDisposable
    abstract Status: SubscriptionStatus

and SubscriptionStatus =
    | NotRunning
    | Processing of current: Position
    | CaughtUp of at: Position
    | Failed of exn * at: Position option
    | Stopped of at: Position

type EventStorage =
    abstract member Stream: Version -> StreamIdentifier -> Async<StreamResult>
    abstract member AllStreamsOf: StreamKind -> Async<EventResult>

    abstract member Append:
        StreamIdentifier -> ExpectedVersion -> EventEnvelope list -> Async<Result<Version, AppendError>>

    abstract member All: unit -> Async<EventResult>
    abstract member Subscribe: SubscriptionDefinition -> SubscriptionHandler -> Async<Subscription>

namespace Contexture.Api.Infrastructure

open System
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Infrastructure
open FsToolkit.ErrorHandling

type EventStore(storage: Storage.EventStorage, clock: Clock) =

    let asTyped items : EventEnvelope<'E> list = items |> List.map EventEnvelope.unbox

    let subscribeStreamKind position (subscription: SubscriptionHandler<'E>) =
        let upcastSubscription events = events |> asTyped |> subscription
        storage.Subscribe (FromKind(StreamKind.Of<'E>(), position)) upcastSubscription

    let subscribeAll position (subscription: SubscriptionHandler<'E>) =
        let upcastSubscription events = events |> asTyped |> subscription
        storage.Subscribe (FromAll position) upcastSubscription

    let allStreams () : Async<Position * EventEnvelope<'E> list> =
        async {
            match! StreamKind.Of<'E>() |> storage.AllStreamsOf with
            | Ok allStreams -> return allStreams |> Tuple.mapSnd asTyped
            | Error e ->
                failwithf "Could not get all streams: %s" e
                return Position.start, List.empty
        }

    let stream name : EventStream<'E> =
        let identifier = StreamIdentifier.from name (StreamKind.Of<'E>())

        { new EventStream<'E> with
            member _.Read version =
                storage.Stream version identifier |> AsyncResult.map (Tuple.mapSnd asTyped)

            member _.Append version definitions =
                let envelopes =
                    definitions
                    |> List.map (fun payload ->
                        { Metadata =
                            { Source = identifier |> StreamIdentifier.source
                              RecordedAt = clock () }
                          Event = payload })
                    |> List.map EventEnvelope.box

                storage.Append identifier version envelopes }


    let all toAllStream : Async<Position * EventEnvelope<'E> list> =
        async {
            match! storage.All() with
            | Ok allStreams -> return allStreams |> Tuple.mapSnd (toAllStream)
            | Error e ->
                failwithf "Could not get all streams: %s" e
                return Position.start, List.empty
        }

    static member With(storage: Storage.EventStorage) clock =
        EventStore(storage, clock)

    member _.Stream name version =
        let stream = stream name
        stream.Read version

    member _.Append identifier version items =
        let stream = stream identifier
        stream.Append version items

    member _.AllStreams() = allStreams ()

    member _.Subscribe position (subscription: SubscriptionHandler<'E>) =
        subscribeStreamKind position subscription

    member _.SubscribeAll position (subscription: SubscriptionHandler<'E>) = subscribeAll position subscription
    member _.All toAllStream = all toAllStream
