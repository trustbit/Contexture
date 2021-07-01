namespace Contexture.Api.ReadModels

open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.NamespaceTemplate.Projections
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Projections
open Entities

module Domain =

    open Contexture.Api.Aggregates.Domain

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

    let private boundedContextProjection : Projection<BoundedContext option, Aggregates.BoundedContext.Event> =
        { Init = None
          Update = Projections.asBoundedContext }

    let boundedContextLookup (eventStore: EventStore) : Async<Map<BoundedContextId, BoundedContext>> =
        async {
            let! allStreams = eventStore.AllStreams<Aggregates.BoundedContext.Event>()

            return
                allStreams
                |> List.fold (projectIntoMapBySourceId boundedContextProjection) Map.empty
                |> Map.filter (fun _ v -> Option.isSome v)
                |> Map.map (fun _ v -> Option.get v)
        }

    let allBoundedContexts (eventStore: EventStore) =
        async {
            let! lookup = eventStore |> boundedContextLookup
            return lookup |> Map.toList |> List.map snd
        }

    let boundedContextsByDomainLookup (contexts: BoundedContext list) =
        contexts
        |> List.groupBy (fun l -> l.DomainId)
        |> Map.ofList

    let allBoundedContextsByDomain (eventStore: EventStore) =
        async {
            let! boundedContexts = eventStore |> allBoundedContexts

            let lookup =
                boundedContexts |> boundedContextsByDomainLookup

            return
                fun domainId ->
                    lookup
                    |> Map.tryFind domainId
                    |> Option.defaultValue []
        }

    let buildBoundedContext (eventStore: EventStore) boundedContextId =
        async {
            let! stream = eventStore.Stream boundedContextId
            return stream |> project boundedContextProjection
        }

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

    let private namespacesProjection : Projection<Namespace list, Aggregates.Namespace.Event> =
        { Init = List.empty
          Update = Projections.asNamespaces }

    let private namespaceProjection : Projection<Namespace option, Aggregates.Namespace.Event> =
        { Init = None
          Update = Projections.asNamespace }

    let selectNamespaceId =
        function
        | NamespaceImported e -> e.NamespaceId
        | NamespaceAdded e -> e.NamespaceId
        | NamespaceRemoved e -> e.NamespaceId
        | LabelAdded l -> l.NamespaceId
        | LabelRemoved l -> l.NamespaceId

    let namespaceLookup (eventStore: EventStore) : Async<Map<NamespaceId, Namespace>> =
        async {
            let! allStreams = eventStore.AllStreams<Aggregates.Namespace.Event>()

            return
                allStreams
                |> List.fold (projectIntoMap (fun e -> selectNamespaceId e.Event) namespaceProjection) Map.empty
                |> Map.filter (fun _ v -> Option.isSome v)
                |> Map.map (fun _ v -> Option.get v)
        }

    let allNamespaces (eventStore: EventStore) =
        async {
            let! lookup = eventStore |> namespaceLookup
            return lookup |> Map.toList |> List.map snd
        }

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
