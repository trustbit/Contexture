namespace Contexture.Api.Apis

open System
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Aggregates.Namespace
open Contexture.Api
open Contexture.Api.Infrastructure
open Contexture.Api.ReadModels
open Microsoft.AspNetCore.Http

open Giraffe

module Namespaces =
    open ValueObjects

    module CommandEndpoints =
        open Namespace
        open FileBasedCommandHandlers
        open CommandHandler

        let private updateAndReturnNamespaces command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()
                    let clock = ctx.GetService<Clock>()
                    let eventBasedHandler = EventBased.eventStoreBasedCommandHandler clock database
                    match! Namespace.useHandler eventBasedHandler command with
                    | Ok (updatedContext,version,position) ->
                        let! namespaceState = ctx.GetService<ReadModels.Namespace.AllNamespacesReadModel>().State(position)
                        // for namespaces we don't use redirects ATM
                        let boundedContext =
                            updatedContext
                            |> ReadModels.Namespace.namespacesOf namespaceState

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
        open ReadModels
        let getNamespaces boundedContextId =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! namespaceState = ctx |> State.fetch State.fromReadModel<ReadModels.Namespace.AllNamespacesReadModel>
                let namespaces =
                    boundedContextId
                    |> ReadModels.Namespace.namespacesOf namespaceState
                let result =
                    namespaces
                    |> json

                return! result next ctx
            }

        let getAllNamespaces =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! state = ctx |> State.fetch State.fromReadModel<ReadModels.Namespace.AllNamespacesReadModel>

                let namespaces =
                    ReadModels.Namespace.allNamespaces state

                return! json namespaces next ctx
                }

    module Templates =
        module CommandEndpoints =
            open System
            open NamespaceTemplate
            open FileBasedCommandHandlers
            open CommandHandler
            open ReadModels

            let private updateAndReturnTemplate command =
                fun (next: HttpFunc) (ctx: HttpContext) ->
                    task {
                        let database = ctx.GetService<EventStore>()
                        let clock = ctx.GetService<Clock>()
                        let eventBasedCommandHandler = EventBased.eventStoreBasedCommandHandler clock database 
                        match! NamespaceTemplate.useHandler eventBasedCommandHandler command with
                        | Ok (updatedTemplate,_,position) ->
                            return! redirectTo false (State.appendProcessedPosition (sprintf "/api/namespaces/templates/%O" updatedTemplate) position) next ctx
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
            open ReadModels
            let getAllTemplates =
                fun (next: HttpFunc) (ctx: HttpContext) -> task {
                    let! templateState = ctx |> State.fetch State.fromReadModel<ReadModels.Templates.AllTemplatesReadModel>

                    let templates =
                        ReadModels.Templates.allTemplates templateState

                    return! json templates next ctx
                    }

            let getTemplate templateId =
                fun (next: HttpFunc) (ctx: HttpContext) -> task {
                    let! templateState = ctx |> State.fetch State.fromReadModel<ReadModels.Templates.AllTemplatesReadModel>
                    let template =
                        templateId
                        |> ReadModels.Templates.template templateState 

                    let result =
                        template
                        |> Option.map json
                        |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "template %O not found" templateId))

                    return! result next ctx
                }

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
