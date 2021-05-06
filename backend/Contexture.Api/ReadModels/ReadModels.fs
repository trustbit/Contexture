namespace Contexture.Api.ReadModels

open Contexture.Api
open Contexture.Api.Aggregates.NamespaceTemplate.Projections
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Projections
open Entities

module Domain =

    open Contexture.Api.Aggregates.Domain

    let private domainsProjection : Projection<Domain option, Aggregates.Domain.Event> =
        { Init = None
          Update = Projections.asDomain }

    let allDomains (eventStore: EventStore) =
        eventStore.Get<Aggregates.Domain.Event>()
        |> List.fold (projectIntoMap domainsProjection) Map.empty
        |> Map.toList
        |> List.choose snd

    let subdomainLookup (domains: Domain list) =
        domains
        |> List.groupBy (fun l -> l.ParentDomainId)
        |> List.choose (fun (key, values) -> key |> Option.map (fun parent -> (parent, values)))
        |> Map.ofList

    let buildDomain (eventStore: EventStore) domainId =
        domainId
        |> eventStore.Stream
        |> project domainsProjection

module BoundedContext =
    open Contexture.Api.Aggregates.BoundedContext

    let private boundedContextProjection : Projection<BoundedContext option, Aggregates.BoundedContext.Event> =
        { Init = None
          Update = Projections.asBoundedContext }

    let boundedContextLookup (eventStore: EventStore) : Map<BoundedContextId, BoundedContext> =
        eventStore.Get<Aggregates.BoundedContext.Event>()
        |> List.fold (projectIntoMap boundedContextProjection) Map.empty
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

    let private collaborationsProjection : Projection<Collaboration option, Aggregates.Collaboration.Event> =
        { Init = None
          Update = Projections.asCollaboration }

    let allCollaborations (eventStore: EventStore) =
        eventStore.Get<Aggregates.Collaboration.Event>()
        |> List.fold (projectIntoMap collaborationsProjection) Map.empty
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

    let namespaceLookup (eventStore: EventStore) : Map<NamespaceId, Namespace> =
        eventStore.Get<Aggregates.Namespace.Event>()
        |> List.fold (projectIntoMap namespaceProjection) Map.empty
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
        |> List.fold (projectIntoMap namespacesProjection) Map.empty
        |> Map.toList
        |> List.collect snd

    let allNamespacesByContext (eventStore: EventStore) =
        let namespaces =
            eventStore.Get<Aggregates.Namespace.Event>()
            |> List.fold (projectIntoMap namespacesProjection) Map.empty

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
        |> List.fold (projectIntoMap projection) Map.empty
        |> Map.toList
        |> List.choose snd

    let buildTemplate (eventStore: EventStore) templateId =
        templateId
        |> eventStore.Stream
        |> project projection
