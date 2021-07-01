namespace Contexture.Api.Infrastructure

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Threading.Tasks

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
type SubscriptionAsync<'E> = EventEnvelope<'E> list -> Async<unit>

type private SubscriptionWrapper = EventEnvelope list -> Async<unit>

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
        |> List.map
            (fun subscription ->
                let upcastSubscription events = events |> asUntyped |> subscription

                upcastSubscription newItems)
        |> Async.Sequential
        |> Async.RunSynchronously
        |> ignore

    let subscribeAsync (subscription: SubscriptionAsync<'E>) =
        let key = typedefof<'E>

        let upcastSubscription events = events |> asTyped |> subscription

        subscriptions.AddOrUpdate(
            key,
            (fun _ -> [ upcastSubscription ]),
            (fun _ subscriptions -> subscriptions @ [ upcastSubscription ])
        )
        |> ignore

    let subscribe (subscription: Subscription<'E>) =
        subscribeAsync (fun events -> async { subscription events })


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

    member __.Stream name : Async<EventEnvelope<'E> list> =
        async {
            let events = stream (name, typeof<'E>) |> asTyped
            return events
        }

    member __.Append items =
        lock __ (fun () -> append items)
        async { return () }

    member __.Subscribe(subscription: Subscription<'E>) = subscribe subscription
    member __.SubscribeAsync(subscription: SubscriptionAsync<'E>) = subscribeAsync subscription
    member __.AllStreams() = async { return get () }

module Projections =
    type Projection<'State, 'Event> =
        { Init: 'State
          Update: 'State -> 'Event -> 'State }

    let projectIntoMap selectId projection =
        fun state (eventEnvelope: EventEnvelope<_>) ->
            let selectedId = selectId eventEnvelope

            state
            |> Map.tryFind selectedId
            |> Option.defaultValue projection.Init
            |> fun projectionState ->
                eventEnvelope.Event
                |> projection.Update projectionState
            |> fun newState -> state |> Map.add selectedId newState

    let projectIntoMapBySourceId projection =
        projectIntoMap (fun eventEnvelope -> eventEnvelope.Metadata.Source) projection

    let project projection (events: EventEnvelope<_> list) =
        events
        |> List.map (fun e -> e.Event)
        |> List.fold projection.Update projection.Init


module ReadModels =
    type EventHandler<'Event> = EventEnvelope<'Event> list -> Async<unit>

    type ReadModelInitialization =
        abstract member ReplayAndConnect : unit -> Async<unit>

    module ReadModelInitialization =
        type private RMI<'Event>(eventStore: EventStore, handler: EventHandler<'Event>) =
            interface ReadModelInitialization with
                member __.ReplayAndConnect() =
                    async {
                        let! allStreams = eventStore.AllStreams<'Event>()
                        do! handler allStreams
                        eventStore.SubscribeAsync handler
                    }

        let initializeWith (eventStore: EventStore) (handler: EventHandler<'Event>) : ReadModelInitialization =
            RMI(eventStore, handler) :> ReadModelInitialization


    type ReadModel<'Event, 'State> =
        abstract member EventHandler : EventEnvelope<'Event> list -> Async<unit>
        abstract member State : unit -> Task<'State>


    type Agent<'T> = MailboxProcessor<'T>

    type Msg<'Event, 'Result> =
        | Notify of EventEnvelope<'Event> list * AsyncReplyChannel<unit>
        | State of AsyncReplyChannel<'Result>

    let readModel
        (updateState: 'State -> EventEnvelope<'Event> list -> 'State)
        (initState: 'State)
        : ReadModel<'Event, 'State> =
        let agent =
            let eventSubscriber (inbox: Agent<Msg<_, _>>) =
                let rec loop state =
                    async {
                        let! msg = inbox.Receive()

                        match msg with
                        | Notify (eventEnvelopes, reply) ->
                            reply.Reply()
                            return! loop (eventEnvelopes |> updateState state)

                        | State reply ->
                            reply.Reply state
                            return! loop state
                    }

                loop initState

            Agent<Msg<_, _>>.Start (eventSubscriber)

        { new ReadModel<'Event, 'State> with
            member _.EventHandler eventEnvelopes =
                agent.PostAndAsyncReply(fun reply -> Notify(eventEnvelopes, reply))

            member _.State() =
                agent.PostAndAsyncReply State |> Async.StartAsTask }
