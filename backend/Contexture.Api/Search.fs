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

            let eventStore = ctx.GetService<EventStore>()

            let domains = Domain.allDomains eventStore

            let collaborations =
                Collaboration.allCollaborations eventStore  
                
            let result =
                {| Collaboration = collaborations
                   Domains = domains
                   ApiBase = basePath.ApiBase + "/api"
                   InitialQuery =
                       ctx.Request.Query
                       |> Seq.map (fun q -> {| Name = q.Key; Value = q.Value |> Seq.tryHead |})
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
                        |> List.map(fun l -> l.Name)
                        |> Set.ofList
                    {|
                        Name = value.Name
                        Description = value.Description
                        TemplateId = value.Id
                        Labels =
                            value.Template
                            |> List.map(fun t -> t.Name)
                            |> Set.ofList
                            |> Set.union(labelNames)
                            |> Set.toList
                            |> List.sort
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
                           |> List.map(fun l -> l.Name)
                           |> List.distinct
                           |> List.sort
                    |}
                     )
                
                
            json {| WithTemplate = namespacesTemplates; WithoutTemplate = namespacesTemplates |} next ctx 

    let routes: HttpHandler =
        subRoute "/search" (choose [
            route "/namespaces" >=> getNamespaces 
            GET >=> indexHandler
        ])
