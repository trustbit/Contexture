namespace Contexture.Api

open Microsoft.AspNetCore.Http

open Giraffe

module Domains =
    
    let getDomains =
        fun (next : HttpFunc) (ctx : HttpContext) ->
//            let domains = Database.getDomains
//                            |> List.map(fun x -> { x with Domains = Database.getSubdomains x.Id })
            
            negotiate 0 next ctx
    
    let routes: HttpHandler =
        subRoute "/domains"
            (choose [
                GET >=> getDomains
            ])

