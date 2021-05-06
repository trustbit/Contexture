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



    module Find =
        type Operator =
            | Equals
            | StartsWith
            | Contains
            | EndsWith
        
        type SearchPhrase =
            private SearchPhrase of Operator * string
            
            
        type SearchTerm =
            private SearchTerm of string
            
        module SearchTerm =
            let fromInput (term: string) =
                term
                |> Option.ofObj
                |> Option.filter(not<<String.IsNullOrWhiteSpace)
                |> Option.map (fun s -> s.Trim())
                |> Option.map SearchTerm
                
            let value (SearchTerm term) = term
            
        module SearchPhrase =
            
            let private operatorAndPhrase (phrase: string) =
                match phrase.StartsWith "*", phrase.EndsWith "*" with
                | true, true -> // *phrase*
                    Contains, phrase.Trim '*'
                | true, false -> // *phrase
                    EndsWith, phrase.TrimStart '*'
                | false, true -> // phrase*
                    StartsWith, phrase.TrimEnd '*'
                | false, false -> // phrase
                    Equals, phrase
            
            let fromInput (phrase: string) =
                phrase
                |> Option.ofObj
                |> Option.filter(not<<String.IsNullOrWhiteSpace)
                |> Option.map (fun s -> s.Trim())
                |> Option.map operatorAndPhrase
                |> Option.map SearchPhrase
                
            let matches (SearchPhrase (operator, phrase)) (SearchTerm value) =
                match operator with
                | Equals -> String.Equals (phrase, value, StringComparison.OrdinalIgnoreCase)
                | StartsWith -> value.StartsWith (phrase, StringComparison.OrdinalIgnoreCase)
                | EndsWith -> value.EndsWith (phrase, StringComparison.OrdinalIgnoreCase)
                | Contains -> value.Contains (phrase, StringComparison.OrdinalIgnoreCase)
                
        let private appendToSet items (key: string, value) =
            items
            |> Map.change
                key
                (function
                | Some values -> values |> Set.add value |> Some
                | None -> value |> Set.singleton |> Some)

        let private removeFromSet findValue value items =
            items
            |> Map.map
                (fun _ (values: Set<_>) ->
                    values
                    |> Set.filter (fun n -> findValue n <> value))
                
        let private findByKey keyPhrase items =
            let matchesKey (key: string) =
                let term = SearchTerm.fromInput key |> Option.get
                match keyPhrase with
                | Some searchTerm -> SearchPhrase.matches searchTerm term
                | None -> true

            items
            |> Map.filter (fun k _ -> matchesKey k)
            |> Map.toList
            |> List.map snd
            |> Set.unionMany

        module Namespaces =
            type NamespaceModel =
                { NamespaceId: NamespaceId
                  NamespaceTemplateId: NamespaceTemplateId option }

            type NamespaceFinder = Map<string, Set<NamespaceModel>>

            let projectNamespaceNameToNamespaceId state eventEnvelope =
                match eventEnvelope.Event with
                | NamespaceAdded n ->
                    appendToSet
                        state
                        (n.Name,
                         { NamespaceId = n.NamespaceId
                           NamespaceTemplateId = n.NamespaceTemplateId })
                | NamespaceImported n ->
                    appendToSet
                        state
                        (n.Name,
                         { NamespaceId = n.NamespaceId
                           NamespaceTemplateId = n.NamespaceTemplateId })
                | NamespaceRemoved n ->
                    state
                    |> removeFromSet (fun i -> i.NamespaceId) n.NamespaceId
                | LabelAdded l -> state
                | LabelRemoved l -> state

            let byName (namespaces: NamespaceFinder) (name: SearchPhrase option) =
                namespaces
                |> findByKey name

            let byTemplate (namespaces: NamespaceFinder) (templateId: NamespaceTemplateId) =
                namespaces
                |> Map.toList
                |> List.map snd
                |> Set.unionMany
                |> Set.filter (fun m -> m.NamespaceTemplateId = Some templateId)

        let namespaces (eventStore: EventStore) : Namespaces.NamespaceFinder =
            eventStore.Get<Aggregates.Namespace.Event>()
            |> List.fold Namespaces.projectNamespaceNameToNamespaceId Map.empty

        module Labels =
            type LabelAndNamespaceModel =
                { Value: string option
                  NamespaceId: NamespaceId
                  NamespaceTemplateId: NamespaceTemplateId option }

            type NamespacesByLabel = Map<string, Set<LabelAndNamespaceModel>>

            let projectLabelNameToNamespace state eventEnvelope =
                match eventEnvelope.Event with
                | NamespaceAdded n ->
                    n.Labels
                    |> List.map
                        (fun l ->
                            l.Name,
                            { Value = l.Value
                              NamespaceId = n.NamespaceId
                              NamespaceTemplateId = n.NamespaceTemplateId })
                    |> List.fold appendToSet state
                | NamespaceImported n ->
                    n.Labels
                    |> List.map
                        (fun l ->
                            l.Name,
                            { Value = l.Value
                              NamespaceId = n.NamespaceId
                              NamespaceTemplateId = n.NamespaceTemplateId })
                    |> List.fold appendToSet state
                | LabelAdded l ->
                    appendToSet
                        state
                        (l.Name,
                         { Value = l.Value
                           NamespaceId = l.NamespaceId
                           NamespaceTemplateId = None })
                | LabelRemoved l ->
                    state
                    |> removeFromSet (fun n -> n.NamespaceId) l.NamespaceId
                | NamespaceRemoved n ->
                    state
                    |> removeFromSet (fun n -> n.NamespaceId) n.NamespaceId

            let byLabelName (phrase: SearchPhrase option) (namespaces: NamespacesByLabel) =
                namespaces
                |> findByKey phrase

        let labels (eventStore: EventStore) : Labels.NamespacesByLabel =
            eventStore.Get<Aggregates.Namespace.Event>()
            |> List.fold Labels.projectLabelNameToNamespace Map.empty

        module Domains =
            open Contexture.Api.Aggregates.Domain
            type DomainByKeyAndNameModel =
                { ByKey : Map<string,DomainId>
                  ByName: Map<string,Set<DomainId>> }
                with static member Empty = { ByKey = Map.empty; ByName = Map.empty }

            let projectToDomain state eventEnvelope =
                let addKey canBeKey domain byKey =
                    match canBeKey with
                    | Some key -> byKey |> Map.add key domain
                    | None -> byKey
                let append key value items =
                    appendToSet items (key, value)
                match eventEnvelope.Event with
                | SubDomainCreated n ->
                    { state with
                        ByName = state.ByName |> append n.Name n.DomainId }
                | DomainCreated n ->
                    { state with
                        ByName = state.ByName |> append n.Name n.DomainId }
                | KeyAssigned k ->
                    { state with
                        ByKey =
                            state.ByKey
                            |> Map.filter(fun _ v -> v <> k.DomainId)
                            |> addKey k.Key k.DomainId
                    }
                | DomainImported n ->
                    { state with
                        ByName = appendToSet state.ByName (n.Name, n.DomainId)
                        ByKey = state.ByKey |> addKey n.Key n.DomainId
                        }
                | DomainRenamed l ->
                    { state with
                        ByName =
                            state.ByName
                            |> removeFromSet id l.DomainId
                            |> append l.Name l.DomainId
                         }
                | DomainRemoved l ->
                    { state with
                        ByName =
                            state.ByName
                            |> removeFromSet id l.DomainId
                        ByKey =
                            state.ByKey
                            |> Map.filter (fun _ v -> v <> l.DomainId)
                    }
                | CategorizedAsSubdomain _
                | PromotedToDomain _
                | VisionRefined _ ->
                    state

            let byName (phrase: SearchPhrase option) (model: DomainByKeyAndNameModel) =
                model.ByName
                |> findByKey phrase
                
        let domains (eventStore: EventStore) : Domains.DomainByKeyAndNameModel =
            eventStore.Get<Aggregates.Domain.Event>()
            |> List.fold Domains.projectToDomain Domains.DomainByKeyAndNameModel.Empty
        
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
