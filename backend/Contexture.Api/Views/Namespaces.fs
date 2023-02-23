module Contexture.Api.Views.Namespaces

open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.BoundedContext.ValueObjects
open Contexture.Api
open Contexture.Api.Infrastructure
open Contexture.Api.ReadModels
open Microsoft.AspNetCore.Http

open Giraffe
open Microsoft.Extensions.Hosting

module Views =
    open Layout
    open Giraffe.ViewEngine

    let breadcrumb (domain: Domain.Domain) =
        div [ _class "row" ] [
            div [ _class "col" ] [
                a [ attr "role" "button"
                    _class "btn btn-link"
                    _href $"/domain/{domain.Id}" ] [
                    str $"Back to Domain '{domain.Name}'"
                ]
            ]
        ]

    let index serialize resolveAssets (boundedContextId: BoundedContextId) (domain: Domain.Domain) baseUrl =
        let namespaceSnippet =
            let flags =
                {| ApiBase = baseUrl
                   BoundedContextId = boundedContextId |}

            div [] [
                div [ _id "namespaces" ] []
                initElm serialize "EntryPoints.ManageNamespaces" "namespaces" flags
            ]

        let content =
            div [ _class "container" ] [
                breadcrumb domain
                namespaceSnippet
            ]

        documentTemplate (headTemplate resolveAssets) (bodyTemplate content)

let index boundedContextId =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let basePath =
                ctx.GetService<IHostEnvironment>()
                |> BasePaths.resolve

            let pathResolver = Asset.resolvePath basePath.AssetBase
            let assetsResolver = Asset.resolveAsset pathResolver

            let eventStore = ctx.GetService<EventStore>()

            let! domainState =
                ctx
                    .GetService<ReadModels.Domain.AllDomainReadModel>()
                    .State()

            let! boundedContextState =
                ctx
                    .GetService<ReadModels.BoundedContext.AllBoundedContextsReadModel>()
                    .State()

            let boundedContext =
                boundedContextId
                |> ReadModels.BoundedContext.boundedContext boundedContextState

            let domainOption =
                boundedContext
                |> Option.map (fun bc -> bc.DomainId)
                |> Option.bind (ReadModels.Domain.domain domainState)

            match domainOption with
            | Some domain ->
                let jsonEncoder = ctx.GetJsonSerializer()

                let baseApi = basePath.ApiBase + "/api"

                return!
                    htmlView
                        (Views.index jsonEncoder.SerializeToString assetsResolver boundedContextId domain baseApi)
                        next
                        ctx
            | None -> return! RequestErrors.NOT_FOUND "Unknown" next ctx
        }

let routes : HttpHandler =
    GET
    >=> routef "/boundedContext/%O/namespaces" index
