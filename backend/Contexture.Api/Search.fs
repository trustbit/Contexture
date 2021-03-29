namespace Contexture.Api

open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.Database
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Search =
    open Giraffe.ViewEngine

    module Views =

        let headTemplate =
            head [] [
                style [] [
                    str "{ padding: 0; margin: 0}"
                ]
                meta [ _charset "UTF-8" ]
                title [] [
                    Text "Contexture - Managing your Domains &amp; Contexts"
                ]
                link [ _rel "stylesheet"
                       _href "https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css" ]
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
                rawText (sprintf "
  var app = Elm.%s.init({
    node: document.getElementById('%s'),
    flags: Date.now()
  }); " name node)
            ]
        let index =
            let searchSnipped =
                div []
                    [ embedElm "Page.Search"
                      div [ _id "search" ][]
                      initElm "Page.Search" "search"
                    ]
                
            documentTemplate headTemplate (bodyTemplate searchSnipped)


    let routes: HttpHandler =
        subRoute "/reports" (choose [ GET >=> htmlView Views.index ])
