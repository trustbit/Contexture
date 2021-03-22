namespace Contexture.Api

open Contexture.Api.Aggregates
open Contexture.Api.Database
open Contexture.Api.Entities
open Contexture.Api.Domains
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Namespaces =
    
    let getNamespaces boundedContextId =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let document = database.Read

            let result =
                document.BoundedContexts.ById boundedContextId
                |> Option.map(fun b -> b.Namespaces |> tryUnbox<Namespace list> |> Option.defaultValue [])
                |> Option.map json
                |> Option.defaultValue (RequestErrors.NOT_FOUND "No namespaces for BoundedContext found")

            result next ctx

    
    let routes boundedContextId: HttpHandler =
        subRouteCi
            "/namespaces"
            (choose [ subRoutef "/%i" (fun namespaceId ->
                          (choose [ ]))
                      GET >=> getNamespaces boundedContextId ])