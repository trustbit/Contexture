module Page.Search exposing (main)
import Browser
import Url
import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Json.Decode as Decode exposing (Decoder)

main =
    Browser.element
      { init = init
      , update = update
      , view = view
      , subscriptions = subscriptions
      }


init : Decode.Value  -> (Model, Cmd Msg)
init flag  =
  let
    -- decode flags
    model =
        { 
        }
  in
    ( model, Cmd.none )


type alias Flags =
    { baseUrl : Maybe Url.Url }

flagsDecoder =
    (Decode.field "baseUrl" Decode.string)
    |> Decode.map Url.fromString
    |> Decode.map Flags

type alias Model =
    { 
    }

type Msg
    = NoOp


update : Msg -> Model ->  ( Model, Cmd Msg )
update msg model =
    case msg of
        NoOp ->
             ( model, Cmd.none )

view : Model -> Html Msg
view model =
    text "Hello world from Elm"


subscriptions : Model -> Sub Msg
subscriptions model =
    Sub.none