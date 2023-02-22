namespace Contexture.Api.Apis

open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.Collaboration
open Contexture.Api
open Contexture.Api.FileBasedCommandHandlers
open Contexture.Api.Infrastructure

open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Collaborations =
    module CommandEndpoints =
        open System
        open CommandHandler
        let private updateAndReturnCollaboration command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()
                    let clock = ctx.GetService<Clock>()
                    let eventBasedCommandHandler = CommandHandler.EventBased.eventStoreBasedCommandHandler clock database
                    match! command |> Collaboration.useHandler eventBasedCommandHandler with
                    | Ok (collaborationId,version,_) ->
                        return! redirectTo false (sprintf "/api/collaborations/%O" collaborationId) next ctx
                    | Error (DomainError error) ->
                        return! RequestErrors.BAD_REQUEST (sprintf "Domain Error %A" error) next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let defineRelationship collaborationId (command: DefineRelationship) =
            updateAndReturnCollaboration (DefineRelationship(collaborationId, command))

        let outboundConnection (command: DefineConnection) =
            updateAndReturnCollaboration (DefineOutboundConnection(Guid.NewGuid(),command))

        let inboundConnection (command: DefineConnection) =
            updateAndReturnCollaboration (DefineOutboundConnection(Guid.NewGuid(),command))

        let removeAndReturnId collaborationId =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()
                    let clock = ctx.GetService<Clock>()
                    match! Collaboration.useHandler (EventBased.eventStoreBasedCommandHandler clock database) (RemoveConnection collaborationId) with
                    | Ok (collaborationId,version,_) -> return! json collaborationId next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }
                
    module QueryEndpoints =
        open Contexture.Api.ReadModels
        let getCollaborations =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! collaborationState = ctx.GetService<ReadModels.Collaboration.AllCollaborationsReadModel>().State()
                let collaborations =
                    collaborationState |> Collaboration.activeCollaborations
                    
                return! json collaborations next ctx
            }

        let getCollaboration collaborationId =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! collaborationState = ctx.GetService<ReadModels.Collaboration.AllCollaborationsReadModel>().State()
                let result =
                    collaborationId
                    |> Collaboration.collaboration collaborationState
                    |> Option.map json
                    |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "Collaboration %O not found" collaborationId))

                return! result next ctx
            }

    let routes: HttpHandler =
        subRoute
            "/collaborations"
            (choose [ subRoutef "/%O" (fun collaborationId ->
                          choose [ GET >=> QueryEndpoints.getCollaboration collaborationId
                                   POST
                                   >=> route "/relationship"
                                   >=> bindJson (CommandEndpoints.defineRelationship collaborationId)
                                   DELETE >=> CommandEndpoints.removeAndReturnId collaborationId ])
                      POST
                      >=> route "/outboundConnection"
                      >=> bindJson CommandEndpoints.outboundConnection
                      POST
                      >=> route "/inboundConnection"
                      >=> bindJson CommandEndpoints.inboundConnection
                      GET >=> QueryEndpoints.getCollaborations ])
