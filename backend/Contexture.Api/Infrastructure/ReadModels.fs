module Contexture.Api.Infrastructure.ReadModels

open System.Threading.Tasks
open Contexture.Api.Infrastructure.Storage

type EventHandler<'Event> = EventEnvelope<'Event> list -> Async<unit>

type ReadModelInitialization =
    abstract member ReplayAndConnect: SubscriptionStartingPosition -> Async<Subscription>

module ReadModelInitialization =
    type private RMI<'Event>(eventStore: EventStore, handler: EventHandler<'Event>) =
        interface ReadModelInitialization with
            member _.ReplayAndConnect position =
                eventStore.Subscribe position  handler
                

    let initializeWith (eventStore: EventStore) (handler: EventHandler<'Event>) : ReadModelInitialization =
        RMI(eventStore, handler) :> ReadModelInitialization

type ReadModel<'Event, 'State> =
    abstract member EventHandler: EventEnvelope<'Event> list -> Async<unit>
    abstract member State: unit -> Task<'State>

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
                    | Notify(eventEnvelopes, reply) ->
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
