namespace Contexture.Api.Infrastructure

open System.Collections.Concurrent
open System.Collections.Generic


type EventSource = System.Guid

type EventMetadata =
    { Source: EventSource
      RecordedAt: System.DateTime }

type EventEnvelope<'Event> =
    { Metadata: EventMetadata
      Event: 'Event }

type Subscription<'E> = EventEnvelope<'E> list -> unit

type EventStore(items: Dictionary<EventSource, EventEnvelope<obj> list>,
                subscriptions: ConcurrentDictionary<System.Type, Subscription<obj> list>) =

    let boxEnvelope (envelope: EventEnvelope<'E>) =
        { Metadata = envelope.Metadata
          Event = box envelope.Event }

    let unboxEnvelope (envelope: EventEnvelope<obj>): EventEnvelope<'E> =
        { Metadata = envelope.Metadata
          Event = unbox<'E> envelope.Event }

    let stream source =
        let (success, events) = items.TryGetValue source
        if success then events |> List.map unboxEnvelope else []

    let subscriptionsOf key =
        let (success, items) = subscriptions.TryGetValue key
        if success then items else []

    let append (newItems: EventEnvelope<'E> list) =
        newItems
        |> List.iter (fun envelope ->
            let source = envelope.Metadata.Source

            let fullStream =
                source
                |> stream
                |> fun s -> s @ [ envelope ]
                |> List.map boxEnvelope

            items.[source] <- fullStream)

        subscriptionsOf typedefof<'E>
        |> List.iter (fun subscription ->
            let upcastSubscription events =
                events |> List.map boxEnvelope |> subscription

            upcastSubscription newItems)

    let subscribe (subscription: Subscription<'E>) =
        let key = typedefof<'E>

        let upcastSubscription events =
            events |> List.map unboxEnvelope |> subscription

        subscriptions.AddOrUpdate
            (key, (fun _ -> [ upcastSubscription ]), (fun _ subscriptions -> subscriptions @ [ upcastSubscription ]))
        |> ignore

    static member Empty = EventStore(Dictionary(), ConcurrentDictionary())
    
    member __.Stream name: EventEnvelope<'E> list = stream name
    member __.Append items = lock __ (fun () -> append items)
    member __.Subscribe(subscription: Subscription<'E>) = subscribe subscription