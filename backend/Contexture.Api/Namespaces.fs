namespace Contexture.Api

open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.Namespaces
open Contexture.Api.Database
open Contexture.Api.Entities
open Contexture.Api.Domains
open Contexture.Api.Infrastructure
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Namespaces =

    let private fetchNamespaces (database: FileBased) boundedContext =
        boundedContext
        |> database.Read.BoundedContexts.ById
        |> Option.map (fun b ->
            b.Namespaces
            |> tryUnbox<Namespace list>
            |> Option.defaultValue []
        )

    module CommandEndpoints =
        open System
        open Namespaces
        open FileBasedCommandHandlers
        
        let clock = fun () -> DateTime.UtcNow

        let private updateAndReturnNamespaces command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()

                    match Namespaces.handle clock database command with
                    | Ok updatedContext ->
                        // for namespaces we don't use redirects ATM
                        let boundedContext =
                            updatedContext
                            |> ReadModels.Namespace.allNamespacesOf database 
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

        let newLabel contextId namespaceId (command: NewLabelDefinition) =
            updateAndReturnNamespaces (AddLabel(contextId, namespaceId, command))
            
    module QueryEndpoints = 

        let getNamespaces boundedContextId =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let database = ctx.GetService<FileBased>()
                let result =
                    boundedContextId
                    |> fetchNamespaces database
                    |> Option.map json
                    |> Option.defaultValue (RequestErrors.NOT_FOUND "No namespaces for BoundedContext found")

                result next ctx

    let routes boundedContextId: HttpHandler =
        subRouteCi "/namespaces"
            (choose [
                subRoutef "/%O" (fun namespaceId ->
                    (choose [
                        subRoute "/labels"
                            (choose [
                                subRoutef "/%O" (fun labelId ->
                                    (choose [
                                        DELETE
                                        >=> CommandEndpoints.removeLabel
                                                boundedContextId
                                                { Namespace = namespaceId
                                                  Label = labelId } ]))
                                POST
                                >=> bindJson (CommandEndpoints.newLabel boundedContextId namespaceId) ])
                        DELETE
                        >=> CommandEndpoints.removeNamespace boundedContextId namespaceId ]))
                GET >=> QueryEndpoints.getNamespaces boundedContextId
                POST
                >=> bindJson (CommandEndpoints.newNamespace boundedContextId) ])
