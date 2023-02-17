module Contexture.Api.Infrastructure.Storage.InMemoryStorage

open System.Collections.Generic
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage

type Msg =
    private
    | Get of StreamKind * AsyncReplyChannel<EventResult>
    | GetStream of StreamIdentifier * AsyncReplyChannel<EventResult>
    | GetAll of AsyncReplyChannel<EventResult>
    | Append of EventEnvelope list * AsyncReplyChannel<Version>
    | Notify of EventEnvelope list * Subscription
    | Subscribe of SubscriptionDefinition * Subscription * AsyncReplyChannel<unit>

type private History =
    { items: (Version * EventEnvelope) list
      byIdentifier: Dictionary<StreamIdentifier, (Version * EventEnvelope) list>
      byEventType: Dictionary<StreamKind, (Version * EventEnvelope) list> }

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

let private withMaxVersion (items: (Version * EventEnvelope) list) =
    if List.isEmpty items then
        Version.start, []
    else
        items |> List.maxBy (fun (Version version, _) -> version) |> fst, items |> List.map snd

let private appendToHistory (history: History) (envelope: EventEnvelope) =
    let source = envelope.Metadata.Source
    let streamKind = envelope.StreamKind
    let key = StreamIdentifier.from source streamKind
    let version = Version.from (history.items.Length + 1)
    let fullStream = key |> stream history |> (fun s -> s @ [ version, envelope ])

    history.byIdentifier.[key] <- fullStream
    let allEvents = getAllStreamsOf history streamKind
    history.byEventType.[streamKind] <- allEvents @ [ version, envelope ]

    { history with items = (version, envelope) :: history.items }

let initialize (initialEvents: EventEnvelope list) =
    let proc (inbox: Agent<Msg>) =
        let rec loop state =
            let (subscriptions, history) = state

            async {
                let! msg = inbox.Receive()

                match msg with
                | Get(kind, reply) ->
                    kind |> getAllStreamsOf history |> withMaxVersion |> Ok |> reply.Reply

                    return! loop state
                | GetStream(identifier, reply) ->
                    identifier |> stream history |> withMaxVersion |> Ok |> reply.Reply

                    return! loop state
                | GetAll reply ->
                    history.items |> withMaxVersion |> Ok |> reply.Reply

                    return! loop state
                | Append(events, reply) ->
                    let extendedHistory = events |> List.fold appendToHistory history

                    let version = extendedHistory.items |> List.last |> fst
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
                    |> List.iter (fun (s, events) -> inbox.Post(Notify(events, s)))

                    return! loop (subscriptions, extendedHistory)
                | Subscribe(definition, subscription, reply) ->
                    let events =
                        match definition with
                        | FromAll position ->
                            let (Position pValue) = position

                            history.items
                            |> List.skipWhile (fun (v, _) ->
                                if position = Position.start then
                                    false
                                else
                                    v <= Version pValue)
                        | FromKind(kind, position) ->
                            let (Position pValue) = position

                            kind
                            |> getAllStreamsOf history
                            |> List.skipWhile (fun (v, _) ->
                                if position = Position.start then
                                    false
                                else
                                    v <= Version pValue)
                        | FromStream(identifier, version) ->
                            identifier
                            |> stream history
                            |> List.skipWhile (fun (v, _) -> if version.IsNone then false else v < version.Value)

                    if not events.IsEmpty then
                        inbox.Post(Notify(events |> List.map snd, subscription))

                    reply.Reply()
                    return! loop ((definition, subscription) :: subscriptions, history)

                | Notify(events, subscription) ->
                    do! subscription events

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
            agent.PostAndAsyncReply(fun reply -> Subscribe(definition, subscription, reply)) }

let empty () = initialize []
