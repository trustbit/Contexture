namespace Contexture.Api.Reactions

open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.FileBasedCommandHandlers
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Projections
open Contexture.Api.Infrastructure.Subscriptions
open Contexture.Api.Infrastructure.Subscriptions.PositionStorage
open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.AsyncResult
open Microsoft.Extensions.Logging

type AllEvents =
    | BoundedContexts of BoundedContext.Event
    | Domains of Domain.Event
    | Namespaces of Namespace.Event
    | NamespaceTemplates of NamespaceTemplate.Event
    | Collaboration of Collaboration.Event

    static member fromEnvelope(event: EventEnvelope) =
        match event.StreamKind with
        | kind when kind = StreamKind.Of<BoundedContext.Event>() ->
            event |> EventEnvelope.unbox |> EventEnvelope.map BoundedContexts |> Some
        | kind when kind = StreamKind.Of<Domain.Event>() ->
            event |> EventEnvelope.unbox |> EventEnvelope.map Domains |> Some
        | kind when kind = StreamKind.Of<Namespace.Event>() ->
            event |> EventEnvelope.unbox |> EventEnvelope.map Namespaces |> Some
        | kind when kind = StreamKind.Of<NamespaceTemplate.Event>() ->
            event |> EventEnvelope.unbox |> EventEnvelope.map NamespaceTemplates |> Some
        | kind when kind = StreamKind.Of<Collaboration.Event>() ->
            event |> EventEnvelope.unbox |> EventEnvelope.map Collaboration |> Some
        | _ -> None

    static member select<'E> (event: AllEvents) =
        match event with
        | e when typeof<'E> = typeof<AllEvents> -> e |> unbox<'E>
        | BoundedContexts e when typeof<'E> = typeof<BoundedContext.Event> -> e |> unbox<'E>
        | Domains e when typeof<'E> = typeof<Domain.Event>-> e |> unbox<'E>
        | Namespaces e when typeof<'E> = typeof<Namespace.Event>-> e |> unbox<'E>
        | NamespaceTemplates e when typeof<'E> = typeof<NamespaceTemplate.Event>-> e |> unbox<'E>
        | Collaboration e when typeof<'E> = typeof<Collaboration.Event>-> e |> unbox<'E>
        | other -> failwithf "Unable to match %s from %O" typeof<'E>.FullName other
        
type Reaction<'State, 'Event> =
        abstract member Projection: Projection<'State, 'Event>
        abstract member Reaction: 'State -> EventEnvelope<'Event> -> Async<unit>

module Reaction =
    let fromAllEvents map (reaction: Reaction<'State,'E>) : Reaction<'State,AllEvents> =
        { new Reaction<_,_> with
            member _.Projection =
                { Update = fun state event -> reaction.Projection.Update state (map event)
                  Init = reaction.Projection.Init }
            member _.Reaction state event = reaction.Reaction state (EventEnvelope.map map event)
        }

type ReactionInitialization =
    abstract member ReplayAndConnect: unit -> Async<Subscription>

