namespace Contexture.Api.Reactions

open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.BoundedContext.Projections
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Subscriptions
open Contexture.Api.Infrastructure.Subscriptions.PositionStorage
open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.AsyncResult
open Microsoft.Extensions.Logging
open Microsoft.FSharp.Control

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



module CascadeDelete =
    open Domain.ValueObjects
    open BoundedContext.ValueObjects
    open Collaboration.ValueObjects

    type State =
        { DomainChildren: Map<DomainId, Set<DomainId>>
          BoundedContexts: Map<DomainId, Set<BoundedContextId>>
          DomainCollaborations: Map<DomainId, Set<CollaborationId>>
          BoundedContextCollaborations: Map<BoundedContextId, Set<CollaborationId>> }

        static member Initial =
            { DomainChildren = Map.empty
              BoundedContexts = Map.empty
              DomainCollaborations = Map.empty
              BoundedContextCollaborations = Map.empty }

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

    let reactWithState logger (store: EventStore) (initialState: State) : SubscriptionHandler<AllEvents> =
        let mutable state = initialState

        fun position events ->
            async {
                do!
                    events
                    |> List.map (handleEvent logger store state)
                    |> Async.Sequential
                    |> Async.Ignore

                state <- List.fold project state (events |> List.map (fun e -> e.Event))
                ()
            }


    let subscribe logger (store: EventStore) (storage: IStorePositions) =
        async {
            let name = "CascadeDelete"
            let! lastProcessedPosition = storage.LastPosition name
            let! _, allData = store.All AllEvents.fromEnvelope

            let initialState =
                allData
                |> List.filter (fun e ->
                    match lastProcessedPosition with
                    | Some processed -> e.Metadata.Position <= processed
                    | None -> true)
                |> List.map (fun e -> e.Event)
                |> List.fold project State.Initial

            let reaction =
                SubscriptionHandler.trackPosition (storage.SavePosition name) (reactWithState logger store initialState)

            let startingPosition =
                lastProcessedPosition |> Option.map From |> Option.defaultValue Start

            let! subscription = store.SubscribeAll AllEvents.fromEnvelope name startingPosition reaction
            return subscription
        }
