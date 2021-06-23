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

        let getAllNamespaces =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let database = ctx.GetService<EventStore>()

                let namespaces =
                    ReadModels.Namespace.allNamespaces database

                json namespaces next ctx

    module Templates =
        module CommandEndpoints =
            open System
            open NamespaceTemplate
            open FileBasedCommandHandlers

            let clock = fun () -> DateTime.UtcNow

            let private updateAndReturnTemplate command =
                fun (next: HttpFunc) (ctx: HttpContext) ->
                    task {
                        let database = ctx.GetService<EventStore>()

                        match NamespaceTemplate.handle clock database command with
                        | Ok updatedTemplate ->
                            return! redirectTo false (sprintf "/api/namespaces/templates/%O" updatedTemplate) next ctx
                        | Error (DomainError error) ->
                            return! RequestErrors.BAD_REQUEST(sprintf "Template Error %A" error) next ctx
                        | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                    }

            let newTemplate (command: NamespaceDefinition) =
                updateAndReturnTemplate (NewNamespaceTemplate(Guid.NewGuid(), command))

            let removeTemplate (command: NamespaceTemplateId) =
                updateAndReturnTemplate (RemoveTemplate(command))

            let removeLabel templateId labelId =
                updateAndReturnTemplate (RemoveTemplateLabel(templateId, { Label = labelId }))

            let newLabel templateId (command: AddTemplateLabel) =
                updateAndReturnTemplate (AddTemplateLabel(templateId, command))

        module QueryEndpoints =
            let getAllTemplates =
                fun (next: HttpFunc) (ctx: HttpContext) ->
                    let database = ctx.GetService<EventStore>()

                    let namespaces =
                        ReadModels.Templates.allTemplates database

                    json namespaces next ctx

            let getTemplate templateId =
                fun (next: HttpFunc) (ctx: HttpContext) ->
                    let database = ctx.GetService<EventStore>()

                    let template =
                        templateId
                        |> ReadModels.Templates.buildTemplate database
                        |> Option.map json
                        |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "template %O not found" templateId))

                    template next ctx

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
        let routesForOneSpecificLabelOfNamespace namespaceId = 
            fun labelId ->
                choose [
                    DELETE >=> CommandEndpoints.removeLabel
                        boundedContextId
                        { Namespace = namespaceId
                          Label = labelId }
                    RequestErrors.NOT_FOUND "Not found"
                ]
        let routesForOneNamespace =
            fun namespaceId ->
                choose [
                    subRouteCi "/labels"
                        (choose [
                            subRoutef "/%O" (routesForOneSpecificLabelOfNamespace namespaceId)                                              
                            POST >=> bindJson (CommandEndpoints.newLabel boundedContextId namespaceId)
                            RequestErrors.NOT_FOUND "Not found"
                        ])
                    DELETE >=> CommandEndpoints.removeNamespace boundedContextId namespaceId
                    RequestErrors.NOT_FOUND "Not found"
                ]
              
        subRouteCi "/namespaces"
            (choose [
                subRoutef "/%O" routesForOneNamespace
                GET >=> QueryEndpoints.getNamespaces boundedContextId
                POST >=> bindJson (CommandEndpoints.newNamespace boundedContextId)
                RequestErrors.NOT_FOUND "Not found"
            ])

    let routes : HttpHandler =
        subRouteCi "/namespaces"
            (choose [
                subRoute "/templates"
                    (choose [
                        subRoutef "/%O"
                            (fun templateId ->
                                choose [
                                     subRoutef "/labels/%O"
                                         (fun labelId ->
                                            choose [
                                                 DELETE >=> (Templates.CommandEndpoints.removeLabel templateId labelId)
                                                 RequestErrors.NOT_FOUND "Not found"
                                            ])   
                                     POST >=> bindModel None (Templates.CommandEndpoints.newLabel templateId)
                                     GET >=> Templates.QueryEndpoints.getTemplate templateId
                                     DELETE >=> Templates.CommandEndpoints.removeTemplate templateId
                                     RequestErrors.NOT_FOUND "Not found"
                                ]
                            )
                        GET >=> Templates.QueryEndpoints.getAllTemplates
                        POST >=> bindModel None Templates.CommandEndpoints.newTemplate
                        RequestErrors.NOT_FOUND "Not found"
                    ])
                GET >=> QueryEndpoints.getAllNamespaces
                RequestErrors.NOT_FOUND "Not found"
            ])
