module Contexture.Api.Infrastructure.ReadModels

open System.Threading.Tasks
open Contexture.Api.Infrastructure.Storage

type EventHandler<'Event> = EventEnvelope<'Event> list -> Async<unit>

type ReadModelInitialization =
    abstract member ReplayAndConnect: SubscriptionStartingPosition -> Async<Subscription>

module ReadModelInitialization =
    type private RMI<'Event>(eventStore: EventStore, handler: EventHandler<'Event>) =
        interface ReadModelInitialization with
            member _.ReplayAndConnect position = eventStore.Subscribe position handler


    let initializeWith (eventStore: EventStore) (handler: EventHandler<'Event>) : ReadModelInitialization =
        RMI(eventStore, handler) :> ReadModelInitialization

type ReadModel<'Event, 'State> =
    abstract member EventHandler: EventEnvelope<'Event> list -> Async<unit>
    abstract member State: unit -> Task<'State>
    abstract member State: Position -> Task<'State>

type Msg<'Event, 'Result> =
    private
    | Notify of EventEnvelope<'Event> list * AsyncReplyChannel<unit>
    | State of AsyncReplyChannel<'Result>
    | StateAfter of Position * AsyncReplyChannel<'Result>

let readModel
    (updateState: 'State -> EventEnvelope<'Event> list -> 'State)
    (initState: 'State)
    : ReadModel<'Event, 'State> =
    let agent =
        let eventSubscriber (inbox: Agent<Msg<_, _>>) =
            let rec loop (processedPosition,state) =
                async {
                    let! msg = inbox.Receive()

                    match msg with
                    | Notify(eventEnvelopes, reply) ->
                        reply.Reply()
                        
                        let highestPosition =
                            eventEnvelopes
                            |> List.map(fun e -> e.Metadata.Position)
                            |> List.maxOr processedPosition
                        return! loop (highestPosition, eventEnvelopes |> updateState state)

                    | State reply ->
                        reply.Reply state
                        return! loop (processedPosition,state)
                    | StateAfter(position, reply) ->
                        if position > processedPosition then
                            inbox.Post(StateAfter(position,reply))
                        else
                            reply.Reply state
                        return! loop (processedPosition,state)
                        
                }

            loop (Position.start,initState)

        Agent<Msg<_, _>>.Start (eventSubscriber)

    { new ReadModel<'Event, 'State> with
        member _.EventHandler eventEnvelopes =
            agent.PostAndAsyncReply(fun reply -> Notify(eventEnvelopes, reply))

        member _.State() =
            agent.PostAndAsyncReply State |> Async.StartAsTask
        
        member _.State position =
            agent.PostAndAsyncReply (fun reply -> StateAfter(position,reply)) |> Async.StartAsTask
    }
