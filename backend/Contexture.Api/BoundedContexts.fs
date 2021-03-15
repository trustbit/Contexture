namespace Contexture.Api

open System
open Contexture.Api.Database
open Contexture.Api.Domain
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module BoundedContexts =

    let getBoundedContexts =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let document = database.Read
            let boundedContexts = document.BoundedContexts.All

            json boundedContexts next ctx

    let routes: HttpHandler =
        subRouteCi "/boundedcontexts" (
            choose [
                GET >=> getBoundedContexts
            ])
