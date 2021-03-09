namespace Contexture.Api

open Microsoft.AspNetCore.Http

open Giraffe

module Collaborations =
    
    let getCollaborations =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let collaborations = Database.getCollaborations()
            json collaborations next ctx
    
    let routes: HttpHandler =
        subRoute "/collaborations"
            (choose [
                GET >=> getCollaborations
            ])

