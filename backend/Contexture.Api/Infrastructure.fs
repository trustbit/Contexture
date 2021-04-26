namespace Contexture.Api.Infrastructure

open System
open System.Collections.Concurrent
open System.Collections.Generic

type EventSource = System.Guid

type EventMetadata =
    { Source: EventSource
      RecordedAt: System.DateTime }

type EventEnvelope<'Event> =
    { Metadata: EventMetadata
      Event: 'Event }

type EventEnvelope =
    { Metadata: EventMetadata
      Payload: obj
      EventType: Type }

type Subscription<'E> = EventEnvelope<'E> list -> unit

type private SubscriptionWrapper = EventEnvelope list -> unit

module EventEnvelope =
    let box (envelope: EventEnvelope<'E>) =
        { Metadata = envelope.Metadata
          Payload = box envelope.Event
          EventType = typeof<'E> }

    let unbox (envelope: EventEnvelope) : EventEnvelope<'E> =
        { Metadata = envelope.Metadata
          Event = unbox<'E> envelope.Payload }

type EventStore
    private
    (
        items: Dictionary<EventSource * System.Type, EventEnvelope list>,
        subscriptions: ConcurrentDictionary<System.Type, SubscriptionWrapper list>
    ) =
    let byEventType =
        items.Values
        |> Seq.collect id
        |> Seq.toList
        |> List.groupBy (fun v -> v.EventType)
        |> dict
        |> Dictionary

    let stream source =
        let (success, events) = items.TryGetValue source
        if success then events else []

    let subscriptionsOf key =
        let (success, items) = subscriptions.TryGetValue key
        if success then items else []

    let getAll key : EventEnvelope list =
        let (success, items) = byEventType.TryGetValue key
        if success then items else []

    let asTyped items : EventEnvelope<'E> list = items |> List.map EventEnvelope.unbox

    let asUntyped items = items |> List.map EventEnvelope.box

    let append (newItems: EventEnvelope<'E> list) =
        newItems
        |> List.iter
            (fun envelope ->
                let source = envelope.Metadata.Source
                let eventType = typedefof<'E>
                let key = (source, eventType)

                let fullStream =
                    key
                    |> stream
                    |> asTyped
                    |> fun s -> s @ [ envelope ]
                    |> asUntyped

                items.[key] <- fullStream
                let allEvents = getAll eventType
                byEventType.[eventType] <- allEvents @ [ EventEnvelope.box envelope ])

        subscriptionsOf typedefof<'E>
        |> List.iter
            (fun subscription ->
                let upcastSubscription events = events |> asUntyped |> subscription

                upcastSubscription newItems)

    let subscribe (subscription: Subscription<'E>) =
        let key = typedefof<'E>

        let upcastSubscription events = events |> asTyped |> subscription

        subscriptions.AddOrUpdate(
            key,
            (fun _ -> [ upcastSubscription ]),
            (fun _ subscriptions -> subscriptions @ [ upcastSubscription ])
        )
        |> ignore

    let get () : EventEnvelope<'E> list = typeof<'E> |> getAll |> asTyped

    static member Empty =
        EventStore(Dictionary(), ConcurrentDictionary())

    static member With(items: EventEnvelope list) =
        EventStore(
            items
            |> List.groupBy (fun i -> (i.Metadata.Source, i.EventType))
            |> dict
            |> Dictionary,
            ConcurrentDictionary()
        )

    member __.Stream name : EventEnvelope<'E> list = stream (name, typeof<'E>) |> asTyped
    member __.Append items = lock __ (fun () -> append items)
    member __.Subscribe(subscription: Subscription<'E>) = subscribe subscription
    member __.Get() = get ()

module Projections =
    type Projection<'State, 'Event> =
        { Init: 'State
          Update: 'State -> 'Event -> 'State }

    let projectIntoMap projection =
        fun state (eventEnvelope: EventEnvelope<_>) ->
            state
            |> Map.tryFind eventEnvelope.Metadata.Source
            |> Option.defaultValue projection.Init
            |> fun projectionState ->
                eventEnvelope.Event
                |> projection.Update projectionState
            |> fun newState ->
                state
                |> Map.add eventEnvelope.Metadata.Source newState

    let project projection (events: EventEnvelope<_> list) =
        events
        |> List.map (fun e -> e.Event)
        |> List.fold projection.Update projection.Init
