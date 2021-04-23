module Components.Search exposing (main)

import Api exposing (boundedContexts)
import Page.Bcc.BoundedContextCard as BoundedContextCard
import BoundedContext.Canvas
import BoundedContext.Namespace as Namespace
import Browser
import ContextMapping.Collaboration as Collaboration exposing (Collaborations)
import Domain exposing (Domain)
import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import Page.Bcc.BoundedContext as BoundedContext
import Url


main =
    Browser.element
        { init = init
        , update = update
        , view = view
        , subscriptions = subscriptions
        }


initModel : Api.Configuration -> Collaboration.Collaborations -> DomainModel -> ( Model, Cmd Msg ) -> ( Model, Cmd Msg )
initModel config collaboration domainModel ( model, cmd ) =
    BoundedContext.init config domainModel.domain domainModel.boundedContexts collaboration
        |> Tuple.mapSecond (Cmd.map BoundedContextMsg)
        |> Tuple.mapSecond (\c -> Cmd.batch [ cmd, c ])
        |> Tuple.mapFirst (\m -> { model | results = m :: model.results })


init : Decode.Value -> ( Model, Cmd Msg )
init flag =
    case flag |> Decode.decodeValue flagsDecoder of
        Ok decoded ->
            decoded.domains
                |> List.foldl (initModel decoded.apiBase decoded.collaboration) ( { results = [] }, Cmd.none )

        Err e ->
            ( { results = [] }, Cmd.none )


type alias DomainModel =
    { domain : Domain
    , boundedContexts : List BoundedContextCard.Item
    }


type alias Flags =
    { collaboration : Collaborations
    , domains : List DomainModel
    , apiBase : Api.Configuration
    }


domainDecoder =
    Decode.map2 DomainModel
        (Decode.field "domain" Domain.domainDecoder)
        (Decode.field "boundedContexts" (Decode.list BoundedContextCard.decoder))


baseConfiguration =
    Decode.string
        |> Decode.andThen
            (\v ->
                case v |> Url.fromString of
                    Just url ->
                        url |> Api.config |> Decode.succeed
                    Nothing ->
                        if not <| String.isEmpty v
                        then v |> Api.baseConfig |> Decode.succeed
                        else Decode.fail <| "Could not decode url from " ++ v
            )


flagsDecoder =
    Decode.map3 Flags
        (Decode.field "collaboration" (Decode.list Collaboration.decoder))
        (Decode.field "domains" (Decode.list domainDecoder))
        (Decode.field "apiBase" baseConfiguration)


type alias Model =
    { results : List BoundedContext.Model
    }


type Msg
    = BoundedContextMsg BoundedContext.Msg


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        BoundedContextMsg m ->
            ( model, Cmd.none )


view : Model -> Html Msg
view model =
    model.results
        |> List.map BoundedContext.view
        |> List.map (Html.map BoundedContextMsg)
        |> div [ class "container" ]


subscriptions : Model -> Sub Msg
subscriptions model =
    Sub.none
