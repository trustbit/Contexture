module EntryPoints.Search exposing (main)

import Api as Api
import Browser
import Html exposing (Html, div, text)
import Json.Decode as Decode
import Page.Searching.Searching as Searching
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
            Searching.init decoded.apiBase
                |> Tuple.mapFirst Ok

        Err e ->
            ( Err (Decode.errorToString e), Cmd.none )


type alias Flags =
    { apiBase : Api.Configuration
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


flagsDecoder =
    Decode.map Flags
        (Decode.field "apiBase" baseConfiguration)


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
subscriptions model =
    case model of
        Ok model_ ->
            Searching.subscriptions model_

        Err _ ->
            Sub.none


view : Model -> Html Msg
view model =
    case model of
        Ok searching ->
            Searching.view searching

        Err e ->
            text <| "Error on loading: " ++ e
