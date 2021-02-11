namespace Contexture.Api

open Giraffe

module Domains =
    
    let getDomains ctx =
        negotiate 0 ctx
    
    let routes: HttpHandler =
        subRoute "/domains"
            (choose [
                route "" >=> getDomains
            ])

