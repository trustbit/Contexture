namespace Contexture.Api

open Contexture.Api.Database
open Microsoft.AspNetCore.Http

open Giraffe

module Collaborations =
    
    let getCollaborations =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let collaborations = database.Read.Collaborations.All
            json collaborations next ctx
    
    let routes : HttpHandler =
        subRoute "/collaborations"
            (choose [
                GET >=> getCollaborations
            ])

