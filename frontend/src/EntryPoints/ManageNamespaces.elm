module EntryPoints.ManageNamespaces exposing (main)


import Api exposing (boundedContexts)
import Browser
import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import Page.Bcc.Edit.Namespaces as Namespaces
import Url
import BoundedContext.BoundedContextId exposing (BoundedContextId)

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
            Namespaces.init decoded.apiBase decoded.boundedContextId
        Err e ->
            Debug.todo ("failed " ++ (Debug.toString e))


type alias Flags =
    { boundedContextId : BoundedContextId
    , apiBase : Api.Configuration
    }


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
    Decode.map2 Flags
        (Decode.field "boundedContextId" BoundedContext.BoundedContextId.idDecoder)
        (Decode.field "apiBase" baseConfiguration)


type alias Model = Namespaces.Model

type alias Msg = Namespaces.Msg

update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    Namespaces.update msg model


view : Model -> Html Msg
view model =
    Namespaces.view model


subscriptions : Model -> Sub Msg
subscriptions model =
    Namespaces.subscriptions model
