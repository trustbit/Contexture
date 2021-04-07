namespace Contexture.Api

open System
open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.Database
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

    module Asset =
        let js file = JavaScript [ "js"; file ]
        let css file = Stylesheet [ "css"; file ]

        let resolvePath (environment: IHostEnvironment) (path: Path) =
            let asString = String.Join("/", path)

            if environment.IsDevelopment()
            then sprintf "http://localhost:8000/assets/%s" asString
            else sprintf "/assets/%s" asString
            
        let stylesheet path =
             link [ _rel "stylesheet"
                    _href path]
        let javascript path =
            script [ _src path ] []

        let resolveAsset resolvePath asset =
            match asset with
            | Stylesheet path ->
               stylesheet (resolvePath path) 
            | JavaScript path -> javascript (resolvePath path)

    module Views =

        let headTemplate resolveAsset =
            head [] [
                style [] [
                    str "{ padding: 0; margin: 0}"
                ]
                meta [ _charset "UTF-8" ]
                title [] [
                    Text "Contexture - Managing your Domains &amp; Contexts"
                ]
                resolveAsset (Asset.css "contexture.css")
                resolveAsset (Asset.js "Contexture.js")
                Asset.stylesheet "https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css"
            ]

        let navTemplate =
            nav [ _class "navbar navbar-expand-sm navbar-dark bg-primary" ] [
                a [ _class "navbar-brand"; _href "/" ] [
                    Text "Contexture"
                ]
                button [ _class "navbar-toggler navbar-toggler-right"
                         _type "button" ] [
                    span [ _class "navbar-toggler-icon" ] []
                ]
                div [ _class "collapse navbar-collapse" ] [
                    div [ _style "display: flex; width: 100%;" ] [
                        ul [ _class "navbar-nav mr-auto" ] []
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

        let initElm name node =
            script [] [
                rawText
                    (sprintf "
  var app = Elm.%s.init({
    node: document.getElementById('%s'),
    flags: Date.now()
  }); "               name node)
            ]

        let index resolveAssets =
            let searchSnipped =
                div [] [
                    div [ _id "search" ] []
                    initElm "Components.Search" "search"
                ]

            documentTemplate (headTemplate resolveAssets) (bodyTemplate searchSnipped)

    let indexHandler: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let pathResolver =
                Asset.resolvePath (ctx.GetService<IHostEnvironment>())

            let assetsResolver = Asset.resolveAsset pathResolver

            htmlView (Views.index assetsResolver) next ctx

    let routes: HttpHandler =
        subRoute "/reports" (choose [ GET >=> indexHandler ])
