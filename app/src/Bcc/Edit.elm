module Bcc.Edit exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup

import Url
import Url.Parser exposing (Parser, custom)

import Http
import Json.Encode as Encode
import Json.Decode exposing (Decoder, map2, field, string, int, at, nullable)
import Json.Decode.Pipeline as JP


import Route
import Bcc exposing(BoundedContextCanvas)

-- MODEL

type alias Model = 
  { key: Nav.Key
  , self: Url.Url
  , canvas: BoundedContextCanvas
  }

init : Nav.Key -> Url.Url -> (Model, Cmd Msg)
init key url =
  let
    model =
      { key = key
      , self = url
      , canvas = { name = "", description = ""}
      }
  in
    (
      model
    , loadBCC model
    )


-- UPDATE

type FieldMsg
  = SetName String
  | SetDescription String

type Msg
  = Loaded (Result Http.Error BoundedContextCanvas)
  | Field FieldMsg
  | Save
  | Saved (Result Http.Error ())
  | Delete
  | Deleted (Result Http.Error ())
  | Back


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Field fieldMsg ->
      ({ model | canvas = updateFields fieldMsg model.canvas  }, Cmd.none)
    Save -> 
      (model, saveBCC model)
    Saved (Ok _) -> 
      (model, Cmd.none)
    Delete ->
      (model, deleteBCC model)
    Deleted (Ok _) ->
      (model, Route.pushUrl Route.Main model.key)
    Loaded (Ok m) ->
      ({ model | canvas = m }, Cmd.none)    
    Back -> 
      (model, Route.goBack model.key)
    _ ->
      Debug.log ("BCC: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)

updateFields: FieldMsg -> BoundedContextCanvas -> BoundedContextCanvas
updateFields msg canvas =
  case msg of
    SetName name ->
      { canvas | name = name}
      
    SetDescription description ->
      { canvas | description = description}
   
-- VIEW

view : Model -> Html Msg
view model =
    Form.form [Html.Events.onSubmit Save]
        [ viewCanvas model.canvas |> Html.map Field
        , Grid.row []
            [ Grid.col [] 
                [ Form.label [] [ text <| "echo name: " ++ model.canvas.name ]
                , Html.br [] []
                , Form.label [] [ text <| "echo description: " ++ model.canvas.description ]
                , Html.br [] []
                , div []
                  [ Button.button [Button.secondary, Button.onClick Back] [text "Back"]
                  , Button.submitButton [ Button.primary ] [ text "Save"]
                  , Button.button 
                    [ Button.danger
                    , Button.small
                    , Button.onClick Delete
                    , Button.attrs [ title ("Delete " ++ model.canvas.name) ] 
                    ]
                    [ text "X" ]
                  ]
                ]
            ]
        ]


viewCanvas: BoundedContextCanvas -> Html FieldMsg
viewCanvas model =
  Grid.row []
    [ Grid.col []
      [ Form.group []
        [ Form.label [for "name"] [ text "Name"]
        , Input.text [ Input.id "name", Input.value model.name, Input.onInput SetName ] ]
      , Form.group []
        [ Form.label [for "description"] [ text "Description"]
        , Input.text [ Input.id "description", Input.value model.description, Input.onInput SetDescription ] ]
      ]
    ]


-- HTTP

loadBCC: Model -> Cmd Msg
loadBCC model =
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
      , body = Http.jsonBody <| modelEncoder model
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

-- encoders

modelEncoder: Model -> Encode.Value
modelEncoder model = 
  Encode.object
    [ ("name", Encode.string model.canvas.name)
    , ("description", Encode.string model.canvas.description)
    ]

modelDecoder: Decoder BoundedContextCanvas
modelDecoder =
  Json.Decode.succeed BoundedContextCanvas
    |> JP.required "name" string
    |> JP.optional "description" string ""
    