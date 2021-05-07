namespace Contexture.Api

open System
open Contexture.Api.Entities
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Infrastructure
open Contexture.Api.ReadModels

module SearchFor =

    let combineResultsWithAnd (searchResults: Set<_> option list) =
        searchResults
        |> List.fold
            (fun ids results ->
                match ids, results with
                | Some existing, Some r -> Set.intersect r existing |> Some
                | None, Some r -> Some r
                | Some existing, None -> Some existing
                | None, None -> None)
            None
        |> Option.defaultValue Set.empty

    module NamespaceId =

        [<CLIMutable>]
        type LabelQuery =
            { Name: string option
              Value: string option }
            member this.IsActive = this.Name.IsSome || this.Value.IsSome

        [<CLIMutable>]
        type NamespaceQuery =
            { Template: NamespaceTemplateId option
              Name: string option }
            member this.IsActive = this.Name.IsSome || this.Template.IsSome

        let findRelevantNamespaces (database: EventStore) (item: NamespaceQuery) =
            let availableNamespaces = Find.namespaces database

            let namespacesByName =
                item.Name
                |> Option.map Find.SearchPhrase.fromInput
                |> Option.map (Find.Namespaces.byName availableNamespaces)

            let namespacesByTemplate =
                item.Template
                |> Option.map (Find.Namespaces.byTemplate availableNamespaces)

            let relevantNamespaces =
                combineResultsWithAnd [ namespacesByName
                                        namespacesByTemplate ]

            relevantNamespaces

        let findRelevantLabels (database: EventStore) (item: LabelQuery) =
            let namespacesByLabel = database |> Find.labels

            namespacesByLabel
            |> Find.Labels.byLabelName (
                item.Name
                |> Option.bind Find.SearchPhrase.fromInput
            )
            |> Set.filter
                (fun { Value = value } ->
                    match item.Value
                          |> Option.bind Find.SearchPhrase.fromInput with
                    | Some searchTerm ->
                        value
                        |> Option.bind Find.SearchTerm.fromInput
                        |> Option.map (Find.SearchPhrase.matches searchTerm)
                        |> Option.defaultValue false
                    | None -> true)

        let find (database: EventStore) (byNamespace: NamespaceQuery option) (byLabel: LabelQuery option) =
            let relevantNamespaceIds =
                byNamespace
                |> Option.map (findRelevantNamespaces database)
                |> Option.map (Set.map (fun n -> n.NamespaceId))

            let relevantLabels =
                byLabel
                |> Option.map (findRelevantLabels database)

            let namespacesIds =
                match relevantNamespaceIds, relevantLabels with
                | Some namespaces, Some labels ->
                    labels
                    |> Set.filter (fun { NamespaceId = namespaceId } -> namespaces.Contains namespaceId)
                    |> Set.map (fun n -> n.NamespaceId)
                    |> Some
                | Some namespaces, None -> Some namespaces
                | None, Some labels -> labels |> Set.map (fun n -> n.NamespaceId) |> Some
                | None, None -> None

            namespacesIds

    module DomainId =
        [<CLIMutable>]
        type DomainQuery =
            { Name: string option
              Key: string option }
            member this.IsActive = this.Name.IsSome || this.Key.IsSome

        let findRelevantDomains (database: EventStore) (query: DomainQuery option) =
            query
            |> Option.map
                (fun item ->
                    let findDomains = database |> Find.domains

                    let foundByName =
                        item.Name
                        |> Option.map Find.SearchPhrase.fromInput
                        |> Option.map (Find.Domains.byName findDomains)

                    let foundByKey =
                        item.Key
                        |> Option.map Find.SearchPhrase.fromInput
                        |> Option.map (Find.Domains.byKey findDomains)

                    combineResultsWithAnd [ foundByName
                                            foundByKey ])

    module BoundedContextId =
        [<CLIMutable>]
        type BoundedContextQuery =
            { Name: string option
              Key: string option }
            member this.IsActive = this.Name.IsSome || this.Key.IsSome

        let findRelevantBoundedContexts (database: EventStore) (query: BoundedContextQuery option) =
            query
            |> Option.map
                (fun item ->
                    let findBoundedContext = database |> Find.boundedContexts

                    let foundByName =
                        item.Name
                        |> Option.map Find.SearchPhrase.fromInput
                        |> Option.map (Find.BoundedContexts.byName findBoundedContext)

                    let foundByKey =
                        item.Key
                        |> Option.map Find.SearchPhrase.fromInput
                        |> Option.map (Find.BoundedContexts.byKey findBoundedContext)

                    combineResultsWithAnd [ foundByName
                                            foundByKey ])
