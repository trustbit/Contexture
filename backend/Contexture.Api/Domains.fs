namespace Contexture.Api

open System
open Contexture.Api.Database
open Contexture.Api.Domain
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Domains =

    module Results =

        type DomainResult =
            { Id: int
              DomainId: int option
              Key: string option
              Name: string
              Vision: string option
              Subdomains: DomainResult list
              BoundedContexts: BoundedContext list }

        let convertDomain (domain: Domain) =
            { Id = domain.Id
              DomainId = domain.ParentDomainId
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
                      |> Document.boundedContextsOf database.BoundedContexts }

    module Aggregate =
        type Errors = | EmptyName

        let nameValidation name =
            if String.IsNullOrWhiteSpace name then Error EmptyName else Ok name
            
        let newDomain name =
            name
            |> nameValidation
            |> Result.map (fun name ->
                fun id ->
                    { Id = id
                      Key = None
                      ParentDomainId = None
                      Name = name
                      Vision = None }
            )

        let moveDomain parent (domain: Domain) = Ok { domain with ParentDomainId = parent }

        let refineVisionOfDomain vision (domain: Domain) =
            Ok
                { domain with
                      Vision =
                          vision
                          |> Option.ofObj
                          |> Option.filter (String.IsNullOrWhiteSpace >> not) }

        let renameDomain potentialName (domain: Domain) =
            potentialName
            |> nameValidation
            |> Result.map (fun name -> { domain with Name = name })

        let assignKeyToDomain key (domain: Domain) =
            Ok
                { domain with
                      Key =
                          key
                          |> Option.ofObj
                          |> Option.filter (String.IsNullOrWhiteSpace >> not) }

    module Commands =
        open Aggregate
        type CreateDomain = { Name: string }
        type RenameDomain = { Name: string }
        type MoveDomain = { ParentDomainId: int option }
        type RefineVision = { Vision: string }
        type AssignKey = { Key: string }
        
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
                        >=> bindJson (Commands.rename domainId)
                        POST
                        >=> route "/move"
                        >=> bindJson (Commands.move domainId)
                        POST
                        >=> route "/vision"
                        >=> bindJson (Commands.refineVision domainId)
                        POST
                        >=> route "/key"
                        >=> bindJson (Commands.assignKey domainId)
                        DELETE >=> Commands.remove domainId
                        ]
                    )
                )
                GET >=> getDomains
                POST >=> bindJson Commands.create ])
