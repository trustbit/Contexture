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

    let byEventType =
        items.Values
        |> Seq.choose (fun v ->
            v
            |> List.tryHead
            |> Option.map (fun first -> first.Event.GetType(), v))
        |> dict
        |> Dictionary

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

    let getAll key: EventEnvelope<'E> list =
        let (success, items) = byEventType.TryGetValue key
        if success then items |> List.map unboxEnvelope else []

    let append (newItems: EventEnvelope<'E> list) =
        newItems
        |> List.iter (fun envelope ->
            let source = envelope.Metadata.Source

            let fullStream =
                source
                |> stream
                |> fun s -> s @ [ envelope ]
                |> List.map boxEnvelope

            items.[source] <- fullStream
            let eventType = typedefof<'E>
            let allEvents = getAll eventType
            byEventType.[eventType] <- allEvents @ [ boxEnvelope envelope ])

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

    let get (): EventEnvelope<'E> list = getAll typedefof<'E>


    static member Empty =
        EventStore(Dictionary(), ConcurrentDictionary())

    member __.Stream name: EventEnvelope<'E> list = stream name
    member __.Append items = lock __ (fun () -> append items)
    member __.Subscribe(subscription: Subscription<'E>) = subscribe subscription
    member __.Get() = get ()

module Projections =
    type Projection<'State, 'Event> =
        { Init: 'State
          Update: 'State -> 'Event -> 'State }

    let projectIntoMap projection =
        fun state eventEnvelope ->
            state
            |> Map.tryFind eventEnvelope.Metadata.Source
            |> Option.defaultValue projection.Init
            |> fun projectionState ->
                eventEnvelope.Event
                |> projection.Update projectionState
            |> fun newState ->
                state
                |> Map.add eventEnvelope.Metadata.Source newState

    let project projection events =
        events
        |> List.map (fun e -> e.Event)
        |> List.fold projection.Update projection.Init
