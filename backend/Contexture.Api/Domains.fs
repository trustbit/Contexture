namespace Contexture.Api

open System
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Database
open Contexture.Api.Entities
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

   
    module CommandEndpoints =
        open Aggregates.Domain
        open FileBasedCommandHandlers
    
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
                
        let createDomain (command: CreateDomain) =
            updateAndReturnDomain (CreateDomain(command))
                
        let createSubDomain domainId (command: CreateDomain) =
            updateAndReturnDomain (CreateSubdomain(domainId,command))

        let move domainId (command: MoveDomain) =
            updateAndReturnDomain (MoveDomain(domainId,command))

        let rename domainId (command: RenameDomain) =
            updateAndReturnDomain (RenameDomain(domainId,command))

        let refineVision domainId (command: RefineVision) =
            updateAndReturnDomain (RefineVision(domainId,command))            

        let assignKey domainId (command: AssignKey) =
            updateAndReturnDomain (AssignKey(domainId,command))
            
        let newBoundedContextOn domainId (command: CreateBoundedContext) =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let database = ctx.GetService<FileBased>()
                match BoundedContext.handle database (CreateBoundedContext (domainId, command)) with
                | Ok addedContext ->
                    let context =
                        addedContext
                        |> database.Read.BoundedContexts.ById
                        |> Option.get
                        |> Results.convertBoundedContext
                    return! json context next ctx
                | Error (DomainError Aggregates.BoundedContext.EmptyName) ->
                    return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
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
                        POST
                        >=> route "/domains"
                        >=> bindJson (CommandEndpoints.createSubDomain domainId)
                        GET
                        >=> routeCi "/boundedContexts"
                        >=> getBoundedContextsOf domainId
                        POST
                        >=> routeCi "/boundedContexts"
                        >=> bindJson (CommandEndpoints.newBoundedContextOn domainId)
                        GET
                        >=> getDomain domainId
                        POST
                        >=> route "/rename"
                        >=> bindJson (CommandEndpoints.rename domainId)
                        POST
                        >=> route "/move"
                        >=> bindJson (CommandEndpoints.move domainId)
                        POST
                        >=> route "/vision"
                        >=> bindJson (CommandEndpoints.refineVision domainId)
                        POST
                        >=> route "/key"
                        >=> bindJson (CommandEndpoints.assignKey domainId)
                       
                        DELETE >=> CommandEndpoints.remove domainId
                       
                        ]
                    )
                )
                GET >=> getDomains
                POST >=> bindJson CommandEndpoints.createDomain ])
