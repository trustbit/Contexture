namespace Contexture.Api

open System
open System.Reflection.Metadata
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.Domain
open Contexture.Api.Database
open Contexture.Api.Entities
open Contexture.Api.Domains
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module BoundedContexts =
    open Database

    module Results =
        type BoundedContextResult =
            { Id: int
              ParentDomainId: DomainId
              Key: string option
              Name: string
              Description: string option
              Classification: StrategicClassification
              BusinessDecisions: BusinessDecision list
              UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
              Messages: Messages
              DomainRoles: DomainRole list
              TechnicalDescription: TechnicalDescription option
              Domain: Domain option }

        let convertBoundedContextWithDomain (database: Document) (boundedContext: BoundedContext) =
            { Id = boundedContext.Id
              ParentDomainId = boundedContext.DomainId
              Key = boundedContext.Key
              Name = boundedContext.Name
              Description = boundedContext.Description
              Classification = boundedContext.Classification
              BusinessDecisions = boundedContext.BusinessDecisions
              UbiquitousLanguage = boundedContext.UbiquitousLanguage
              Messages = boundedContext.Messages
              DomainRoles = boundedContext.DomainRoles
              TechnicalDescription = boundedContext.TechnicalDescription
              Domain = database.Domains.ById boundedContext.DomainId }

    module CommandEndpoints =
        open BoundedContext
        open FileBasedCommandHandlers

        let private updateAndReturnBoundedContext command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<FileBased>()

                    match BoundedContext.handle database command with
                    | Ok updatedContext ->
                        let boundedContext =
                            updatedContext
                            |> database.Read.BoundedContexts.ById
                            |> Option.get
                            |> Results.convertBoundedContext

                        return! json boundedContext next ctx
                    | Error (DomainError EmptyName) ->
                        return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let technical contextId (command: UpdateTechnicalInformation) =
            updateAndReturnBoundedContext (UpdateTechnicalInformation(contextId, command))

        let rename contextId (command: RenameBoundedContext) =
            updateAndReturnBoundedContext (RenameBoundedContext(contextId, command))

        let key contextId (command: AssignKey) =
            updateAndReturnBoundedContext (AssignKey(contextId, command))

        let move contextId (command: MoveBoundedContextToDomain) =
            updateAndReturnBoundedContext (MoveBoundedContextToDomain(contextId, command))

        let reclassify contextId (command: ReclassifyBoundedContext) =
            updateAndReturnBoundedContext (ReclassifyBoundedContext(contextId, command))

        let description contextId (command: ChangeDescription) =
            updateAndReturnBoundedContext (ChangeDescription(contextId, command))

        let businessDecisions contextId (command: UpdateBusinessDecisions) =
            updateAndReturnBoundedContext (UpdateBusinessDecisions(contextId, command))

        let ubiquitousLanguage contextId (command: UpdateUbiquitousLanguage) =
            updateAndReturnBoundedContext (UpdateUbiquitousLanguage(contextId, command))

        let domainRoles contextId (command: UpdateDomainRoles) =
            updateAndReturnBoundedContext (UpdateDomainRoles(contextId, command))

        let messages contextId (command: UpdateMessages) =
            updateAndReturnBoundedContext (UpdateMessages(contextId, command))

        let remove contextId =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<FileBased>()

                    match BoundedContext.handle database (RemoveBoundedContext contextId) with
                    | Ok domainId -> return! json domainId next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

    let getBoundedContexts =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let document = database.Read

            let boundedContexts =
                document.BoundedContexts.All
                |> List.map (Results.convertBoundedContextWithDomain database.Read)

            json boundedContexts next ctx

    let getBoundedContext contextId =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let document = database.Read

            let result =
                contextId
                |> document.BoundedContexts.ById
                |> Option.map (Results.convertBoundedContextWithDomain database.Read)
                |> Option.map json
                |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "BoundedContext %i not found" contextId))

            result next ctx

    let routes: HttpHandler =
        subRouteCi
            "/boundedcontexts"
            (choose [ subRoutef "/%i" (fun contextId ->
                          (choose [ GET >=> getBoundedContext contextId
                                    POST
                                    >=> route "/technical"
                                    >=> bindJson (CommandEndpoints.technical contextId)
                                    POST
                                    >=> route "/rename"
                                    >=> bindJson (CommandEndpoints.rename contextId)
                                    POST
                                    >=> route "/key"
                                    >=> bindJson (CommandEndpoints.key contextId)
                                    POST
                                    >=> route "/move"
                                    >=> bindJson (CommandEndpoints.move contextId)
                                    POST
                                    >=> route "/reclassify"
                                    >=> bindJson (CommandEndpoints.reclassify contextId)
                                    POST
                                    >=> route "/description"
                                    >=> bindJson (CommandEndpoints.description contextId)
                                    POST
                                    >=> route "/businessDecisions"
                                    >=> bindJson (CommandEndpoints.businessDecisions contextId)
                                    POST
                                    >=> route "/ubiquitousLanguage"
                                    >=> bindJson (CommandEndpoints.ubiquitousLanguage contextId)
                                    POST
                                    >=> route "/domainRoles"
                                    >=> bindJson (CommandEndpoints.domainRoles contextId)
                                    POST
                                    >=> route "/messages"
                                    >=> bindJson (CommandEndpoints.messages contextId)
                                    DELETE >=> CommandEndpoints.remove contextId ]))
                      GET >=> getBoundedContexts ])
