module Contexture.Api.Infrastructure.Storage.InMemory

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open FsToolkit.ErrorHandling
open Contexture.Api.Infrastructure.Subscriptions
open Microsoft.Extensions.Logging

module private SingleWriterPipeline =
    type Msg =
        | Get of StreamKind * AsyncReplyChannel<EventResult>
        | GetStream of StreamIdentifier * AsyncReplyChannel<StreamResult>
        | GetAll of AsyncReplyChannel<EventResult>
        | Append of NonEmptyList<EventDefinition> * AsyncReplyChannel<Version * Position>
        | ForwardTo of
            string *
            SubscriptionDefinition *
            (Position -> EventEnvelope list -> unit) *
            AsyncReplyChannel<unit>

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

        let envelope =
            { Metadata =
                { Source = definition.Source
                  RecordedAt = clock ()
                  Position = position
                  Version = eventVersion }
              Payload = definition.Event
              EventType = definition.EventType
              StreamKind = definition.StreamKind }

        let fullStream =
            existingStream |> (fun s -> s @ [ position, eventVersion, envelope ])

        history.byIdentifier.[key] <- fullStream
        let allEvents = getAllStreamsOf history streamKind
        history.byEventType.[streamKind] <- allEvents @ [ position, eventVersion, envelope ]

        { history with items = (position, eventVersion, envelope) :: history.items }, envelopes @ [ envelope ]

    let singleWriterPipeline clock (initialEvents: EventDefinition list) (inbox: Agent<Msg>) =
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
                    let extendedHistory, envelopes =
                        eventDefinitions
                        |> NonEmptyList.asList
                        |> List.fold (appendToHistory clock) (history, List.empty)

                    let position, version =
                        extendedHistory.items |> List.head |> (fun (p, v, _) -> p, v)

                    reply.Reply(version, position)

                    let byIdentifier =
                        envelopes
                        |> List.groupBy (fun e -> StreamIdentifier.from e.Metadata.Source e.StreamKind)
                        |> Map.ofList

                    let byKind = envelopes |> List.groupBy (fun e -> e.StreamKind) |> Map.ofList

                    let subscriptionsToNotifyWithEvents, remainingSubscriptions =
                        subscriptions
                        |> Map.toList
                        |> List.fold
                            (fun (toNotify, other) (name, (definition, s)) ->
                                match definition with
                                | FromStream(streamIdentifier, _) when byIdentifier |> Map.containsKey streamIdentifier ->
                                    toNotify @ [ (name, s, byIdentifier |> Map.find streamIdentifier) ], other
                                | FromAll _ -> toNotify @ [ name, s, envelopes ], other
                                | FromKind(streamKind, _) when byKind |> Map.containsKey streamKind ->
                                    toNotify @ [ name, s, byKind |> Map.find streamKind ], other
                                | _ -> toNotify, (name, s) :: other)
                            (List.empty, List.empty)

                    subscriptionsToNotifyWithEvents
                    |> List.iter (fun (name, s, events) -> s position events)

                    remainingSubscriptions |> List.iter (fun (name, s) -> s position List.Empty)

                    return! loop (subscriptions, extendedHistory)
                | ForwardTo(name, definition, subscription, reply) ->
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
                        subscription lastPosition (events |> List.map selectItem)
                    else
                        subscription lastPosition List.empty

                    reply.Reply()
                    return! loop (subscriptions |> Map.add name (definition, subscription), history)
            }

        let initialHistory, _ =
            initialEvents |> List.fold (appendToHistory clock) (History.Empty, [])

        loop (Map.empty, initialHistory)

module private SingleWriterSubscription =
    type Msg =
        | Enqueue of Position * EventEnvelope list
        | Process
        | Status of AsyncReplyChannel<Choice<Position option, Position option>>

    let subscriptionFor (name: string) (subscription: SubscriptionHandler) =
        let agent =
            Agent<Msg>.Start
                (fun inbox ->
                    let rec loop state =
                        let processing, position, backlog = state

                        async {
                            let! msg = inbox.Receive()

                            match msg with
                            | Enqueue(enqueuePosition, events) ->
                                let newState =
                                    backlog |> List.append events |> List.sortBy (fun e -> e.Metadata.Position)

                                let newPosition =
                                    match position with
                                    | Some p when p < enqueuePosition -> enqueuePosition
                                    | Some p -> p
                                    | _ -> enqueuePosition

                                if not processing then
                                    inbox.Post Process

                                let b = name
                                return! loop (true, Some newPosition, newState)
                            | Process ->
                                match position with
                                | Some p ->
                                    do! subscription p backlog
                                    return! loop (false, position, List.Empty)
                                | None -> return! loop (false, position, backlog)
                            | Status reply ->
                                let b = name

                                if backlog.IsEmpty then
                                    reply.Reply(Choice1Of2 position)
                                else
                                    reply.Reply(Choice2Of2 position)

                                return! loop state
                        }

                    loop (false, None, List.empty))

        agent.Error.Add(fun e -> System.Console.WriteLine(e.ToString()))
        agent.Post Process
        agent

let eventStoreWith (loggerFactory: ILoggerFactory) clock (initialEvents: EventDefinition list) =
    let logger = loggerFactory.CreateLogger("InMemory-EventStore")

    let agent =
        Agent<SingleWriterPipeline.Msg>.Start (SingleWriterPipeline.singleWriterPipeline clock initialEvents)

    { new EventStorage with
        member _.Stream version identifier =
            agent.PostAndAsyncReply(fun reply -> SingleWriterPipeline.GetStream(identifier, reply))

        member _.AllStreamsOf streamType =
            agent.PostAndAsyncReply(fun reply -> SingleWriterPipeline.Get(streamType, reply))

        member _.Append identifier expectedVersion events =
            agent.PostAndAsyncReply(fun reply -> SingleWriterPipeline.Append(events, reply))
            |> Async.map Ok

        member _.All() =
            agent.PostAndAsyncReply(SingleWriterPipeline.GetAll)

        member _.Subscribe name definition subscription =
            let cancel = new CancellationTokenSource()
            let subscriptionStorage = SingleWriterSubscription.subscriptionFor name subscription

            let enqueue position events =
                subscriptionStorage.Post(SingleWriterSubscription.Enqueue(position, events))

            let resultTask =
                Async.StartAsTask(
                    agent.PostAndAsyncReply(fun reply ->
                        SingleWriterPipeline.ForwardTo(name, definition, enqueue, reply)),
                    cancellationToken = cancel.Token
                )

            let subscriptionInstance =
                { new Subscription with
                    member _.Name = name

                    member _.Status =
                        match subscriptionStorage.PostAndReply(SingleWriterSubscription.Status) with
                        | Choice1Of2(Some position) -> CaughtUp position
                        | Choice2Of2(Some position) -> Processing position
                        | _ -> NotRunning

                    member _.DisposeAsync() =
                        if not cancel.IsCancellationRequested then
                            cancel.Cancel()
                            cancel.Dispose()

                        (subscriptionStorage :> IDisposable).Dispose()
                        resultTask.Dispose()
                        ValueTask.CompletedTask }

            Async.retn subscriptionInstance }

let emptyEventStore factory clock = eventStoreWith factory clock []
