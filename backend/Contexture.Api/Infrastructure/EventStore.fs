namespace Contexture.Api.Infrastructure.Storage

open System
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Subscriptions

type EventDefinition =
    { Source: EventSource
      Event: obj
      EventType: System.Type
      StreamKind: StreamKind }
    
module EventDefinition =
    let from source (item: 'E) =
        { Source = source
          Event = item
          EventType = typeof<'E>
          StreamKind = StreamKind.Of<'E>()
        }

type EventStorage =
    abstract member Stream: Version -> StreamIdentifier -> Async<StreamResult>
    abstract member AllStreamsOf: StreamKind -> Async<EventResult>

    abstract member Append:
        StreamIdentifier -> ExpectedVersion -> EventDefinition list -> Async<Result<Version * Position, AppendError>>

    abstract member All: unit -> Async<EventResult>
    abstract member Subscribe: string -> SubscriptionDefinition -> SubscriptionHandler -> Async<Subscription>

namespace Contexture.Api.Infrastructure

open System
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Infrastructure
open FsToolkit.ErrorHandling
open Contexture.Api.Infrastructure.Subscriptions

type EventStore(storage: Storage.EventStorage) =

    let asTyped items : EventEnvelope<'E> list = items |> List.map EventEnvelope.unbox

    let subscribeStreamKind name position (subscription: SubscriptionHandler<'E>) =
        let upcastSubscription position events = events |> asTyped |> subscription position
        storage.Subscribe name (FromKind(StreamKind.Of<'E>(), position)) upcastSubscription

    let subscribeAll (convertToAll : EventEnvelope -> EventEnvelope<'E> option) name position (subscription: SubscriptionHandler<'E>) =
        let upcastSubscription position events =
            let convertedEvents =
                events
                |> List.choose convertToAll
            subscription position convertedEvents
        storage.Subscribe name (FromAll position) upcastSubscription

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
                let eventDefinitions =
                    definitions
                    |> List.map (fun payload ->
                        { Source = identifier |> StreamIdentifier.source
                          Event = box payload
                          EventType = typeof<'E>
                          StreamKind = StreamKind.Of<'E>() }
                    )

                storage.Append identifier version eventDefinitions }

    let all toAllStream : Async<Position * EventEnvelope<'E> list> =
        async {
            match! storage.All() with
            | Ok allStreams -> return allStreams |> Tuple.mapSnd (List.choose toAllStream)
            | Error e ->
                failwithf "Could not get all streams: %s" e
                return Position.start, List.empty
        }

    static member With(storage: Storage.EventStorage)=
        EventStore(storage)

    member _.Stream name version =
        let stream = stream name
        stream.Read version

    member _.Append identifier version items =
        let stream = stream identifier
        stream.Append version items

    member _.AllStreams() = allStreams ()

    member _.Subscribe name position (subscription: SubscriptionHandler<'E>) =
        subscribeStreamKind name position subscription

    member _.SubscribeAll convert name position (subscription: SubscriptionHandler<'E>) =
        subscribeAll convert name position subscription

    member _.All toAllStream = all toAllStream
