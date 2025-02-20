namespace Contexture.Api.ReadModels.Find

open System
open Contexture.Api.Infrastructure
open Contexture.Api.ReadModels
open Contexture.Api.ReadModels.Find

module SearchFor =
    module NamespaceId =
        open Contexture.Api.Aggregates.Namespace
        open ValueObjects

        [<CLIMutable>]
        type NamespaceQuery =
            { Template: NamespaceTemplateId []
              Name: string [] }
            member this.IsActive =
                this.Name.Length > 0 || this.Template.Length > 0
            static member ValidKeys =
                let dummy = Unchecked.defaultof<NamespaceQuery>
                [ nameof dummy.Template
                  nameof dummy.Name ]

        let findRelevantNamespaces (availableNamespaces: Namespaces.NamespaceFinder) (item: NamespaceQuery) =
                let namespacesByName =
                    item.Name
                    |> SearchArgument.fromInputs
                    |> SearchArgument.executeSearch (Find.Namespaces.byName availableNamespaces)

                let namespacesByTemplate =
                    item.Template
                    |> SearchArgument.fromValues
                    |> SearchArgument.executeSearch (Find.Namespaces.byTemplate availableNamespaces)

                SearchResult.combineResults
                    [ namespacesByName
                      namespacesByTemplate ]

        let find (state:  Namespaces.NamespaceFinder) (byNamespace: NamespaceQuery option) =
            byNamespace
            |> Option.map (findRelevantNamespaces state)
            |> Option.map(SearchResult.map (fun n -> n.NamespaceId))
            |> SearchResult.fromOption

    module Labels =

        [<CLIMutable>]
        type LabelQuery =
            { Name: string []
              Value: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.Value))
            static member ValidKeys =
                let dummy = Unchecked.defaultof<LabelQuery>
                [ nameof dummy.Name
                  nameof dummy.Value ]

        let findRelevantLabels (namespacesByLabel: Labels.State) (item: LabelQuery) =
            let byNameResults =
                item.Name
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.Labels.byLabelName namespacesByLabel)


            let byLabelResults =
                item.Value
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.Labels.byLabelValue namespacesByLabel)

            SearchResult.combineResults [ byNameResults
                                          byLabelResults ]

        let find (state: Labels.State) (byLabel: LabelQuery option) =
            byLabel
            |> Option.map (findRelevantLabels state)
            |> SearchResult.fromOption

    module DomainId =
        [<CLIMutable>]
        type DomainQuery =
            { Name: string []
              ShortName: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.ShortName))
            static member ValidKeys =
                let dummy = Unchecked.defaultof<DomainQuery>
                [ nameof dummy.Name
                  nameof dummy.ShortName ]

        let findRelevantDomains (findDomains: Domains.DomainByShortNameAndNameModel) (query: DomainQuery) =
            let foundByName =
                query.Name
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.Domains.byName findDomains)

            let foundByShortName =
                query.ShortName
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.Domains.byShortName findDomains)

            SearchResult.combineResults [ foundByName
                                          foundByShortName ]


        let find (state: Domains.DomainByShortNameAndNameModel) (query: DomainQuery option) =
            query
            |> Option.map (findRelevantDomains state)
            |> SearchResult.fromOption

    module BoundedContextId =
        [<CLIMutable>]
        type BoundedContextQuery =
            { Name: string []
              ShortName: string [] }
            member this.IsActive =
                not (Seq.isEmpty (Seq.append this.Name this.ShortName))
            static member ValidKeys =
                let dummy = Unchecked.defaultof<BoundedContextQuery>
                [ nameof dummy.Name
                  nameof dummy.ShortName ]

        let findRelevantBoundedContexts
            (findBoundedContext: Find.BoundedContexts.BoundedContextByShortNameAndNameModel)
            (query: BoundedContextQuery)
            =
            let foundByName =
                query.Name
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.BoundedContexts.byName findBoundedContext)

            let foundByShortName =
                query.ShortName
                |> SearchArgument.fromInputs
                |> SearchArgument.executeSearch (Find.BoundedContexts.byShortName findBoundedContext)

            SearchResult.combineResults [ foundByName
                                          foundByShortName ]

        let find (state: BoundedContexts.BoundedContextByShortNameAndNameModel) (query: BoundedContextQuery option) =
            query
            |> Option.map (findRelevantBoundedContexts state)
            |> SearchResult.fromOption
