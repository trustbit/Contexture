namespace Contexture.Api

open Contexture.Api.Database
open Microsoft.AspNetCore.Http

open Giraffe

module Domains =
    
    let getDomains =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let domains = database.getDomains()
                            |> List.map(fun x -> { x with
                                                    Subdomains = database.getSubdomains x.Id
                                                    BoundedContexts = database.getBoundedContexts x.Id })
            
            json domains next ctx
    
    let routes : HttpHandler =
        subRoute "/domains"
            (choose [
                GET >=> getDomains
            ])

