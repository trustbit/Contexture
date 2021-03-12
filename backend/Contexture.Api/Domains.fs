namespace Contexture.Api

open Contexture.Api.Database
open Contexture.Api.Domain
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Domains =
    
    module Commands =
        type CreateDomain =
            { Name: string }
        type MoveDomain =
            { ParentDomain: int option }
            
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
               DomainId = domain.ParentDomain
               Key = domain.Key
               Name = domain.Name
               Vision = domain.Vision
               Subdomains = []
               BoundedContexts = [] }
            
        let includingSubdomainsAndBoundedContexts (database:FileBased) (domain: Domain) =
            { (domain |> convertDomain) with
                Subdomains = domain.Id |> database.getSubdomains |> List.map convertDomain 
                BoundedContexts = database.getBoundedContexts domain.Id }

    let getDomains =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let domains =
                database.getDomains()
                |> List.map(Results.includingSubdomainsAndBoundedContexts database)
            
            json domains next ctx

    let getSubDomains domainId =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let domains =
                database.getSubdomains domainId
                |> List.map Results.convertDomain
            
            json domains next ctx

    let getDomain domainId =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let result =
                domainId
                |> database.getDomain
                |> Option.map (Results.includingSubdomainsAndBoundedContexts database)
                |> Option.map json
                |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "Domain %i not found" domainId))
            result next ctx

    let moveDomain domainId (command: Commands.MoveDomain) =
        fun (next: HttpFunc) (ctx : HttpContext) -> task {
            let database = ctx.GetService<FileBased>()
            let updateDomain domain =
                { domain with ParentDomain = command.ParentDomain }
            match database.UpdateDomain domainId updateDomain with
            | Ok updatedDomain ->
                return! json updatedDomain next ctx
            | Error e ->
                return! ServerErrors.INTERNAL_ERROR e next ctx
        }
        
    let createDomain (command: Commands.CreateDomain) =
        fun (next: HttpFunc) (ctx : HttpContext) -> task {
            let database = ctx.GetService<FileBased>()
            match  command.Name |> database.AddDomain with
            | Ok addedDomain ->
                return! json addedDomain next ctx
            | Error e ->
                return! ServerErrors.INTERNAL_ERROR e next ctx
        }
        
    let removeDomain domainId =
        fun (next: HttpFunc) (ctx : HttpContext) -> task {
            let database = ctx.GetService<FileBased>()
            match database.RemoveDomain domainId with
            | Ok (Some removedDomain) -> 
                return! json removedDomain next ctx
            | Ok None ->
                return! json null next ctx
            | Error e ->
                return! ServerErrors.INTERNAL_ERROR e next ctx
        }
    
    let routes : HttpHandler =
        subRoute "/domains"
            (choose [
                subRoutef "/%i" (fun domainId ->
                    (choose [
                        GET >=> route "/domains" >=> getSubDomains domainId
                        GET >=> getDomain domainId 
                        POST >=> route "/move" >=> bindJson (moveDomain domainId)
                        DELETE >=> removeDomain domainId
                    ])
                )
                GET >=> getDomains
                POST >=> bindJson createDomain
                
            ])

