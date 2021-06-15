namespace Contexture.Api

open System
open Contexture.Api.Entities
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Infrastructure
open Contexture.Api.ReadModels

module SearchFor =

    let combineResultsWithAnd (searchResults: Set<_> option seq) =
        searchResults
        |> Seq.fold
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
            { Name: string []
              Value: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.Value))

        [<CLIMutable>]
        type NamespaceQuery =
            { Template: NamespaceTemplateId []
              Name: string [] }
            member this.IsActive =
                this.Name.Length > 0 || this.Template.Length > 0

        let findRelevantNamespaces (database: EventStore) (item: NamespaceQuery) =
            let availableNamespaces = Find.namespaces database

            let namespacesByName =
                item.Name
                |> Seq.choose Find.SearchPhrase.fromInput
                |> Find.Namespaces.byName availableNamespaces

            let namespacesByTemplate =
                item.Template
                |> Option.ofObj
                |> Option.filter (not << Seq.isEmpty)
                |> Option.map (Seq.map (Find.Namespaces.byTemplate availableNamespaces))
                |> Option.map Set.intersectMany

            let relevantNamespaces =
                combineResultsWithAnd
                    [ namespacesByName
                      namespacesByTemplate ]

            relevantNamespaces

        let findRelevantLabels (database: EventStore) (item: LabelQuery) =
            let namespacesByLabel = database |> Find.labels

            let byName =
                item.Name
                |> Seq.choose Find.SearchPhrase.fromInput
                |> Find.Labels.byLabelName namespacesByLabel


            let searchForLabels =
                namespacesByLabel
                |> Map.toList
                |> List.map snd
                |> Set.unionMany


            let byLabel =
                item.Value
                |> Seq.choose Find.SearchPhrase.fromInput
                |> Option.ofObj
                |> Option.filter (not << Seq.isEmpty)
                |> Option.map
                    (fun searchPhrases ->
                        searchForLabels
                        |> Set.filter
                            (fun { Value = value } ->
                                value
                                |> Option.bind Find.SearchTerm.fromInput
                                |> Option.map
                                    (fun searchTerm ->
                                        searchPhrases
                                        |> Seq.exists (fun phrase -> Find.SearchPhrase.matches phrase searchTerm))
                                |> Option.defaultValue false))

            combineResultsWithAnd [ byName
                                    byLabel ]

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
            { Name: string []
              Key: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.Key))

        let findRelevantDomains (database: EventStore) (query: DomainQuery option) =
            query
            |> Option.map
                (fun item ->
                    let findDomains = database |> Find.domains

                    let foundByName =
                        item.Name
                        |> Seq.choose Find.SearchPhrase.fromInput
                        |> Find.Domains.byName findDomains

                    let foundByKey =
                        item.Key
                        |> Seq.choose Find.SearchPhrase.fromInput
                        |> Find.Domains.byKey findDomains

                    combineResultsWithAnd [ foundByName
                                            foundByKey ])

    module BoundedContextId =
        [<CLIMutable>]
        type BoundedContextQuery =
            { Name: string []
              Key: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.Key))

        let findRelevantBoundedContexts (database: EventStore) (query: BoundedContextQuery option) =
            query
            |> Option.map
                (fun item ->
                    let findBoundedContext = database |> Find.boundedContexts

                    let foundByName =
                        item.Name
                        |> Seq.choose Find.SearchPhrase.fromInput
                        |> Find.BoundedContexts.byName findBoundedContext

                    let foundByKey =
                        item.Key
                        |> Seq.choose Find.SearchPhrase.fromInput
                        |> Find.BoundedContexts.byKey findBoundedContext

                    combineResultsWithAnd
                        [ foundByName
                          foundByKey ])
