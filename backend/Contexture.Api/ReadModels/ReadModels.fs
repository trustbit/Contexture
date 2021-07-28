namespace Contexture.Api.ReadModels

open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.NamespaceTemplate.Projections
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Projections

module Domain =

    open Contexture.Api.Aggregates.Domain
    open ValueObjects

    type AllDomainState =
        { Domains: Map<EventSource, Domain.State>
          Subdomains: Map<DomainId, Domain list> }
        static member Initial =
            { Domains = Map.empty
              Subdomains = Map.empty }

    type AllDomainReadModel = ReadModels.ReadModel<Domain.Event, AllDomainState>

    // ATM we reuse the state projection to reduce effort
    let private domainsProjection : Projection<State, Aggregates.Domain.Event> =
        { Init = State.Initial
          Update = State.evolve }

    let private asOption =
        function
        | Existing s -> Some s
        | _ -> None

    let private getDomains domainState =
        domainState
        |> Map.toList
        |> List.map snd
        |> List.choose asOption

    let private subdomainLookup (domains: Domain list) =
        domains
        |> List.groupBy (fun l -> l.ParentDomainId)
        |> List.choose (fun (key, values) -> key |> Option.map (fun parent -> (parent, values)))
        |> Map.ofList

    let domainsReadModel () : AllDomainReadModel =
        let updateState state eventEnvelopes =
            let domains =
                eventEnvelopes
                |> List.fold (projectIntoMapBySourceId domainsProjection) state.Domains

            { Domains = domains
              // this is brute force ATM - but probably good enough for a while
              Subdomains = domains |> getDomains |> subdomainLookup }

        ReadModels.readModel updateState AllDomainState.Initial

    let allDomains readModel = readModel.Domains |> getDomains

    let subdomainsOf readModel = readModel.Subdomains

    let domain readModel domainId =
        readModel.Domains
        |> Map.tryFind domainId
        |> Option.bind asOption

module BoundedContext =
    open Contexture.Api.Aggregates.BoundedContext
    open ValueObjects
    open Projections


    type BoundedContextState =
        { BoundedContexts: Map<BoundedContextId, BoundedContext option>
          ByDomain: Map<DomainId, BoundedContext list> }
        static member Initial =
            { BoundedContexts = Map.empty
              ByDomain = Map.empty }

    type AllBoundedContextsReadModel = ReadModels.ReadModel<BoundedContext.Event, BoundedContextState>

    let private boundedContextProjection : Projection<BoundedContext option, Aggregates.BoundedContext.Event> =
        { Init = None
          Update = Projections.asBoundedContext }

    let private boundedContextsByDomainLookup (contexts: BoundedContext list) =
        contexts
        |> List.groupBy (fun l -> l.DomainId)
        |> Map.ofList

    let private allContexts state = state |> Map.toList |> List.choose snd

    let boundedContextsReadModel () : AllBoundedContextsReadModel =
        let updateState state eventEnvelopes =
            let contexts =
                eventEnvelopes
                |> List.fold (projectIntoMapBySourceId boundedContextProjection) state.BoundedContexts

            { BoundedContexts = contexts
              // this is brute force ATM - but probably good enough for a while
              ByDomain =
                  contexts
                  |> allContexts
                  |> boundedContextsByDomainLookup }

        ReadModels.readModel updateState BoundedContextState.Initial

    let boundedContextLookup (state: BoundedContextState) =
        state.BoundedContexts
        |> Map.filter (fun _ v -> Option.isSome v)
        |> Map.map (fun _ v -> Option.get v)

    let allBoundedContexts (state: BoundedContextState) = state.BoundedContexts |> allContexts

    let boundedContextsByDomain (state: BoundedContextState) =
        fun domainId ->
            state.ByDomain
            |> Map.tryFind domainId
            |> Option.defaultValue []

    let boundedContext (state: BoundedContextState) boundedContextId =
        state.BoundedContexts
        |> Map.tryFind boundedContextId
        |> Option.flatten


module Collaboration =
    open Contexture.Api.Aggregates.Collaboration

    type CollaborationState = Map<EventSource, State>
    type AllCollaborationsReadModel = ReadModels.ReadModel<Event, CollaborationState>

    // ATM we reuse the state projection to reduce effort
    let private collaborationsProjection : Projection<State, Aggregates.Collaboration.Event> =
        { Init = Initial
          Update = State.evolve }

    let private asOption =
        function
        | Initial -> None
        | Existing e -> Some e
        | Deleted -> None

    let activeCollaborations (state: CollaborationState) =
        state
        |> Map.toList
        |> List.map snd
        |> List.choose asOption

    let collaboration (state: CollaborationState) collaborationId =
        state
        |> Map.tryFind collaborationId
        |> Option.bind asOption

    let collaborationsReadModel () =
        let updateState state eventEnvelopes =
            let collaborations =
                eventEnvelopes
                |> List.fold (projectIntoMapBySourceId collaborationsProjection) state

            collaborations

        ReadModels.readModel updateState Map.empty

