module EntryPoints.Search exposing (main)

import Api as Api
import Browser
import ContextMapping.Collaboration as Collaboration exposing (Collaborations)
import Domain exposing (Domain)
import Html exposing (Html, div, text)
import Html.Attributes as Attributes exposing (..)
import Http
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import Page.Search.Filter as Filter
import Page.Search.Searching as Searching
import Url


main =
    Browser.element
        { init = init
        , update = update
        , view = view
        , subscriptions = subscriptions
        }


init : Decode.Value -> ( Model, Cmd Msg )
init flag =
    case flag |> Decode.decodeValue flagsDecoder of
        Ok decoded ->
            Searching.init decoded.apiBase decoded.domains decoded.collaboration decoded.initialQuery
                |> Tuple.mapFirst Ok

        Err e ->
            ( Err (Decode.errorToString e), Cmd.none )


type alias Flags =
    { collaboration : Collaborations
    , domains : List Domain
    , apiBase : Api.Configuration
    , initialQuery : List Filter.FilterParameter
    }


baseConfiguration =
    Decode.string
        |> Decode.andThen
            (\v ->
                case v |> Url.fromString of
                    Just url ->
                        url |> Api.config |> Decode.succeed

                    Nothing ->
                        if not <| String.isEmpty v then
                            v |> Api.baseConfig |> Decode.succeed

                        else
                            Decode.fail <| "Could not decode url from " ++ v
            )


queryDecoder =
    Decode.map2 Filter.FilterParameter
        (Decode.field "name" Decode.string)
        (Decode.field "value" Decode.string)


flagsDecoder =
    Decode.map4 Flags
        (Decode.field "collaboration" (Decode.list Collaboration.decoder))
        (Decode.field "domains" (Decode.list Domain.domainDecoder))
        (Decode.field "apiBase" baseConfiguration)
        (Decode.field "initialQuery" (Decode.list queryDecoder))


type alias Model =
    Result String Searching.Model


type alias Msg =
    Searching.Msg


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case model of
        Ok model_ ->
            Searching.update msg model_
                |> Tuple.mapFirst Result.Ok

        Err _ ->
            ( model, Cmd.none )


subscriptions : Model -> Sub Msg
subscriptions _ =
    Sub.none


view : Model -> Html Msg
view model =
    case model of
        Ok searching ->
            Searching.view searching

        Err e ->
            text <| "Error on loading: " ++ e