module ReactionInitialization =
    let trackStateInMemory
        (initialState: 'State)
        (reaction: Reaction<'State, AllEvents>)
        : SubscriptionHandler<AllEvents> =
        let mutable state = initialState

        fun _ events ->
            async {
                do!
                    events
                    |> List.map (reaction.Reaction state)
                    |> Async.Sequential
                    |> Async.Ignore

                state <- List.fold reaction.Projection.Update state (events |> List.map (fun e -> e.Event))
                ()
            }
    type private ReplayFromStartWithAllEventsReactionInitialization<'State>
        (
            logger: ILogger,
            store: EventStore,
            storage: IStorePositions,
            name: string,
            reaction: Reaction<'State, AllEvents>
        ) =
        
        interface ReactionInitialization with
            member _.ReplayAndConnect() =
                async {
                    use _ = logger.BeginScope("Initializing Reaction {Name}", name)
                    let! lastProcessedPosition = storage.LastPosition name
                    logger.LogDebug("Replaying until {LastProcessedPosition}", lastProcessedPosition)
                    let! _, allData = store.All AllEvents.fromEnvelope
                    logger.LogDebug("Fetched {EventCount} events", allData.Length)

                    let initialState =
                        allData
                        |> List.filter (fun e ->
                            match lastProcessedPosition with
                            | Some processed -> e.Metadata.Position <= processed
                            | None -> true)
                        |> List.map (fun e -> e.Event)
                        |> List.fold reaction.Projection.Update reaction.Projection.Init

                    logger.LogDebug("State {StateType} initialized", typeof<'State>.FullName)

                    let reaction =
                        SubscriptionHandler.trackPosition
                            (storage.SavePosition name)
                            (trackStateInMemory initialState reaction)

                    let startingPosition =
                        lastProcessedPosition |> Option.map From |> Option.defaultValue Start

                    let! subscription = store.SubscribeAll AllEvents.fromEnvelope name startingPosition reaction
                    return subscription
                }

    let initializeWithReplayFromStartWithAllEvents
        logger
        (store: EventStore)
        (storage: IStorePositions)
        name
        (reaction: Reaction<'State, 'E>)
        : ReactionInitialization =
        ReplayFromStartWithAllEventsReactionInitialization(logger, store, storage, name, Reaction.fromAllEvents AllEvents.select<'E> reaction)
        :> ReactionInitialization


module CascadeDelete =
    open Domain.ValueObjects
    open BoundedContext.ValueObjects
    open Collaboration.ValueObjects

    type State =
        { DomainChildren: Map<DomainId, Set<DomainId>>
          BoundedContexts: Map<DomainId, Set<BoundedContextId>>
          DomainCollaborations: Map<DomainId, Set<CollaborationId>>
          BoundedContextCollaborations: Map<BoundedContextId, Set<CollaborationId>>
          BoundedContextNamespaces: Map<BoundedContextId, Set<Namespace.ValueObjects.NamespaceId>> }

        static member Initial =
            { DomainChildren = Map.empty
              BoundedContexts = Map.empty
              DomainCollaborations = Map.empty
              BoundedContextCollaborations = Map.empty
              BoundedContextNamespaces = Map.empty }

    let private removeEntry key item (map: Map<_, Set<_>>) =
        map
        |> Map.change key (fun items ->
            match items with
            | Some items -> items |> Set.remove item |> Some
            | None -> None)

    let private addEntry key item (map: Map<_, Set<_>>) =
        map
        |> Map.change key (fun items ->
            match items with
            | Some items -> items |> Set.add item
            | None -> item |> Set.singleton
            |> Some)

    let project state =
        function
        | BoundedContexts(BoundedContext.BoundedContextImported bc) ->
            { state with BoundedContexts = state.BoundedContexts |> addEntry bc.DomainId bc.BoundedContextId }
        | BoundedContexts(BoundedContext.BoundedContextCreated bc) ->
            { state with BoundedContexts = state.BoundedContexts |> addEntry bc.DomainId bc.BoundedContextId }
        | BoundedContexts(BoundedContext.BoundedContextMovedToDomain bc) ->
            { state with
                BoundedContexts =
                    state.BoundedContexts
                    |> removeEntry bc.OldDomainId bc.BoundedContextId
                    |> addEntry bc.DomainId bc.BoundedContextId }
        | BoundedContexts(BoundedContext.BoundedContextRemoved bc) ->
            { state with
                BoundedContexts = state.BoundedContexts |> removeEntry bc.DomainId bc.BoundedContextId
                BoundedContextCollaborations = Map.remove bc.BoundedContextId state.BoundedContextCollaborations }
        | Domains(Domain.SubDomainCreated d) ->
            { state with DomainChildren = state.DomainChildren |> addEntry d.ParentDomainId d.DomainId }
        | Domains(Domain.DomainImported d) when d.ParentDomainId.IsSome ->
            { state with DomainChildren = state.DomainChildren |> addEntry d.ParentDomainId.Value d.DomainId }
        | Domains(Domain.PromotedToDomain d) ->
            { state with DomainChildren = state.DomainChildren |> removeEntry d.OldParentDomain d.DomainId }
        | Domains(Domain.CategorizedAsSubdomain d) when d.OldParentDomainId.IsSome ->
            { state with
                DomainChildren =
                    state.DomainChildren
                    |> removeEntry d.OldParentDomainId.Value d.DomainId
                    |> addEntry d.ParentDomainId d.DomainId }
        | Domains(Domain.CategorizedAsSubdomain d) when d.OldParentDomainId.IsNone ->
            { state with DomainChildren = state.DomainChildren |> addEntry d.ParentDomainId d.DomainId }
        | Domains(Domain.DomainRemoved d) when d.OldParentDomain.IsSome ->
            { state with
                DomainChildren = state.DomainChildren |> removeEntry d.OldParentDomain.Value d.DomainId
                DomainCollaborations = Map.remove d.DomainId state.DomainCollaborations }
        | Domains(Domain.DomainRemoved d) when d.OldParentDomain.IsNone ->
            { state with DomainCollaborations = Map.remove d.DomainId state.DomainCollaborations }

        | Collaboration(Collaboration.CollaboratorsConnected c) ->
            let addToState collaborator state =
                match collaborator with
                | BoundedContext bc ->
                    { state with
                        BoundedContextCollaborations =
                            state.BoundedContextCollaborations |> addEntry bc c.CollaborationId }
                | Domain d ->
                    { state with DomainCollaborations = state.DomainCollaborations |> addEntry d c.CollaborationId }
                | _ -> state

            state |> addToState c.Initiator |> addToState c.Recipient

        | Namespaces(Namespace.NamespaceAdded e) ->
            { state with
                BoundedContextNamespaces = state.BoundedContextNamespaces |> addEntry e.BoundedContextId e.NamespaceId }
        | Namespaces(Namespace.NamespaceImported e) ->
            { state with
                BoundedContextNamespaces = state.BoundedContextNamespaces |> addEntry e.BoundedContextId e.NamespaceId }
        | Namespaces(Namespace.NamespaceRemoved e) ->
            { state with
                BoundedContextNamespaces =
                    state.BoundedContextNamespaces |> removeEntry e.BoundedContextId e.NamespaceId }
        | _ -> state


    let private triggerDelete logError (store: EventStore) chooseHandler asCommand setItems =
        async {
            match setItems with
            | Some affectedItems ->
                let commands = affectedItems |> Set.map asCommand |> Set.toList

                let eventBasedHandler =
                    CommandHandler.EventBased.eventStoreBasedCommandHandler store

                let handler = chooseHandler eventBasedHandler

                do!
                    CommandHandler.Decider.batch handler commands
                    |> AsyncResult.teeError logError
                    |> AsyncResult.ignore
                    |> AsyncResult.ignoreError
            | None -> ()
        }

    let private handleEvent (logger: ILogger) (store: EventStore) (state: State) event =
        async {
            let logError error =
                logger.LogError("Failed to handle event {Event}: {Error}", event, error)

            match event.Event with
            | BoundedContexts(BoundedContext.BoundedContextRemoved e) ->
                do!
                    state.BoundedContextCollaborations
                    |> Map.tryFind e.BoundedContextId
                    |> triggerDelete
                        logError
                        store
                        FileBasedCommandHandlers.Collaboration.useHandler
                        Collaboration.RemoveConnection

                do!
                    state.BoundedContextNamespaces
                    |> Map.tryFind e.BoundedContextId
                    |> triggerDelete logError store FileBasedCommandHandlers.Namespace.useHandler (fun id ->
                        Namespace.RemoveNamespace(e.BoundedContextId, id))
            | Domains(Domain.DomainRemoved e) ->
                do!
                    state.DomainChildren
                    |> Map.tryFind e.DomainId
                    |> triggerDelete logError store FileBasedCommandHandlers.Domain.useHandler Domain.RemoveDomain

                do!
                    state.BoundedContexts
                    |> Map.tryFind e.DomainId
                    |> triggerDelete
                        logError
                        store
                        FileBasedCommandHandlers.BoundedContext.useHandler
                        BoundedContext.RemoveBoundedContext

                do!
                    state.DomainCollaborations
                    |> Map.tryFind e.DomainId
                    |> triggerDelete
                        logError
                        store
                        FileBasedCommandHandlers.Collaboration.useHandler
                        Collaboration.RemoveConnection
            | _ -> ()
        }

    let reaction (loggerFactory:ILoggerFactory) store =
        let logger = loggerFactory.CreateLogger "CascadeDelete"
        { new Reaction<_,_> with
            member _.Projection = { Update = project; Init = State.Initial }
            member _.Reaction state event = handleEvent logger store state event
        }
    