module Route exposing (Route(..), parseUrl)

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