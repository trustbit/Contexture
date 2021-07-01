namespace Contexture.Api

open System
open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.Namespace
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
        fun (next: HttpFunc) (ctx: HttpContext) -> task {
            let eventStore = ctx.GetService<EventStore>()

            let! allNamespaces =
                ReadModels.Namespace.allNamespaces eventStore
            let! allTemplates =
                eventStore
                |> ReadModels.Templates.allTemplates

            let templateNamespaces =
                allTemplates
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

            let mergeLabels state key value =
                state
                |> Map.change
                    key
                    (Option.map (Set.union value)
                     >> Option.orElse (Some value))

            let namespaces =
                allNamespaces
                |> List.groupBy (fun n -> n.Name.ToLowerInvariant())
                |> List.map
                    (fun (_, namespaces) ->
                        let existingLabels = namespaces |> collectLabelsAndValues

                        let referenceNamespace =
                            namespaces
                            |> List.tryFind (fun n -> Option.isSome n.Template)
                            |> Option.defaultValue (namespaces |> List.head)

                        let namespaceDescription, templateLabels =
                            referenceNamespace.Template
                            |> Option.bind (fun key -> templateNamespaces |> Map.tryFind key)
                            |> Option.map
                                (fun template ->
                                    Some template.Description,
                                    template.Template
                                    |> List.map (fun t -> t.Name, Set.empty)
                                    |> Map.ofList)
                            |> Option.defaultValue (None, Map.empty)

                        {| Name = referenceNamespace.Name
                           Template = referenceNamespace.Template
                           Description = namespaceDescription
                           Labels =
                               Map.fold mergeLabels existingLabels templateLabels
                               |> convertLabelsAndValuesToOutput |})

            return! json namespaces next ctx
        }

    let apiRoutes : HttpHandler =
        subRoute "/search/filter" (choose [ route "/namespaces" >=> getNamespaces ])

    let routes : HttpHandler =
        subRoute "/search" (choose [ GET >=> indexHandler ])
