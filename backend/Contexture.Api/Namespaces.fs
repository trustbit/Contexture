namespace Contexture.Api

open System
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.Namespace
open Contexture.Api.Database
open Contexture.Api.Entities
open Contexture.Api.Domains
open Contexture.Api.Infrastructure
open Contexture.Api.Views
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Microsoft.Extensions.Hosting

open Giraffe

module Namespaces =

    let private fetchNamespaces (database: FileBased) boundedContext =
        boundedContext
        |> database.Read.BoundedContexts.ById
        |> Option.map
            (fun b ->
                b.Namespaces
                |> tryUnbox<Namespace list>
                |> Option.defaultValue [])

    module CommandEndpoints =
        open System
        open Namespace
        open FileBasedCommandHandlers

        let clock = fun () -> DateTime.UtcNow

        let private updateAndReturnNamespaces command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()

                    match Namespace.handle clock database command with
                    | Ok updatedContext ->
                        // for namespaces we don't use redirects ATM
                        let boundedContext =
                            updatedContext
                            |> ReadModels.Namespace.namespacesOf database

                        return! json boundedContext next ctx
                    | Error (DomainError error) ->
                        return! RequestErrors.BAD_REQUEST(sprintf "Domain Error %A" error) next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let newNamespace contextId (command: NamespaceDefinition) =
            updateAndReturnNamespaces (NewNamespace(contextId, command))

        let removeNamespace contextId (command: NamespaceId) =
            updateAndReturnNamespaces (RemoveNamespace(contextId, command))

        let removeLabel contextId (command: RemoveLabel) =
            updateAndReturnNamespaces (RemoveLabel(contextId, command))

        let newLabel contextId namespaceId (command: NewLabelDefinition) =
            updateAndReturnNamespaces (AddLabel(contextId, namespaceId, command))

    module QueryEndpoints =

        let getNamespaces boundedContextId =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let database = ctx.GetService<EventStore>()

                let result =
                    boundedContextId
                    |> ReadModels.Namespace.namespacesOf database
                    |> json

                result next ctx


        [<CLIMutable>]
        type LabelQuery =
            { Name: string option
              Value: string option
              NamespaceTemplate: NamespaceTemplateId option }

        let getBoundedContextsByLabel (item: LabelQuery) =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let database = ctx.GetService<EventStore>()

                let namespacesByLabel =
                    database |> ReadModels.Namespace.namespacesByLabel

                let namespaces =
                    namespacesByLabel
                    |> ReadModels.Namespace.findByLabelName item.Name
                    |> Set.filter
                        (fun { NamespaceTemplateId = name } ->
                            match item.NamespaceTemplate with
                            | Some n -> name = Some n
                            | None -> true)
                    |> Set.filter
                        (fun { Value = value } ->
                            match item.Value with
                            | Some searchTerm ->
                                value
                                |> Option.exists (fun v -> v.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                            | None -> true)
                    |> Set.map (fun m -> m.NamespaceId)

                let boundedContextsByNamespace =
                    ReadModels.Namespace.boundedContextByNamespace database

                let boundedContextIds =
                    namespaces
                    |> Set.map (
                        boundedContextsByNamespace
                        >> Option.toList
                        >> Set.ofList
                    )
                    |> Set.unionMany
                    |> Set.toList

                json boundedContextIds next ctx

        let getBoundedContextsWithLabel (name, value) =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let database = ctx.GetService<EventStore>()

                let namespaces =
                    database
                    |> ReadModels.Namespace.namespacesByLabel
                    |> ReadModels.Namespace.getByLabelName name
                    |> Set.filter (fun { Value = v } -> v = Some value)
                    |> Set.map (fun n -> n.NamespaceId)

                let boundedContextsByNamespace =
                    ReadModels.Namespace.boundedContextByNamespace database

                let boundedContextIds =
                    namespaces
                    |> Set.map (
                        boundedContextsByNamespace
                        >> Option.toList
                        >> Set.ofList
                    )
                    |> Set.unionMany
                    |> Set.toList

                json boundedContextIds next ctx

        let getAllNamespaces =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let database = ctx.GetService<EventStore>()

                let namespaces =
                    ReadModels.Namespace.allNamespaces database

                json namespaces next ctx


        let getAllTemplates =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let database = ctx.GetService<EventStore>()

                let namespaces =
                    ReadModels.Templates.allTemplates database

                json namespaces next ctx


    module Views =

        open Layout
        open Giraffe.ViewEngine

        let breadcrumb (domain: Domain) =
            div [ _class "row" ] [
                div [ _class "col" ] [
                    a [ attr "role" "button"
                        _class "btn btn-link"
                        _href $"/domain/{domain.Id}" ] [
                        str $"Back to Domain '{domain.Name}'"
                    ]
                ]
            ]

        let index serialize resolveAssets (boundedContextId: BoundedContextId) (domain: Domain) baseUrl =
            let namespaceSnippet =
                let flags =
                    {| ApiBase = baseUrl
                       BoundedContextId = boundedContextId |}

                div [] [
                    div [ _id "namespaces" ] []
                    initElm serialize "Components.ManageNamespaces" "namespaces" flags
                ]

            let content =
                div [ _class "container" ] [
                    breadcrumb domain
                    namespaceSnippet
                ]

            documentTemplate (headTemplate resolveAssets) (bodyTemplate content)

    let index boundedContextId =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let basePath =
                ctx.GetService<IHostEnvironment>()
                |> BasePaths.resolve

            let pathResolver = Asset.resolvePath basePath.AssetBase
            let assetsResolver = Asset.resolveAsset pathResolver

            let eventStore = ctx.GetService<EventStore>()

            let domainOption =
                boundedContextId
                |> ReadModels.BoundedContext.buildBoundedContext eventStore
                |> Option.map (fun bc -> bc.DomainId)
                |> Option.bind (ReadModels.Domain.buildDomain eventStore)

            match domainOption with
            | Some domain ->
                let jsonEncoder = ctx.GetJsonSerializer()

                let baseApi = basePath.ApiBase + "/api"

                htmlView
                    (Views.index jsonEncoder.SerializeToString assetsResolver boundedContextId domain baseApi)
                    next
                    ctx
            | None -> RequestErrors.NOT_FOUND "Unknown" next ctx

    let routesForBoundedContext boundedContextId : HttpHandler =
        subRouteCi
            "/namespaces"
            (choose [ subRoutef
                          "/%O"
                          (fun namespaceId ->
                              (choose [ subRoute
                                            "/labels"
                                            (choose [ subRoutef
                                                          "/%O"
                                                          (fun labelId ->
                                                              (choose [ DELETE
                                                                        >=> CommandEndpoints.removeLabel
                                                                                boundedContextId
                                                                                { Namespace = namespaceId
                                                                                  Label = labelId } ]))
                                                      POST
                                                      >=> bindJson (
                                                          CommandEndpoints.newLabel boundedContextId namespaceId
                                                      ) ])
                                        DELETE
                                        >=> CommandEndpoints.removeNamespace boundedContextId namespaceId ]))
                      GET
                      >=> QueryEndpoints.getNamespaces boundedContextId
                      POST
                      >=> bindJson (CommandEndpoints.newNamespace boundedContextId) ])

    let routes : HttpHandler =
        subRouteCi
            "/namespaces"
            (choose [ subRoute
                          "/templates"
                          (choose [ GET >=> QueryEndpoints.getAllTemplates

                                     ])
                      GET
                      >=> routef "/boundedContextsWithLabel/%s/%s" QueryEndpoints.getBoundedContextsWithLabel
                      GET
                      >=> route "/boundedContextsWithLabel"
                      >=> bindQuery<QueryEndpoints.LabelQuery> None QueryEndpoints.getBoundedContextsByLabel
                      GET >=> QueryEndpoints.getAllNamespaces ])
