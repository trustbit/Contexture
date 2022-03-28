port module Page.Bubble.Ports exposing (MoreInfoParameters(..), moreInfoChanged, showHome,showAllConnections, savePresentation, visualizationLoaded, Visualization(..))

import BoundedContext.BoundedContextId exposing (BoundedContextId)
import Domain.DomainId exposing (DomainId)
import Json.Decode as Decode exposing (Error)


type MoreInfoParameters
    = None
    | Domain DomainId
    | SubDomain DomainId DomainId
    | BoundedContext DomainId DomainId BoundedContextId


moreInfoChanged : (Result Error MoreInfoParameters -> msg) -> Sub msg
moreInfoChanged toMsg =
    onMoreInfoChanged (Decode.decodeString decoder >> toMsg)


port onMoreInfoChanged : (String -> msg) -> Sub msg
port showHome : () -> Cmd msg
port showAllConnections : Bool -> Cmd msg

decoder =
    Decode.oneOf
        [ Decode.map3 BoundedContext
            (Decode.field "Domain" Domain.DomainId.idDecoder)
            (Decode.field "SubDomain" Domain.DomainId.idDecoder)
            (Decode.field "BoundedContext" BoundedContext.BoundedContextId.idDecoder)
        , Decode.map2 SubDomain
            (Decode.field "Domain" Domain.DomainId.idDecoder)
            (Decode.field "SubDomain" Domain.DomainId.idDecoder)
        , Decode.map Domain
            (Decode.field "Domain" Domain.DomainId.idDecoder)
        , Decode.succeed None
        ]


port storeVisualization : String -> Cmd msg


port onVisualizationChanged : (String -> msg) -> Sub msg


type Visualization
    = Grid
    | Bubble

savePresentation : Visualization -> Cmd msg
savePresentation visualization =
    visualization
        |> toString
        |> storeVisualization


visualizationLoaded : (Maybe Visualization -> msg) -> Sub msg
visualizationLoaded toMsg =
    onVisualizationChanged (readFromString >> toMsg)


toString visualization =
    case visualization of
        Grid ->
            "Grid"

        Bubble ->
            "Bubble"

readFromString : String -> Maybe Visualization
readFromString s =
    case s |> String.toLower of
        "grid" ->
            Just Grid

        "bubble" ->
            Just Bubble

        _ ->
            Nothing