module Route exposing (Route(..), parseUrl,pushUrl)

import Browser.Navigation as Nav
import Url exposing (Url)
import Url.Parser exposing (..)

import Bcc exposing (BoundedContextId)


type Route
    = NotFound
    | Main
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
        [ map Main top
        , map Bcc (s "bccs" </> Bcc.idParser)

        ]

pushUrl : Route -> Nav.Key -> Cmd msg
pushUrl route navKey =
    routeToString route
        |> Nav.pushUrl navKey


routeToString : Route -> String
routeToString route =
    case route of
        NotFound ->
            "/not-found"
        Main ->
            "/"
        Bcc bccId ->
            "/bccs/" ++ Bcc.idToString bccId