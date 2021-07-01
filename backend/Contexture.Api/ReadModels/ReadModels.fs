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

    type AllDomainReadModel = ReadModels.ReadModel<Domain.Event, AllDomainState>

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

    let boundedContextLookup (eventStore: EventStore) : Map<BoundedContextId, BoundedContext> =
        eventStore.Get<Aggregates.BoundedContext.Event>()
        |> List.fold (projectIntoMapBySourceId boundedContextProjection) Map.empty
        |> Map.filter (fun _ v -> Option.isSome v)
        |> Map.map (fun _ v -> Option.get v)

    let allBoundedContexts (eventStore: EventStore) =
        eventStore
        |> boundedContextLookup
        |> Map.toList
        |> List.map snd

    let boundedContextsByDomainLookup (contexts: BoundedContext list) =
        contexts
        |> List.groupBy (fun l -> l.DomainId)
        |> Map.ofList

    let allBoundedContextsByDomain (eventStore: EventStore) =
        let boundedContexts =
            eventStore
            |> allBoundedContexts
            |> boundedContextsByDomainLookup

        fun domainId ->
            boundedContexts
            |> Map.tryFind domainId
            |> Option.defaultValue []

    let buildBoundedContext (eventStore: EventStore) boundedContextId =
        boundedContextId
        |> eventStore.Stream
        |> project boundedContextProjection

module Collaboration =
    open Contexture.Api.Aggregates.Collaboration

    let private collaborationsProjection : Projection<Projections.Collaboration option, Aggregates.Collaboration.Event> =
        { Init = None
          Update = Projections.asCollaboration }

    let allCollaborations (eventStore: EventStore) =
        eventStore.Get<Aggregates.Collaboration.Event>()
        |> List.fold (projectIntoMapBySourceId collaborationsProjection) Map.empty
        |> Map.toList
        |> List.choose snd

    let buildCollaboration (eventStore: EventStore) collaborationId =
        collaborationId
        |> eventStore.Stream
        |> project collaborationsProjection

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

    let namespaceLookup (eventStore: EventStore) : Map<NamespaceId, Namespace> =
        eventStore.Get<Aggregates.Namespace.Event>()
        |> List.fold (projectIntoMap (fun e -> selectNamespaceId e.Event) namespaceProjection) Map.empty
        |> Map.filter (fun _ v -> Option.isSome v)
        |> Map.map (fun _ v -> Option.get v)

    let allNamespaces (eventStore: EventStore) =
        eventStore
        |> namespaceLookup
        |> Map.toList
        |> List.map snd

    let namespacesOf (eventStore: EventStore) boundedContextId =
        boundedContextId
        |> eventStore.Stream
        |> List.fold (projectIntoMapBySourceId namespacesProjection) Map.empty
        |> Map.toList
        |> List.collect snd

    let allNamespacesByContext (eventStore: EventStore) =
        let namespaces =
            eventStore.Get<Aggregates.Namespace.Event>()
            |> List.fold (projectIntoMapBySourceId namespacesProjection) Map.empty

        fun contextId ->
            namespaces
            |> Map.tryFind contextId
            |> Option.defaultValue []

    let buildNamespaces (eventStore: EventStore) boundedContextId =
        boundedContextId
        |> eventStore.Stream
        |> project namespacesProjection

    module BoundedContexts =
        let private projectNamespaceIdToBoundedContextId state eventEnvelope =
            match eventEnvelope.Event with
            | NamespaceAdded n -> state |> Map.add n.NamespaceId n.BoundedContextId
            | NamespaceImported n -> state |> Map.add n.NamespaceId n.BoundedContextId
            | NamespaceRemoved n -> state |> Map.remove n.NamespaceId
            | LabelAdded l -> state
            | LabelRemoved l -> state

        let byNamespace (eventStore: EventStore) =
            let namespaces =
                eventStore.Get<Aggregates.Namespace.Event>()
                |> List.fold projectNamespaceIdToBoundedContextId Map.empty

            fun (namespaceId: NamespaceId) -> namespaces |> Map.tryFind namespaceId

module Templates =
    open Contexture.Api.Aggregates.NamespaceTemplate

    let private projection : Projection<NamespaceTemplate option, Aggregates.NamespaceTemplate.Event> =
        { Init = None
          Update = Projections.asTemplate }

    let allTemplates (eventStore: EventStore) =
        eventStore.Get<Aggregates.NamespaceTemplate.Event>()
        |> List.fold (projectIntoMapBySourceId projection) Map.empty
        |> Map.toList
        |> List.choose snd

    let buildTemplate (eventStore: EventStore) templateId =
        templateId
        |> eventStore.Stream
        |> project projection
