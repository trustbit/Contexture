namespace Contexture.Api

open System
open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.BoundedContexts
open Contexture.Api.Entities
open Contexture.Api.ReadModels
open Contexture.Api.Domains
open Contexture.Api.Infrastructure
open Contexture.Api.Views
open Microsoft.AspNetCore.Http

open FSharp.Control.Tasks

open Giraffe
open Microsoft.Extensions.Hosting


module Search =

    module Views =

        open Layout
        open Giraffe.ViewEngine

        let index jsonEncoder resolveAssets flags =
            let searchSnipped =
                div [] [
                    div [ _id "search" ] []
                    initElm jsonEncoder "EntryPoints.Search" "search" flags
                ]

            documentTemplate (headTemplate resolveAssets) (bodyTemplate searchSnipped)

    let indexHandler : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let basePath =
                ctx.GetService<IHostEnvironment>()
                |> BasePaths.resolve

            let pathResolver = Asset.resolvePath basePath.AssetBase
            let assetsResolver = Asset.resolveAsset pathResolver

            let result =
                {| ApiBase = basePath.ApiBase + "/api"
                   InitialQuery =
                       ctx.Request.Query
                       |> Seq.collect
                           (fun q ->
                               q.Value
                               |> Seq.map (fun value -> {| Name = q.Key; Value = value |})) |}

            let jsonEncoder = ctx.GetJsonSerializer()

            htmlView (Views.index jsonEncoder.SerializeToString assetsResolver result) next ctx

    let getNamespaces : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let eventStore = ctx.GetService<EventStore>()

            let allNamespaces =
                ReadModels.Namespace.allNamespaces eventStore

            let namespacesByTemplateId =
                allNamespaces
                |> List.groupBy (fun n -> n.Template)
                |> Map.ofList

            let templateNamespaces =
                eventStore
                |> ReadModels.Templates.allTemplates
                |> List.map (fun t -> t.Id, t)
                |> Map.ofList
                
            let collectLabelsAndValues namespaces =
                namespaces
                |> List.collect (fun n -> n.Labels)
                |> List.groupBy (fun l -> l.Name)
                |> Map.ofList
                |> Map.map
                    (fun _ labels ->
                        labels
                        |> List.map (fun l -> l.Value)
                        |> Set.ofList)
                    
            let convertLabelsAndValuesToOutput labelsAndValues =
                labelsAndValues
                |> Map.toList
                |> List.map
                   (fun l ->
                       {| Name = fst l
                          Values = l |> snd |> Set.toList |> List.sort |})
                |> List.sortBy (fun l -> l.Name)

            let namespacesTemplates =
                templateNamespaces
                |> Map.toList
                |> List.filter
                    (fun (key, _) ->
                        namespacesByTemplateId
                        |> Map.containsKey (Some key))
                |> List.map
                    (fun (key, value) ->
                        let existingLabels =
                            namespacesByTemplateId
                            |> Map.find (Some key)
                            |> collectLabelsAndValues
                            
                        let templateLabelsWithoutValues =
                            value.Template
                           |> List.map (fun t -> t.Name, Set.empty)
                           |> Map.ofList

                        let mergeLabels state key value =
                            state
                            |> Map.change
                               key
                               (Option.map (Set.union value)
                                >> Option.orElse (Some value))

                        {| Name = value.Name
                           Description = value.Description
                           TemplateId = value.Id
                           Labels =
                               Map.fold mergeLabels existingLabels templateLabelsWithoutValues
                               |> convertLabelsAndValuesToOutput |})
                |> List.sortBy (fun m -> m.Name)

            let namespacesWithoutTemplates =
                namespacesByTemplateId
                |> Map.tryFind None
                |> Option.defaultValue []
                |> List.groupBy (fun n -> n.Name)
                |> List.map
                    (fun (name, namespaces) ->
                        {| Name = name
                           Labels =
                               namespaces
                               |> collectLabelsAndValues
                               |> convertLabelsAndValuesToOutput |})

            json
                {| WithTemplate = namespacesTemplates
                   WithoutTemplate = namespacesWithoutTemplates |}
                next
                ctx

    let apiRoutes : HttpHandler =
        subRoute "/search/filter" (choose [ route "/namespaces" >=> getNamespaces ])

    let routes : HttpHandler =
        subRoute "/search" (choose [ GET >=> indexHandler ])
