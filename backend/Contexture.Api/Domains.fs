namespace Contexture.Api

open System
open Contexture.Api.Database
open Contexture.Api.Domain
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Domains =

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
              TechnicalDescription: TechnicalDescription option }
        
        type DomainResult =
            { Id: int
              ParentDomainId: int option
              Key: string option
              Name: string
              Vision: string option
              Subdomains: DomainResult list
              BoundedContexts: BoundedContextResult list }
            
            
        let convertBoundedContext (boundedContext: BoundedContext) =
            { Id = boundedContext.Id
              ParentDomainId = boundedContext.DomainId
              Key= boundedContext.Key
              Name= boundedContext.Name
              Description= boundedContext.Description
              Classification= boundedContext.Classification
              BusinessDecisions = boundedContext. BusinessDecisions
              UbiquitousLanguage = boundedContext. UbiquitousLanguage
              Messages= boundedContext.Messages
              DomainRoles= boundedContext.DomainRoles
              TechnicalDescription= boundedContext.TechnicalDescription
            }

        let convertDomain (domain: Domain) =
            { Id = domain.Id
              ParentDomainId = domain.ParentDomainId
              Key = domain.Key
              Name = domain.Name
              Vision = domain.Vision
              Subdomains = []
              BoundedContexts = [] }

        let includingSubdomainsAndBoundedContexts (database: Document) (domain: Domain) =
            { (domain |> convertDomain) with
                  Subdomains =
                      domain.Id
                      |> Document.subdomainsOf database.Domains
                      |> List.map convertDomain
                  BoundedContexts =
                      domain.Id
                      |> Document.boundedContextsOf database.BoundedContexts
                      |> List.map convertBoundedContext }

   
    module CommandHandler =
        open Aggregates.Domain
        open FileBasedCommandHandlers
        
        let private updateDomainsIn (document: Document) =
            Result.map(fun (domains,item) ->
                { document with Domains = domains },item
            )

        let create (command: CreateDomain) =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<FileBased>()
                    match Domain.handle database (CreateDomain command) with
                    | Ok addedDomain ->
                        let domain =
                            addedDomain
                            |> database.Read.Domains.ById
                            |> Option.get
                            |> Results.convertDomain
                        return! json domain next ctx
                    | Error (InfrastructureError e) ->
                        return! ServerErrors.INTERNAL_ERROR e next ctx
                    | Error (DomainError EmptyName) ->
                        return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                }

        let remove domainId =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<FileBased>()
                    match Domain.handle database (RemoveDomain domainId) with
                    | Ok domainId -> return! json domainId next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let private updateAndReturnDomain command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<FileBased>()
                    match Domain.handle database command with
                    | Ok updatedDomain ->
                        let domain =
                            updatedDomain
                            |> database.Read.Domains.ById
                            |> Option.get
                            |> Results.convertDomain
                        return! json domain next ctx
                    | Error (DomainError EmptyName) ->
                        return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let move domainId (command: MoveDomain) =
            updateAndReturnDomain (MoveDomain(domainId,command))

        let rename domainId (command: RenameDomain) =
            updateAndReturnDomain (RenameDomain(domainId,command))

        let refineVision domainId (command: RefineVision) =
            updateAndReturnDomain (RefineVision(domainId,command))            

        let assignKey domainId (command: AssignKey) =
            updateAndReturnDomain (AssignKey(domainId,command))
            
        let newBoundedContextOn domainId (command: Aggregates.BoundedContext.Commands.CreateBoundedContext) =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let database = ctx.GetService<FileBased>()
                match Aggregates.BoundedContext.newBoundedContext domainId command.Name with
                | Ok addNewBoundedContext ->
                    let changed =
                        database.Change(fun document ->
                            addNewBoundedContext
                            |> document.BoundedContexts.Add
                            |> Result.map(fun (bcs,item) ->
                                { document with BoundedContexts = bcs },item
                            )
                           )
                    match changed with
                    | Ok addedContext ->
                        return! json (Results.convertBoundedContext addedContext) next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                | Error Aggregates.BoundedContext.EmptyName ->
                    return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
            }

    let getDomains =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let document = database.Read
            let domains =
                document.Domains.All
                |> List.map (Results.includingSubdomainsAndBoundedContexts document)

            json domains next ctx

    let getSubDomains domainId =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()

            let domains =
                domainId
                |> Document.subdomainsOf database.Read.Domains 
                |> List.map Results.convertDomain

            json domains next ctx

    let getDomain domainId =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let document = database.Read
            let result =
                domainId
                |> document.Domains.ById
                |> Option.map (Results.includingSubdomainsAndBoundedContexts document)
                |> Option.map json
                |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "Domain %i not found" domainId))

            result next ctx
            
    let getBoundedContextsOf domainId =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let document = database.Read
            let boundedContexts =
                domainId
                |> Document.boundedContextsOf document.BoundedContexts
                |> List.map Results.convertBoundedContext
            
            json boundedContexts next ctx
            

            

    let routes: HttpHandler =
        subRoute
            "/domains"
            (choose [
                subRoutef "/%i" (fun domainId ->
                    (choose [
                        GET
                        >=> route "/domains"
                        >=> getSubDomains domainId
                        GET
                        >=> routeCi "/boundedContexts"
                        >=> getBoundedContextsOf domainId
                        GET
                        >=> getDomain domainId
                        POST
                        >=> route "/rename"
                        >=> bindJson (CommandHandler.rename domainId)
                        POST
                        >=> route "/move"
                        >=> bindJson (CommandHandler.move domainId)
                        POST
                        >=> route "/vision"
                        >=> bindJson (CommandHandler.refineVision domainId)
                        POST
                        >=> route "/key"
                        >=> bindJson (CommandHandler.assignKey domainId)
                        POST
                        >=> routeCi "/boundedContexts"
                        >=> bindJson (CommandHandler.newBoundedContextOn domainId)
                        DELETE >=> CommandHandler.remove domainId
                        ]
                    )
                )
                GET >=> getDomains
                POST >=> bindJson CommandHandler.create ])
