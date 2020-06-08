module Domain.Edit exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.Checkbox as Checkbox
import Bootstrap.Button as Button

import Json.Encode as Encode
import Json.Decode.Pipeline as JP
import Json.Decode as Decode


import Url
import Http
import Dict

import Route

import Domain
import Bcc.Index

-- MODEL

type alias EditingDomain =
  { domain : Domain.Domain }

type alias Model =
  { key: Nav.Key
  , self: Url.Url
  , edit: EditingDomain
  , contexts : Bcc.Index.Model
  }

init : Nav.Key -> Url.Url -> (Model, Cmd Msg)
init key url =
  let
    domain = Domain.init ()
    (contexts, contextCmd) = Bcc.Index.init url key
    model =
      { key = key
      , self = url
      , edit = { domain = domain }
      , contexts = contexts
      }
  in
    (
      model
    , Cmd.batch [loadDomain model, contextCmd |> Cmd.map BccMsg ]
    )

-- UPDATE

type EditingMsg
  = Field Domain.Msg

type Msg
  = Loaded (Result Http.Error Domain.Domain)
  | Editing EditingMsg
  | Save
  | Saved (Result Http.Error ())
  | Delete
  | Deleted (Result Http.Error ())
  | Back
  | BccMsg Bcc.Index.Msg

updateEdit : EditingMsg -> EditingDomain -> EditingDomain
updateEdit msg model =
  case msg of
    Field fieldMsg ->
      { model | domain = Domain.update fieldMsg model.domain }

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Editing editing ->
      ({ model | edit = updateEdit editing model.edit}, Cmd.none)
    Save ->
      (model, saveBCC model)
    Saved (Ok _) ->
      (model, Cmd.none)
    Delete ->
      (model, deleteBCC model)
    Deleted (Ok _) ->
      (model, Route.pushUrl Route.Home model.key)
    Loaded (Ok m) ->
        ({ model | edit = { domain = m } } , Cmd.none)
    Back ->
      (model, Route.goBack model.key)
    _ ->
      Debug.log ("BCC: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)

-- VIEW

ifValid : (model -> Bool) -> (model -> result) -> (model -> result) -> model -> result
ifValid predicate trueRenderer falseRenderer model =
  if predicate model then
    trueRenderer model
  else
    falseRenderer model

ifNameValid =
  ifValid (\name -> String.length name <= 0)

viewLabel : String -> String -> Html msg
viewLabel labelId caption =
  Form.label [ for labelId] [ Html.h6 [] [ text caption ] ]

view : Model -> Html Msg
view model =
  Grid.containerFluid []
      [ viewDomain model.edit |> Html.map Editing
      , Grid.row []
        [ Grid.col []
          [ Button.button [Button.secondary, Button.onClick Back] [text "Back"]
          , Button.submitButton
            [ Button.primary
            , Button.onClick Save
            , Button.disabled (model.edit.domain.name |> ifNameValid (\_ -> True) (\_ -> False))
            ]
            [ text "Save"]
          , Button.button
            [ Button.danger
            , Button.onClick Delete
            , Button.attrs [ title ("Delete " ++ model.edit.domain.name) ]
            ]
            [ text "Delete" ]
          ]
        ]
      ]

viewDomain : EditingDomain -> Html EditingMsg
viewDomain model =
  div []
    [ Form.group []
      [ viewLabel "name" "Name"
      , Input.text (
        List.concat
        [ [ Input.id "name", Input.value model.domain.name, Input.onInput Domain.SetName ]
        , model.domain.name |> ifNameValid (\_ -> [ Input.danger ]) (\_ -> [])
        ]
      )
      , Form.invalidFeedback [] [ text "A name for the Domain is required!" ]
      ]
    , Html.hr [] []
    , Form.group []
        [ viewLabel "vision" "Vision Statement"
        , Input.text [ Input.id "vision", Input.value model.domain.vision, Input.onInput Domain.SetVision ]
        , Form.help [] [ text "Summary of purpose"] ]
    ]
    |> Html.map Field

-- HTTP

loadDomain: Model -> Cmd Msg
loadDomain model =
  Http.get
    { url = Url.toString model.self
    , expect = Http.expectJson Loaded modelDecoder
    }

saveBCC: Model -> Cmd Msg
saveBCC model =
    Http.request
      { method = "PUT"
      , headers = []
      , url = Url.toString model.self
      , body = Http.jsonBody <| modelEncoder model.edit.domain
      , expect = Http.expectWhatever Saved
      , timeout = Nothing
      , tracker = Nothing
      }

deleteBCC: Model -> Cmd Msg
deleteBCC model =
    Http.request
      { method = "DELETE"
      , headers = []
      , url = Url.toString model.self
      , body = Http.emptyBody
      , expect = Http.expectWhatever Deleted
      , timeout = Nothing
      , tracker = Nothing
      }

modelDecoder : Decode.Decoder Domain.Domain
modelDecoder =
  Decode.succeed Domain.Domain
    |> JP.required "name" Decode.string
    |> JP.optional "vision" Decode.string ""

modelEncoder : Domain.Domain -> Encode.Value
modelEncoder model =
    Encode.object
        [ ("name", Encode.string model.name)
        , ("vision", Encode.string model.vision)
        ]