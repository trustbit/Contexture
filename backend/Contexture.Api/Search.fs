namespace Contexture.Api

open System
open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.BoundedContexts
open Contexture.Api.ReadModels
open Contexture.Api.Domains
open Contexture.Api.Infrastructure
open Microsoft.AspNetCore.Http

open FSharp.Control.Tasks

open Giraffe
open Microsoft.Extensions.Hosting


module Search =
    open Giraffe.ViewEngine

    type Path = string list

    type Asset =
        | Stylesheet of Path
        | JavaScript of Path

    type ResolveAsset = Asset -> XmlNode

    let resolveBase (environment: IHostEnvironment) =
        if environment.IsDevelopment() then "http://localhost:8000/" else "/"

    module Asset =
        let js file = JavaScript [ "js"; file ]
        let css file = Stylesheet [ "css"; file ]

        let resolvePath baseUrl (path: Path) =
            let asString = String.Join("/", path)
            sprintf "%sassets/%s" baseUrl asString


        let stylesheet path = link [ _rel "stylesheet"; _href path ]
        let javascript path = script [ _src path ] []

        let resolveAsset resolvePath asset =
            match asset with
            | Stylesheet path -> stylesheet (resolvePath path)
            | JavaScript path -> javascript (resolvePath path)

    module Views =

        let headTemplate resolveAsset =
            head [] [
                style [] [
                    str "{ padding: 0; margin: 0}"
                ]
                meta [ _charset "UTF-8" ]
                title [] [
                    str "Contexture - Managing your Domains & Contexts"
                ]
                resolveAsset (Asset.css "contexture.css")
                resolveAsset (Asset.js "Contexture.js")
                Asset.stylesheet "https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css"
            ]

        let navTemplate =
            nav [ _class "navbar navbar-expand-sm navbar-dark bg-primary" ] [
                a [ _class "navbar-brand"; _href "/" ] [
                    str "Contexture"
                ]
                button [ _class "navbar-toggler navbar-toggler-right"
                         _type "button" ] [
                    span [ _class "navbar-toggler-icon" ] []
                ]
                div [ _class "collapse navbar-collapse" ] [
                    div [ _style "display: flex; width: 100%;" ] [
                        ul [ _class "navbar-nav mr-auto" ] [
                            li [ _class "navbar-item" ] [ a [_href "/"; _class "nav-link"] [ str "Domains" ]]
                            li [ _class "navbar-item" ] [ a [_href "/search"; _class "nav-link active"] [ str "Search" ]]
                        ]
                    ]
                ]
            ]

        let bodyTemplate content: XmlNode =
            body [] [
                navTemplate
                div [ _class "pt-3" ] [ content ]
            ]

        let documentTemplate (head: XmlNode) (body: XmlNode) = html [] [ head; body ]

        let embedElm name =
            script [ _src (sprintf "/js/%s.js" name) ] []

        let initElm name node flags =
            script [] [
                rawText
                    (sprintf "
  var app = Elm.%s.init({
    node: document.getElementById('%s'),
    flags: %s
  }); "               name node flags)
            ]

        let index resolveAssets flags =
            let searchSnipped =
                div [] [
                    div [ _id "search" ] []
                    initElm "Components.Search" "search" flags
                ]

            documentTemplate (headTemplate resolveAssets) (bodyTemplate searchSnipped)

    let indexHandler: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let baseUrl =
                ctx.GetService<IHostEnvironment>() |> resolveBase

            let pathResolver = Asset.resolvePath baseUrl
            let assetsResolver = Asset.resolveAsset pathResolver

            let eventStore = ctx.GetService<EventStore>()

            let domains = Domain.allDomains eventStore

            let boundedContextsOf =
                BoundedContext.allBoundedContextsByDomain eventStore

            let namespacesOf =
                Namespace.allNamespacesByContext eventStore

            let collaborations =
                Collaboration.allCollaborations eventStore

            let domainResult =
                domains
                |> List.map (fun d ->
                    {| Domain = d
                       BoundedContexts =
                           d.Id
                           |> boundedContextsOf
                           |> List.map (Results.convertBoundedContext namespacesOf) |})

            let result =
                {| Collaboration = collaborations
                   Domains = domainResult
                   BaseUrl = baseUrl |}

            let jsonEncoder = ctx.GetJsonSerializer()

            htmlView (Views.index assetsResolver (jsonEncoder.SerializeToString result)) next ctx

    let routes: HttpHandler =
        subRoute "/search" (choose [ GET >=> indexHandler ])
