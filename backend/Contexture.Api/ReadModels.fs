namespace Contexture.Api.ReadModels


open System
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

    let allBoundedContexts (eventStore: EventStore) =
        eventStore.Get<Aggregates.BoundedContext.Event>()
        |> List.fold (projectIntoMap boundedContextProjection) Map.empty
        |> Map.toList
        |> List.choose snd

    let boundedContextLookup (contexts: BoundedContext list) =
        contexts
        |> List.groupBy (fun l -> l.DomainId)
        |> Map.ofList

    let allBoundedContextsByDomain (eventStore: EventStore) =
        let boundedContexts =
            eventStore
            |> allBoundedContexts
            |> boundedContextLookup

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
          Update = Projections.asNamespace }

    let allNamespaces (eventStore: EventStore) =
        eventStore.Get<Aggregates.Namespace.Event>()
        |> List.fold (projectIntoMap namespacesProjection) Map.empty
        |> Map.toList
        |> List.collect snd

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

    let buildNamespace (eventStore: EventStore) boundedContextId =
        boundedContextId
        |> eventStore.Stream
        |> project namespacesProjection
        
    
    type NamespaceModel = {
        Value: string option
        NamespaceId : NamespaceId
        NamespaceTemplateId : NamespaceTemplateId option
    }

    let append labels (name: string, value) =
        let key = name.ToLowerInvariant()

        labels
        |> Map.change
            key
            (function
            | Some values -> values |> Set.add value |> Some
            | None -> value |> Set.singleton |> Some)

    let remove labels namespaceId =
        labels
        |> Map.map (fun _ (values: Set<NamespaceModel>) -> values |> Set.filter (fun { NamespaceId = n } -> n <> namespaceId))

    
    let projectLabelNameToNamespaceId state eventEnvelope =
        match eventEnvelope.Event with
        | NamespaceAdded n ->
            n.Labels
            |> List.map (fun l -> l.Name, { Value = l.Value; NamespaceId = n.NamespaceId; NamespaceTemplateId = n.NamespaceTemplateId})
            |> List.fold append state
        | NamespaceImported n ->
            n.Labels
            |> List.map (fun l -> l.Name,  { Value = l.Value; NamespaceId = n.NamespaceId; NamespaceTemplateId = n.NamespaceTemplateId})
            |> List.fold append state
        | LabelAdded l -> append state (l.Name, { Value = l.Value; NamespaceId = l.NamespaceId; NamespaceTemplateId = None})
        | LabelRemoved l -> remove state l.NamespaceId
        | NamespaceRemoved n -> remove state n.NamespaceId


    let projectNamespaceIdToBoundedContextId state eventEnvelope =
        match eventEnvelope.Event with
        | NamespaceAdded n -> state |> Map.add n.NamespaceId n.BoundedContextId
        | NamespaceImported n -> state |> Map.add n.NamespaceId n.BoundedContextId
        | NamespaceRemoved n -> state |> Map.remove n.NamespaceId
        | LabelAdded l -> state
        | LabelRemoved l -> state

    type NamespacesByLabel = Map<string, Set<NamespaceModel>>

    let namespacesByLabel (eventStore: EventStore) : NamespacesByLabel =
        eventStore.Get<Aggregates.Namespace.Event>()
        |> List.fold projectLabelNameToNamespaceId Map.empty

    let getByLabelName (labelName: string) namespaces =
        let searchedKey = labelName.ToLowerInvariant()

        namespaces
        |> Map.filter (fun k _ -> k = searchedKey)
        |> Map.toList
        |> List.map snd
        |> Set.unionMany

    let findByLabelName (labelName: string option) namespaces =
        let searchedKey =
            labelName
            |> Option.map (fun o -> o.ToLowerInvariant())

        let matchesKey (key: string) =
            match searchedKey with
            | Some searchTerm -> key.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            | None -> true

        namespaces
        |> Map.filter (fun k _ -> matchesKey k)
        |> Map.toList
        |> List.map snd
        |> Set.unionMany

    let boundedContextByNamespace (eventStore: EventStore) =
        let namespaces =
            eventStore.Get<Aggregates.Namespace.Event>()
            |> List.fold projectNamespaceIdToBoundedContextId Map.empty

        fun (namespaceId: NamespaceId) -> namespaces |> Map.tryFind namespaceId

    let boundedContextsByLabel (eventStore: EventStore) =
        eventStore
        |> allNamespaces
        |> List.collect
            (fun n ->
                n.Labels
                |> List.map (fun l -> l.Name.ToLowerInvariant(), (l.Id, n.Id)))
        |> List.groupBy fst
        |> Map.ofList
        |> Map.map (fun _ v -> v |> List.map snd)


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