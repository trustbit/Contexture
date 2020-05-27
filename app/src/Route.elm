module Route exposing (Route(..), parseUrl,pushUrl,goBack)

import Browser.Navigation as Nav
import Url exposing (Url)
import Url.Parser exposing (..)

import Bcc exposing (BoundedContextId)


type Route
    = NotFound
    | Overview
    | Bcc BoundedContextId


parseUrl : Url -> Route
parseUrl url =
    let 
        _ = Debug.log "URL" url
    in case parse matchRoute url of
        Just route ->
            route

        Nothing ->
            NotFound


matchRoute : Parser (Route -> a) a
matchRoute =
    oneOf
        [ map Overview top
        , map Bcc (s "bccs" </> Bcc.idParser)

        ]

pushUrl : Route -> Nav.Key -> Cmd msg
pushUrl route navKey =
    routeToString route
        |> Nav.pushUrl navKey

goBack : Nav.Key -> Cmd msg
goBack navKey =
    Nav.back navKey 1


routeToString : Route -> String
routeToString route =
    case route of
        NotFound ->
            "/not-found"
        Overview ->
            "/"
        Bcc bccId ->
            "/bccs/" ++ Bcc.idToString bccId