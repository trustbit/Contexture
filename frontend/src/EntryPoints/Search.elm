module EntryPoints.Search exposing (main)

import Api as Api
import Browser
import Components.BoundedContextsOfDomain exposing (Presentation(..))
import ContextMapping.Collaboration as Collaboration exposing (Collaborations)
import Domain exposing (Domain)
import Html exposing (Html, div, text)
import Json.Decode as Decode
import Page.Searching.Filter as Filter
import Page.Searching.Searching as Searching
import Page.Searching.Ports as Searching exposing (SearchResultPresentation(..))
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
            Searching.init decoded.apiBase decoded.initialQuery decoded.presentation
                |> Tuple.mapFirst Ok

        Err e ->
            ( Err (Decode.errorToString e), Cmd.none )


type alias Flags =
    { apiBase : Api.Configuration
    , initialQuery : List Filter.FilterParameter
    , presentation : Maybe SearchResultPresentation
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


presentationDecoder =
    Decode.string
        |> Decode.map Searching.read


flagsDecoder =
    Decode.map3 Flags
        (Decode.field "apiBase" baseConfiguration)
        (Decode.field "initialQuery" (Decode.list queryDecoder))
        (Decode.field "presentation" presentationDecoder)


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
