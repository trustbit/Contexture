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
        
        let private updateDomainsIn (document: Document) =
            Result.map(fun (domains,item) ->
                { document with Domains = domains },item
            )

        let create (command: CreateDomain) =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<FileBased>()
                    match newDomain command.Name with
                    | Ok addNewDomain ->
                        let changed =
                            database.Change(fun document ->
                                addNewDomain
                                |> document.Domains.Add
                                |> updateDomainsIn document
                               )
                        match changed with
                        | Ok addedDomain ->
                            return! json (Results.convertDomain addedDomain) next ctx
                        | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                    | Error EmptyName ->
                        return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                }

        let remove domainId =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<FileBased>()
                    let changed =
                        database.Change(fun document ->
                            domainId
                            |> document.Domains.Remove
                            |> updateDomainsIn document
                            )
                    match changed with
                    | Ok (Some removedDomain) -> return! json (Results.convertDomain removedDomain) next ctx
                    | Ok None -> return! json null next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let private updateDomain domainId updateDomain =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<FileBased>()
                    let changed =
                        database.Change (fun document ->
                            domainId
                            |> document.Domains.Update updateDomain
                            |> updateDomainsIn document
                            )
                    match changed with
                    | Ok updatedDomain -> return! json (Results.convertDomain updatedDomain) next ctx
                    | Error (ChangeError EmptyName) ->
                        return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let move domainId (command: MoveDomain) =
            updateDomain domainId (moveDomain command.ParentDomainId)

        let rename domainId (command: RenameDomain) =
            updateDomain domainId (renameDomain command.Name)

        let refineVision domainId (command: RefineVision) =
            updateDomain domainId (refineVisionOfDomain command.Vision)

        let assignKey domainId (command: AssignKey) =
            updateDomain domainId (assignKeyToDomain command.Key)
            
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
