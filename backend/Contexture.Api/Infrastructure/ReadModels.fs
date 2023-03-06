module Contexture.Api.Infrastructure.ReadModels

open System.Threading.Tasks
open Contexture.Api.Infrastructure.Storage
open Microsoft.AspNetCore.Http

type ReadModelInitialization =
    abstract member ReplayAndConnect: SubscriptionStartingPosition -> Async<Subscription>

module ReadModelInitialization =
    type private RMI<'Event>(eventStore: EventStore, name: string, handler: SubscriptionHandler<'Event>) =
        interface ReadModelInitialization with
            member _.ReplayAndConnect starting = eventStore.Subscribe name starting handler

    let initializeWith (eventStore: EventStore) name (handler: SubscriptionHandler<'Event>) : ReadModelInitialization =
        RMI(eventStore, name, handler) :> ReadModelInitialization

type IRetrieveState<'State> =
    abstract State : unit -> Task<'State>
    abstract State : Position -> Task<'State>
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
    
    let appendProcessedPosition (url: string) (position:Position) =
        let parts = url.Split("?")
        let queryString =
            if parts.Length = 2 then
                QueryString("?" + parts[1])
            else
                QueryString()
                
        PathString(parts[0]).Add(queryString.Add(processedPosition, position |> Position.value |> string))

    let fromReadModel<'R when 'R :> ReadModel> (ctx: HttpContext) =
        ctx.GetService<'R>()
    let fetch findReadModel (ctx: HttpContext) : Task<'S> =
        let readModel = findReadModel ctx
        let stateRetriever = unbox<IRetrieveState<'S>> readModel
        match ctx.TryGetQueryStringValue processedPosition |> Option.bind (Position.parse) with
        | Some position ->
            stateRetriever.State position
        | None ->
            stateRetriever.State()


type Msg<'Event, 'Result> =
    private
    | Notify of Position * EventEnvelope<'Event> list * AsyncReplyChannel<unit>
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

    { new ReadModel<'Event, 'State> with
        member _.EventHandler position eventEnvelopes =
            agent.PostAndAsyncReply(fun reply -> Notify(position, eventEnvelopes, reply))

        member _.State() =
            agent.PostAndAsyncReply State |> Async.StartAsTask
        
        member _.State position =
            agent.PostAndAsyncReply (fun reply -> StateAfter(position,reply)) |> Async.StartAsTask
    }
