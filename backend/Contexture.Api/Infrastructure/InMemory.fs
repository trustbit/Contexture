module Contexture.Api.Infrastructure.Storage.InMemory

open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open FsToolkit.ErrorHandling
open Contexture.Api.Infrastructure.Subscriptions

type Msg =
    private
    | Get of StreamKind * AsyncReplyChannel<EventResult>
    | GetStream of StreamIdentifier * AsyncReplyChannel<StreamResult>
    | GetAll of AsyncReplyChannel<EventResult>
    | Append of EventDefinition list * AsyncReplyChannel<Version * Position>
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

let private selectVersion (_, version, _) = version
let private selectPosition (position, _, _) = position
let private selectItem (_, _, item) = item

let private withMaxPosition (items: (Position * Version * EventEnvelope) list) =
    if List.isEmpty items then
        Position.start, []
    else
        items |> List.maxBy selectPosition |> selectPosition, items |> List.map selectItem

let private withMaxVersion (items: (Position * Version * EventEnvelope) list) =
    if List.isEmpty items then
        Version.start, []
    else
        items |> List.maxBy selectVersion |> selectVersion, items |> List.map selectItem

let private appendToHistory clock (history: History, envelopes) (definition: EventDefinition) =
    let source = definition.Source
    let streamKind = definition.StreamKind
    let key = StreamIdentifier.from source streamKind
    let position = Position.from (int64 history.items.Length + 1L)
    let existingStream = key |> stream history
    let eventVersion = Version.from (existingStream.Length + 1)

    let envelope = {
        Metadata =
            { Source = definition.Source
              RecordedAt = clock ()
              Position = position
              Version = eventVersion
            }
        Payload = definition.Event
        EventType = definition.EventType
        StreamKind = definition.StreamKind
        }
    let fullStream =
        existingStream |> (fun s -> s @ [ position, eventVersion, envelope ])

    history.byIdentifier.[key] <- fullStream
    let allEvents = getAllStreamsOf history streamKind
    history.byEventType.[streamKind] <- allEvents @ [ position, eventVersion, envelope ]

    { history with items = (position, eventVersion, envelope) :: history.items },envelopes @ [ envelope ]

let private singleWriterPipeline clock (initialEvents: EventDefinition list) (inbox: Agent<Msg>) =
    let rec loop state =
        let (subscriptions, history) = state

        async {
            let! msg = inbox.Receive()

            match msg with
            | Get(kind, reply) ->
                kind |> getAllStreamsOf history |> withMaxPosition |> Ok |> reply.Reply

                return! loop state
            | GetStream(identifier, reply) ->
                identifier |> stream history |> withMaxVersion |> Ok |> reply.Reply

                return! loop state
            | GetAll reply ->
                history.items |> withMaxPosition |> Ok |> reply.Reply

                return! loop state
            | Append(eventDefinitions, reply) ->
                let extendedHistory,envelopes = eventDefinitions |> List.fold (appendToHistory clock) (history, List.empty)

                let position, version =
                    extendedHistory.items |> List.head |> (fun (p, v, _) -> p, v)

                reply.Reply(version,position)

                let byIdentifier =
                    envelopes
                    |> List.groupBy (fun e -> StreamIdentifier.from e.Metadata.Source e.StreamKind)
                    |> Map.ofList

                let byKind = envelopes |> List.groupBy (fun e -> e.StreamKind) |> Map.ofList

                let subscriptionsToNotifyWithEvents,remainingSubscriptions =
                    subscriptions
                    |> List.fold (fun (toNotify,other) (definition, s) ->
                        match definition with
                        | FromStream(streamIdentifier, _) when byIdentifier |> Map.containsKey streamIdentifier ->
                            toNotify @ [ (s, byIdentifier |> Map.find streamIdentifier)],other
                        | FromAll _ -> toNotify @ [ s, envelopes],other
                        | FromKind(streamKind, _) when byKind |> Map.containsKey streamKind ->
                            toNotify @ [ s, byKind |> Map.find streamKind],other
                        | _ ->
                            toNotify, s :: other
                        ) (List.empty,List.empty)

                subscriptionsToNotifyWithEvents
                |> List.iter (fun (s, events) -> inbox.Post(Notify(position, events, s)))
                
                remainingSubscriptions
                |> List.iter (fun s -> inbox.Post(Notify(position,List.empty,s)))
               
                return! loop (subscriptions, extendedHistory)
            | Subscribe(definition, subscription, reply) ->
                let skipUntil position items =
                    items
                    |> List.skipWhile (fun (p, _, _) ->
                        match position with
                        | Start -> false
                        | End -> true
                        | From pos when pos = Position.start -> false
                        | From pos -> p <= pos)

                let events =
                    match definition with
                    | FromAll position -> history.items |> skipUntil position
                    | FromKind(kind, position) -> kind |> getAllStreamsOf history |> skipUntil position
                    | FromStream(identifier, version) ->
                        identifier
                        |> stream history
                        |> List.skipWhile (fun (_, v, _) -> if version.IsNone then false else v < version.Value)

                let lastPosition = history.items |> withMaxPosition |> fst

                if not events.IsEmpty then
                    inbox.Post(Notify(lastPosition, events |> List.map selectItem, subscription))
                else
                    inbox.Post(Notify(lastPosition, List.empty, subscription))

                reply.Reply()
                return! loop ((definition, subscription) :: subscriptions, history)

            | Notify(version, events, subscription) ->
                Async.Start <| subscription version events

                return! loop state
        }

    let initialHistory,_ = initialEvents |> List.fold (appendToHistory clock) (History.Empty,[])

    loop ([], initialHistory)

let eventStoreWith clock (initialEvents: EventDefinition list) =
    let agent = Agent<Msg>.Start (singleWriterPipeline clock initialEvents)

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

        member _.Subscribe name definition subscription =
            async {
                let cancel = new CancellationTokenSource()
                let mutable lastVersion = None

                let recordingSubscription position events =
                    async {
                        do! subscription position events
                        lastVersion <- Some position
                    }

                let resultTask =
                    Async.StartAsTask(
                        agent.PostAndAsyncReply(fun reply -> Subscribe(definition, recordingSubscription, reply)),
                        cancellationToken = cancel.Token
                    )

                return
                    { new Subscription with
                        member _.Name = name
                        member _.Status =
                            match lastVersion with
                            | None -> NotRunning
                            | Some v -> CaughtUp v

                        member _.DisposeAsync() =
                            if not cancel.IsCancellationRequested then
                                cancel.Cancel()
                                cancel.Dispose()

                            resultTask.Dispose()
                            ValueTask.CompletedTask }
            } }

let emptyEventStore clock = eventStoreWith clock [ ]
