namespace Contexture.Api

open Contexture.Api.Database
open Contexture.Api.Domain
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Domains =
    
    module Commands =
        type MoveDomain =
            { ParentDomain: int option }
    
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
        
    let toResult (database:FileBased) (domain: Domain) =
        { (domain |> convertDomain) with
            Subdomains = domain.Id |> database.getSubdomains |> List.map convertDomain 
            BoundedContexts = database.getBoundedContexts domain.Id }

    let getDomains =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let domains =
                database.getDomains()
                |> List.map(toResult database)
            
            json domains next ctx

    let getSubDomains domainId =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let domains =
                database.getSubdomains domainId
                |> List.map convertDomain
            
            json domains next ctx

    let getDomain domainId =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let result =
                domainId
                |> database.getDomain
                |> Option.map (toResult database)
                |> Option.map json
                |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "Domain %i not found" domainId))
            result next ctx

    let moveDomain domainId (command: Commands.MoveDomain)=
        fun (next: HttpFunc) (ctx : HttpContext) -> task {
            let database = ctx.GetService<FileBased>()
            database.UpdateDomain domainId (fun domain ->
                { domain with ParentDomain = command.ParentDomain }
                )
            return! json (database.getDomain domainId) next ctx
        }
    
    let routes : HttpHandler =
        subRoute "/domains"
            (choose [
                subRoutef "/%i" (fun domainId ->
                    (choose [
                        GET >=> route "/domains" >=> getSubDomains domainId
                        GET >=> getDomain domainId 
                        POST
                            >=> route "/move"
                            >=> bindJson<Commands.MoveDomain> (moveDomain domainId)
                    ])
                )
                GET >=> getDomains
            ])

