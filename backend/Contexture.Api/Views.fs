namespace Contexture.Api.Views

open System
open Giraffe.ViewEngine

open FSharp.Control.Tasks

type Path = string list

type Asset =
    | Stylesheet of Path
    | JavaScript of Path

type ResolveAsset = Asset -> XmlNode

module Asset =
    
    let js file = JavaScript [ "js"; file ]
    let css file = Stylesheet [ "css"; file ]

    let resolvePath baseUrl (path: Path) =
        let asString = String.Join("/", path)
        sprintf "%s/assets/%s" baseUrl asString


    let stylesheet path = link [ _rel "stylesheet"; _href path ]
    let javascript path = script [ _src path ] []

    let resolveAsset resolvePath asset =
        match asset with
        | Stylesheet path -> stylesheet (resolvePath path)
        | JavaScript path -> javascript (resolvePath path) 

module BasePaths =
    open Microsoft.Extensions.Hosting
    type BasePath =  { AssetBase: string ; ApiBase: string }
    
    let resolve (environment: IHostEnvironment) =
        if environment.IsDevelopment() then
            { AssetBase = "http://localhost:8000"; ApiBase = "http://localhost:5000" }
        else
            { AssetBase = ""; ApiBase = "" }

module Layout =
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
                        li [ _class "navbar-item" ] [
                            a [ _href "/"; _class "nav-link" ] [
                                str "Domains"
                            ]
                        ]
                        li [ _class "navbar-item" ] [
                            a [ _href "/search"
                                _class "nav-link active" ] [
                                str "Search"
                            ]
                        ]
                    ]
                ]
            ]
        ]

    let bodyTemplate content : XmlNode =
        body [] [
            navTemplate
            div [ _class "pt-3" ] [ content ]
        ]

    let documentTemplate (head: XmlNode) (body: XmlNode) = html [] [ head; body ]

    let initElm (serializeFlags: 'flag -> string) name node (flags: 'flag) =
        script [] [
            rawText
                $"
var app = Elm.%s{name}.init({{
    node: document.getElementById('%s{node}'),
    flags: %s{serializeFlags flags}
}}); "
        ]
