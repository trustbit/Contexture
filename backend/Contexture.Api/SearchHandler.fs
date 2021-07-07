namespace Contexture.Api

open System
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

 
    module Async =
        
        let map mapper o =
            async {
                let! result = o
                return mapper result
            }
        
        let bindOption o =
            async {
                match o with
                | Some value ->
                    let! v = value
                    return Some v
                | None ->
                    return None
            }
        
        let optionMap mapper o = async {
            let! bound = bindOption o
            return Option.map mapper bound
        }
        
        let optionMap2 mapper o =
            async {
                match o with
                | Some value ->
                    let! result = value
                    return Some (mapper result)
                | None ->
                    return None
            }
    module NamespaceId =
        open Contexture.Api.Aggregates.Namespace
        open ValueObjects

        [<CLIMutable>]
        type NamespaceQuery =
            { Template: NamespaceTemplateId []
              Name: string [] }
            member this.IsActive =
                this.Name.Length > 0 || this.Template.Length > 0

        let findRelevantNamespaces (database: EventStore) (item: NamespaceQuery) = async {
            let! availableNamespaces = Find.namespaces database

            let namespacesByName =
                item.Name
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.Namespaces.byName availableNamespaces)

            let namespacesByTemplate =
                item.Template
                |> SearchArgument.fromValues
                |> SearchArgument.executeSearch (Find.Namespaces.byTemplate availableNamespaces)

            return SearchResult.combineResultsWithAnd
                    [ namespacesByName
                      namespacesByTemplate ]
        }

        let find (database: EventStore) (byNamespace: NamespaceQuery option) = async {
            let! relevantNamespaceIds =
                byNamespace
                |> Option.map (findRelevantNamespaces database)
                |> Async.optionMap (SearchResult.map (fun n -> n.NamespaceId))
                |> Async.map SearchResult.fromOption

            return relevantNamespaceIds
        }
            
    module Labels =
          
        [<CLIMutable>]
        type LabelQuery =
            { Name: string []
              Value: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.Value))
                
        let findRelevantLabels (database: EventStore) (item: LabelQuery) = async {
            let! namespacesByLabel = database |> Find.labels

            let byNameResults =
                item.Name
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.Labels.byLabelName namespacesByLabel)


            let byLabelResults =
                item.Value
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.Labels.byLabelValue namespacesByLabel)

            return SearchResult.combineResultsWithAnd [ byNameResults; byLabelResults]
        }

        let find (database: EventStore) (byLabel: LabelQuery option) = async {
            let! relevantLabels =
                byLabel
                |> Option.map (findRelevantLabels database)
                |> Async.bindOption
                |> Async.map SearchResult.fromOption

            return relevantLabels
        }
            

    module DomainId =
        [<CLIMutable>]
        type DomainQuery =
            { Name: string []
              Key: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.Key))

        let findRelevantDomains (database: EventStore) (query: DomainQuery) = async {
            let! findDomains = database |> Find.domains

            let foundByName =
                query.Name
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.Domains.byName findDomains)

            let foundByKey =
                query.Key
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.Domains.byKey findDomains)

            return
                SearchResult.combineResultsWithAnd
                    [ foundByName
                      foundByKey ]
            }
                
        let find (database: EventStore) (query: DomainQuery option) =
            query
            |> Option.map (findRelevantDomains database)
            |> Async.bindOption
            |> Async.map SearchResult.fromOption

    module BoundedContextId =
        [<CLIMutable>]
        type BoundedContextQuery =
            { Name: string []
              Key: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.Key))

        let findRelevantBoundedContexts (database: EventStore) (query: BoundedContextQuery) = async {
            let! findBoundedContext = database |> Find.boundedContexts

            let foundByName =
                query.Name
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.BoundedContexts.byName findBoundedContext)

            let foundByKey =
                query.Key
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch  (Find.BoundedContexts.byKey findBoundedContext)

            return
                SearchResult.combineResultsWithAnd
                    [ foundByName
                      foundByKey ]
            }

        let find (database: EventStore) (query: BoundedContextQuery option) =
            query
            |> Option.map (findRelevantBoundedContexts database)
            |> Async.bindOption
            |> Async.map SearchResult.fromOption