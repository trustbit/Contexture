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

    let indexHandler: HttpHandler =
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
                       |> Seq.collect (fun q -> q.Value |> Seq.map (fun value -> {| Name = q.Key; Value = value |}))
                |}

            let jsonEncoder = ctx.GetJsonSerializer()

            htmlView (Views.index jsonEncoder.SerializeToString assetsResolver result) next ctx
            
    let getNamespaces : HttpHandler =
         fun (next: HttpFunc) (ctx: HttpContext) ->
            let eventStore = ctx.GetService<EventStore>()

            let allNamespaces =
                ReadModels.Namespace.allNamespaces eventStore
                
            let byTemplateId =
                allNamespaces
                |> List.groupBy (fun n -> n.Template)
                |> Map.ofList

            let templateNamespaces =
                eventStore
                |> ReadModels.Templates.allTemplates
                |> List.map(fun t -> t.Id,t)
                |> Map.ofList                
            
            let namespacesTemplates =
                templateNamespaces
                |> Map.filter (fun key _ -> byTemplateId |> Map.containsKey (Some key))
                |> Map.map (fun key value ->
                    let labelNames =
                        byTemplateId
                        |> Map.find (Some key)
                        |> List.collect (fun n -> n.Labels)
                        |> List.groupBy(fun l -> l.Name)
                        |> Map.ofList
                        |> Map.map(fun _ labels -> labels |> List.map (fun l -> l.Value) |> Set.ofList)
                    {|
                        Name = value.Name
                        Description = value.Description
                        TemplateId = value.Id
                        Labels =
                            value.Template
                            |> List.map(fun t -> t.Name, Set.empty)
                            |> Map.ofList
                            |> Map.fold(fun s key value ->
                                s |> Map.change key (Option.map(Set.union value) >> Option.orElse (Some value))
                                ) labelNames
                            |> Map.toList
                            |> List.map (fun l -> {| Name = fst l; Values =  l |> snd |> Set.toList |> List.sort |})
                            |> List.sortBy (fun l -> l.Name)
                    |}
                    )
                |> Map.toList
                |> List.sortBy fst
                |> List.map snd
            
            let namespacesWithoutTemplates =
                byTemplateId
                |> Map.tryFind None
                |> Option.defaultValue []
                |> List.groupBy (fun n -> n.Name)
                |> List.map (fun (name,namespaces) ->
                    {|
                       Name = name
                       Labels =
                           namespaces
                            |> List.collect (fun n -> n.Labels)
                            |> List.groupBy(fun l -> l.Name)
                            |> Map.ofList
                            |> Map.map(fun _ labels -> labels |> List.map (fun l -> l.Value) |> Set.ofList)
                            |> Map.toList
                            |> List.map (fun l -> {| Name = fst l; Values =  l |> snd |> Set.toList |> List.sort |})
                            |> List.sort
                    |}
                     )  
                
                
            json {| WithTemplate = namespacesTemplates; WithoutTemplate = namespacesWithoutTemplates |} next ctx 

    let apiRoutes: HttpHandler =
        subRoute "/search/filter" (choose [
            route "/namespaces" >=> getNamespaces 
        ])
    let routes: HttpHandler =
        subRoute "/search" (choose [
            GET >=> indexHandler
        ])
