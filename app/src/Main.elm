module Main exposing (..)

import Browser
import Browser.Navigation as Nav
import Url
import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)
import Bootstrap.CDN as CDN
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Button as Button

import Http
import Json.Encode as Encode
import Json.Decode exposing (Decoder, map2, field, string, int, at)

import Bcc

-- MAIN


main =
  Browser.application 
    { init = init "1234"
    , update = update
    , view = view
    , subscriptions = subscriptions
    , onUrlChange = UrlChanged
    , onUrlRequest = LinkClicked 
    }


-- MODEL

type alias Model = 
  { key : Nav.Key
  , url : Url.Url
  , model: Bcc.Model }


init : Bcc.BoundedContextId -> () -> Url.Url -> Nav.Key -> (Model, Cmd Msg)
init id _ url key =
  let
    (m,cmd) = Bcc.init id
  in
    (
      { key = key
      , url = url
      , model = m }
    , Cmd.map BccMsg cmd
    )


-- UPDATE

type Msg
  = LinkClicked Browser.UrlRequest
  | UrlChanged Url.Url
  | BccMsg Bcc.Msg


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    LinkClicked urlRequest ->
      case urlRequest of
        Browser.Internal url ->
          ( model, Nav.pushUrl model.key (Url.toString url) )

        Browser.External href ->
          ( model, Nav.load href )

    UrlChanged url ->
      ( { model | url = url }
      , Cmd.none
      )
    BccMsg m ->
      let
        (mo, msg2) = Bcc.update m model.model
      in
        ({ model | model = mo},Cmd.map BccMsg msg2)
      
-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions _ =
  Sub.none

-- VIEW


view : Model -> Browser.Document Msg
view model =
  { title = "Bounded Context Wizard"
  , body = 
    [ CDN.stylesheet
    , Bcc.view model.model |> Html.map BccMsg
    ]
  }
