namespace Contexture.Api

open Contexture.Api.Database
open Contexture.Api.Domain
open Microsoft.AspNetCore.Http

open Giraffe

module Domains =
    
    type DomainResult =
        { Id: int
          ParentDomain: int option
          Key: string option
          Name: string
          Vision: string option
          Subdomains: Domain list
          BoundedContexts: BoundedContext list }
    
    let getDomains =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let domains = database.getDomains()
                            |> List.map(fun x -> { Id = x.Id
                                                   ParentDomain = x.ParentDomain
                                                   Key = x.Key
                                                   Name = x.Name
                                                   Vision = x.Vision
                                                   Subdomains = database.getSubdomains x.Id
                                                   BoundedContexts = database.getBoundedContexts x.Id }
                            )
            
            json domains next ctx
    
    let routes : HttpHandler =
        subRoute "/domains"
            (choose [
                GET >=> getDomains
            ])

