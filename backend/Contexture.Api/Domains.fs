namespace Contexture.Api

open Microsoft.AspNetCore.Http

open Giraffe

module Domains =
    
    let getDomains =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let domains = Database.getDomains()
                            |> List.map(fun x -> { x with
                                                    Subdomains = Database.getSubdomains x.Id
                                                    BoundedContexts = Database.getBoundedContexts x.Id })
            
            json domains next ctx
    
    let routes: HttpHandler =
        subRoute "/domains"
            (choose [
                GET >=> getDomains
            ])

