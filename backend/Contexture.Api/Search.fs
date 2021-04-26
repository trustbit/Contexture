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
                    initElm jsonEncoder "Components.Search" "search" flags
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

            let boundedContextsOf =
                BoundedContext.allBoundedContextsByDomain eventStore

            let namespacesOf =
                Namespace.allNamespacesByContext eventStore

            let collaborations =
                Collaboration.allCollaborations eventStore

            let domainResult =
                domains
                |> List.map (fun d ->
                    {| Domain = d
                       BoundedContexts =
                           d.Id
                           |> boundedContextsOf
                           |> List.map (Results.convertBoundedContext namespacesOf) |})

            let result =
                {| Collaboration = collaborations
                   Domains = domainResult
                   ApiBase = basePath.ApiBase + "/api" |}

            let jsonEncoder = ctx.GetJsonSerializer()

            htmlView (Views.index jsonEncoder.SerializeToString assetsResolver result) next ctx

    let routes: HttpHandler =
        subRoute "/search" (choose [ GET >=> indexHandler ])
