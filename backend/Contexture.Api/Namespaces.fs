namespace Contexture.Api

open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.Namespaces
open Contexture.Api.Database
open Contexture.Api.Entities
open Contexture.Api.Domains
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Namespaces =

    module CommandEndpoints =
        open Namespaces
        open FileBasedCommandHandlers

        let private updateAndReturnNamespaces command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<FileBased>()

                    match Namespaces.handle database command with
                    | Ok updatedContext ->
                        let boundedContext =
                            updatedContext
                            |> database.Read.BoundedContexts.ById
                            |> Option.map (fun b -> b.Namespaces)
                            |> Option.defaultValue []

                        return! json boundedContext next ctx
                    | Error (DomainError error) ->
                        return! RequestErrors.BAD_REQUEST (sprintf "Domain Error %A" error) next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let newNamespace contextId (command: NamespaceDefinition) =
            updateAndReturnNamespaces (NewNamespace(contextId, command))
            
        let removeNamespace contextId (command: NamespaceId) =
            updateAndReturnNamespaces (RemoveNamespace(contextId, command))
            
        let removeLabel contextId (command: RemoveLabel) =
            updateAndReturnNamespaces (RemoveLabel(contextId, command))

    let getNamespaces boundedContextId =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let document = database.Read

            let result =
                document.BoundedContexts.ById boundedContextId
                |> Option.map (fun b ->
                    b.Namespaces
                    |> tryUnbox<Namespace list>
                    |> Option.defaultValue [])
                |> Option.map json
                |> Option.defaultValue (RequestErrors.NOT_FOUND "No namespaces for BoundedContext found")

            result next ctx


    let routes boundedContextId: HttpHandler =
        subRouteCi
            "/namespaces"
            (choose [
                subRoutef "/%O" (fun namespaceId -> (
                    choose [
                        subRoutef "/labels/%O" (fun labelId -> (
                            choose [
                                DELETE >=> CommandEndpoints.removeLabel boundedContextId { Namespace = namespaceId; Label = labelId }
                            ])
                        )
                        DELETE >=> CommandEndpoints.removeNamespace boundedContextId namespaceId
                    ]))
                GET >=> getNamespaces boundedContextId
                POST
                >=> bindJson (CommandEndpoints.newNamespace boundedContextId) ])
