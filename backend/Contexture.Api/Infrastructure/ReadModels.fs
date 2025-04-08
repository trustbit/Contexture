module Contexture.Api.Infrastructure.ReadModels

open System.Threading.Tasks
open Contexture.Api.Infrastructure.Subscriptions
open Microsoft.AspNetCore.Http

type ReadModelInitialization =
    abstract member ReplayAndConnect: SubscriptionStartingPosition -> Async<Subscription>

module ReadModelInitialization =
    let initializeWith (eventStore: EventStore) name (handler: SubscriptionHandler<'Event>) : ReadModelInitialization =
        {
            new ReadModelInitialization with                
                member this.ReplayAndConnect(position: SubscriptionStartingPosition): Async<Subscription> = 
                    eventStore.Subscribe name position handler
        }

    let initializeFromAll (eventStore: EventStore) convert name (handler: SubscriptionHandler<'Event>) : ReadModelInitialization =
        {
            new ReadModelInitialization with                
                member this.ReplayAndConnect(position: SubscriptionStartingPosition): Async<Subscription> = 
                    eventStore.SubscribeAll convert name position handler
        }

type IRetrieveState<'State> =
    abstract State : Position option -> Task<'State>

[<AutoOpen>]
module Extensions =
    type IRetrieveState<'State> with
        member this.State () =
            this.State None
type ReadModel = interface end
type ReadModel<'Event, 'State> =
    inherit IRetrieveState<'State>
    inherit ReadModel
    abstract member EventHandler: Position -> EventEnvelope<'Event> list -> Async<unit>
    
module State =
    open Microsoft.AspNetCore.Http
    open Giraffe
    open System
    
    [<Literal>]
    let private processedPosition = "processedPosition"
    
    let appendProcessedPosition (url: string) (position: Position option) =
        match position with
        | Some position ->
            let parts = url.Split("?")
            let queryString =
                if parts.Length = 2 then
                    QueryString("?" + parts[1])
                else
                    QueryString()
                    
            PathString(parts[0]).Add(queryString.Add(processedPosition, position |> Position.value |> string))
        | None -> url

    let fromReadModel<'R when 'R :> ReadModel> (ctx: HttpContext) =
        ctx.GetService<'R>()
    let fetch findReadModel (ctx: HttpContext) : Task<'S> =
        let readModel = findReadModel ctx
        let stateRetriever = unbox<IRetrieveState<'S>> readModel
        ctx.TryGetQueryStringValue processedPosition
        |> Option.bind Position.parse
        |> stateRetriever.State

type Msg<'Event, 'Result> =
    private
    | Notify of Position * EventEnvelope<'Event> list * AsyncReplyChannel<unit>
    | State of AsyncReplyChannel<'Result>
    | StateAfter of Position * AsyncReplyChannel<'Result>

let readModel
    (updateState: 'State -> EventEnvelope<'Event> list -> 'State)
    (initState: 'State)
    (replyTimeout: int option)
    : ReadModel<'Event, 'State> =
    let agent =
        let eventSubscriber (inbox: Agent<Msg<_, _>>) =
            let rec loop (processedPosition,state) =
                async {
                    let! msg = inbox.Receive()

                    match msg with
                    | Notify(maxPosition,eventEnvelopes, reply) ->
                        reply.Reply()
                        return! loop (maxPosition, eventEnvelopes |> updateState state)

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

    let replyTimeoutMiliseconds = replyTimeout |> Option.defaultValue -1
    { new ReadModel<'Event, 'State> with
        member _.EventHandler position eventEnvelopes =
            agent.PostAndAsyncReply((fun reply -> Notify(position, eventEnvelopes, reply)), replyTimeoutMiliseconds)

        member _.State position =
            match position with
            | Some position ->
                agent.PostAndAsyncReply ((fun reply -> StateAfter(position,reply)), replyTimeoutMiliseconds) |> Async.StartAsTask
            | None ->
                agent.PostAndAsyncReply (State, replyTimeoutMiliseconds) |> Async.StartAsTask
    }
