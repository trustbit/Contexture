namespace Contexture.Api

open System
open Contexture.Api.Entities
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Infrastructure
open Contexture.Api.ReadModels
open Contexture.Api.ReadModels.Find

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
                |> Option.map SearchResult.combineResults

            let relevantNamespaces =
                combineResultsWithAnd
                    [ namespacesByName
                      namespacesByTemplate ]

            relevantNamespaces      

        let find (database: EventStore) (byNamespace: NamespaceQuery option) =
            let relevantNamespaceIds =
                byNamespace
                |> Option.map (findRelevantNamespaces database)
                |> Option.map (Set.map (fun n -> n.NamespaceId))

            relevantNamespaceIds
            
    module Labels =
          
        [<CLIMutable>]
        type LabelQuery =
            { Name: string []
              Value: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.Value))
                
        let findRelevantLabels (database: EventStore) (item: LabelQuery) =
            let namespacesByLabel = database |> Find.labels

            let byNameResults =
                item.Name
                |> SearchArgument.fromInputs
                |> SearchResult.applyArguments (Find.Labels.byLabelName namespacesByLabel)


            let byLabelResults =
                item.Value
                |> SearchArgument.fromInputs
                |> SearchResult.applyArguments (Find.Labels.byLabelValue namespacesByLabel)

            SearchResult.combineResultsWithAnd [ byNameResults; byLabelResults]

        let find (database: EventStore) (byLabel: LabelQuery option) =
            let relevantLabels =
                byLabel
                |> Option.map (findRelevantLabels database)

            relevantLabels
            

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
                        |> SearchArgument.fromInputs
                        |> SearchResult.applyArguments (Find.Domains.byName findDomains)

                    let foundByKey =
                        item.Key
                        |> SearchArgument.fromInputs
                        |> SearchResult.applyArguments (Find.Domains.byKey findDomains)

                    SearchResult.combineResultsWithAnd
                        [ foundByName
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
