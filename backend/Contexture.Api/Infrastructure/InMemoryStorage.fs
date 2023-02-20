module Contexture.Api.Infrastructure.Storage.InMemoryStorage

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open FsToolkit.ErrorHandling

type Msg =
    private
    | Get of StreamKind * AsyncReplyChannel<EventResult>
    | GetStream of StreamIdentifier * AsyncReplyChannel<EventResult>
    | GetAll of AsyncReplyChannel<EventResult>
    | Append of EventEnvelope list * AsyncReplyChannel<Version>
    | Notify of Position * EventEnvelope list * (Position -> EventEnvelope list -> Async<unit>)
    | Subscribe of SubscriptionDefinition * (Position -> EventEnvelope list -> Async<unit>) * AsyncReplyChannel<unit>

type private History =
    { items: (Position * Version * EventEnvelope) list
      byIdentifier: Dictionary<StreamIdentifier, (Position * Version * EventEnvelope) list>
      byEventType: Dictionary<StreamKind, (Position * Version * EventEnvelope) list> }

    static member Empty =
        { items = []
          byIdentifier = Dictionary()
          byEventType = Dictionary() }

let private stream history source =
    let (success, events) = history.byIdentifier.TryGetValue source
    if success then events else []

let private getAllStreamsOf history key =
    let (success, items) = history.byEventType.TryGetValue key
    if success then items else []

let private withMaxPosition (items: (Position * Version * EventEnvelope) list) =
    if List.isEmpty items then
        Position.start, []
    else
        items |> List.maxBy (fun (Position pos,_, _) -> pos) |> fun (pos,_,_) -> pos,
        items |> List.map (fun (_,_,item) -> item)
        
let private withMaxVersion (items: (Position * Version * EventEnvelope) list) =
    if List.isEmpty items then
        Version.start, []
    else
        items |> List.maxBy (fun (_, Version version, _) -> version) |> fun (_,version,_) -> version,
        items |> List.map (fun (_,_,item) -> item)

let private appendToHistory (history: History) (envelope: EventEnvelope) =
    let source = envelope.Metadata.Source
    let streamKind = envelope.StreamKind
    let key = StreamIdentifier.from source streamKind
    let position = Position.from (int64 history.items.Length + 1L)
    let existingStream = key |> stream history
    let eventVersion = Version.from (existingStream.Length + 1)
    let fullStream = existingStream |> (fun s -> s @ [ position, eventVersion, envelope ])

    history.byIdentifier.[key] <- fullStream
    let allEvents = getAllStreamsOf history streamKind
    history.byEventType.[streamKind] <- allEvents @ [ position, eventVersion, envelope ]

    { history with items = (position,eventVersion, envelope) :: history.items }

let initialize (initialEvents: EventEnvelope list) =
    let proc (inbox: Agent<Msg>) =
        let rec loop state =
            let (subscriptions, history) = state

            async {
                let! msg = inbox.Receive()

                match msg with
                | Get(kind, reply) ->
                    kind |> getAllStreamsOf history |> withMaxPosition |> Ok |> reply.Reply

                    return! loop state
                | GetStream(identifier, reply) ->
                    identifier |> stream history |> withMaxPosition |> Ok |> reply.Reply

                    return! loop state
                | GetAll reply ->
                    history.items |> withMaxPosition |> Ok |> reply.Reply

                    return! loop state
                | Append(events, reply) ->
                    let extendedHistory = events |> List.fold appendToHistory history

                    let (position,version) = extendedHistory.items |> List.last |> fun (p,v,_) -> p,v
                    reply.Reply(version)

                    let byIdentifier =
                        events
                        |> List.groupBy (fun e -> StreamIdentifier.from e.Metadata.Source e.StreamKind)
                        |> Map.ofList

                    let byKind = events |> List.groupBy (fun e -> e.StreamKind) |> Map.ofList

                    let subscriptionsToNotify =
                        subscriptions
                        |> List.choose (fun (definition, s) ->
                            match definition with
                            | FromStream(streamIdentifier, versionOption) when
                                byIdentifier |> Map.containsKey streamIdentifier
                                ->
                                Some(s, byIdentifier |> Map.find streamIdentifier)
                            | FromAll _ -> Some(s, events)
                            | FromKind(streamKind, position) when byKind |> Map.containsKey streamKind ->
                                Some(s, byKind |> Map.find streamKind)
                            | _ -> None)

                    subscriptionsToNotify
                    |> List.iter (fun (s, events) -> inbox.Post(Notify(position, events, s)))

                    return! loop (subscriptions, extendedHistory)
                | Subscribe(definition, subscription, reply) ->
                    let events =
                        match definition with
                        | FromAll position ->
                            history.items
                            |> List.skipWhile (fun (p,_, _) ->
                                if position = Position.start then
                                    false
                                else
                                    p <= position)
                        | FromKind(kind, position) ->
                            kind
                            |> getAllStreamsOf history
                            |> List.skipWhile (fun (p,_, _) ->
                                if position = Position.start then
                                    false
                                else
                                    p <= position)
                        | FromStream(identifier, version) ->
                            identifier
                            |> stream history
                            |> List.skipWhile (fun (_,v, _) -> if version.IsNone then false else v < version.Value)

                    if not events.IsEmpty then
                        inbox.Post(
                            Notify(
                                events |> List.map (fun (p,_,_) -> p) |> List.max,
                                events |> List.map (fun (_,_,i) -> i),
                                subscription
                            )
                        )
                    else
                        inbox.Post(
                            Notify(
                                Position.start,
                                List.empty,
                                subscription
                            )
                        )

                    reply.Reply()
                    return! loop ((definition, subscription) :: subscriptions, history)

                | Notify(version, events, subscription) ->
                    do! subscription version events

                    return! loop state
            }

        let initialHistory = initialEvents |> List.fold appendToHistory History.Empty

        loop ([], initialHistory)

    let agent = Agent<Msg>.Start (proc)

    { new EventStorage with
        member _.Stream version identifier =
            agent.PostAndAsyncReply(fun reply -> GetStream(identifier, reply))

        member _.AllStreamsOf streamType =
            agent.PostAndAsyncReply(fun reply -> Get(streamType, reply))

        member _.Append identifier expectedVersion events =
            async {
                let! result = agent.PostAndAsyncReply(fun reply -> Append(events, reply))
                return Ok result
            }

        member _.All() =
            agent.PostAndAsyncReply(fun reply -> GetAll(reply))

        member _.Subscribe definition subscription =
            let cancel = new CancellationTokenSource()
            let mutable lastVersion = None
            let recordingSubscription version events = async {
                do! subscription events
                lastVersion <- Some version
                }
                
            let resultTask =
                Async.StartAsTask(
                    agent.PostAndAsyncReply(fun reply -> Subscribe(definition, recordingSubscription, reply)),
                    cancellationToken = cancel.Token
                )
                 
            { new Subscription with
                member _.Status =
                    match lastVersion with
                    | None -> NotRunning
                    | Some v -> CaughtUp v
                member _.DisposeAsync() =
                    if not cancel.IsCancellationRequested then
                        cancel.Cancel()
                        cancel.Dispose()
                        
                    resultTask.Dispose()
                    ValueTask.CompletedTask
            }
    }

let empty () = initialize []