module Namespace =
    open Contexture.Api.Aggregates.Namespace
    open ValueObjects

    type NamespaceState =
        { ByNamespaceId: Map<NamespaceId, Projections.Namespace option> }
        static member Empty = { ByNamespaceId = Map.empty }

    type AllNamespacesReadModel = ReadModels.ReadModel<Namespace.Event, NamespaceState>

    let private namespacesProjection : Projection<Projections.Namespace list, Aggregates.Namespace.Event> =
        { Init = List.empty
          Update = Projections.asNamespaces }

    let private namespaceProjection : Projection<Projections.Namespace option, Aggregates.Namespace.Event> =
        { Init = None
          Update = Projections.asNamespace }

    let selectNamespaceId =
        function
        | NamespaceImported e -> e.NamespaceId
        | NamespaceAdded e -> e.NamespaceId
        | NamespaceRemoved e -> e.NamespaceId
        | LabelAdded l -> l.NamespaceId
        | LabelRemoved l -> l.NamespaceId

    let allNamespaces (state: NamespaceState) =
        state.ByNamespaceId
        |> Map.filter (fun _ v -> Option.isSome v)
        |> Map.map (fun _ v -> Option.get v)
        |> Map.toList
        |> List.map snd

    let allNamespacesReadModel () =
        let updateState state eventEnvelopes =
            let namespaces =
                eventEnvelopes
                |> List.fold
                    (projectIntoMap (fun e -> selectNamespaceId e.Event) namespaceProjection)
                    state.ByNamespaceId

            { state with
                  ByNamespaceId = namespaces }

        ReadModels.readModel updateState NamespaceState.Empty

    let namespacesOf (eventStore: EventStore) boundedContextId =
        async {
            let! stream = eventStore.Stream boundedContextId

            return
                stream
                |> List.fold (projectIntoMapBySourceId namespacesProjection) Map.empty
                |> Map.toList
                |> List.collect snd
        }

    let allNamespacesByContext (eventStore: EventStore) =
        async {
            let! namespaces = eventStore.AllStreams<Aggregates.Namespace.Event>()

            let lookup =
                namespaces
                |> List.fold (projectIntoMapBySourceId namespacesProjection) Map.empty

            return
                fun contextId ->
                    lookup
                    |> Map.tryFind contextId
                    |> Option.defaultValue []
        }

    let buildNamespaces (eventStore: EventStore) boundedContextId =
        async {
            let! stream = eventStore.Stream boundedContextId
            return stream |> project namespacesProjection
        }

    module BoundedContexts =
        let private projectNamespaceIdToBoundedContextId state eventEnvelope =
            match eventEnvelope.Event with
            | NamespaceAdded n -> state |> Map.add n.NamespaceId n.BoundedContextId
            | NamespaceImported n -> state |> Map.add n.NamespaceId n.BoundedContextId
            | NamespaceRemoved n -> state |> Map.remove n.NamespaceId
            | LabelAdded l -> state
            | LabelRemoved l -> state

        let byNamespace (eventStore: EventStore) =
            async {
                let! namespaces = eventStore.AllStreams<Aggregates.Namespace.Event>()

                let lookup =
                    namespaces
                    |> List.fold projectNamespaceIdToBoundedContextId Map.empty

                return fun (namespaceId: NamespaceId) -> lookup |> Map.tryFind namespaceId
            }

module Templates =
    open Contexture.Api.Aggregates.NamespaceTemplate

    type TemplateState = Map<EventSource, NamespaceTemplate option>
    type AllTemplatesReadModel = ReadModels.ReadModel<Event, TemplateState>

    let private projection : Projection<NamespaceTemplate option, Aggregates.NamespaceTemplate.Event> =
        { Init = None
          Update = Projections.asTemplate }

    let templatesReadModel () =
        let updateState state eventEnvelopes =
            let templates =
                eventEnvelopes
                |> List.fold (projectIntoMapBySourceId projection) state

            templates

        ReadModels.readModel updateState Map.empty

    let allTemplates (state: TemplateState) = state |> Map.toList |> List.choose snd

    let template (state: TemplateState) templateId =
        state |> Map.tryFind templateId |> Option.flatten
