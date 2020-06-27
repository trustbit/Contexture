module Route exposing (Route(..), parseUrl,pushUrl,goBack,routeToString)

import Browser.Navigation as Nav
import Url exposing (Url)
import Url.Parser exposing (..)

import Domain exposing(DomainId)
import BoundedContext exposing (BoundedContextId)


type Route
    = NotFound
    | Home
    | Domain DomainId
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
        [ map Home top
        , map Domain (s "domain" </> Domain.idParser)
        , map Bcc (s "bccs" </> BoundedContext.idParser)
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
        Home ->
            "/"
        Domain id ->
            "/domain/" ++ Domain.idToString id
        Bcc bccId ->
            "/bccs/" ++ BoundedContext.idToString bccId