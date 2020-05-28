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
import Bootstrap.Form.Radio as Radio
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup

import Url

import Http
import Json.Encode as Encode
import Json.Decode exposing (Decoder, map2, field, string, int, at, nullable)
import Json.Decode.Pipeline as JP


import Route
import Bcc

-- MODEL

type alias Model = 
  { key: Nav.Key
  , self: Url.Url
  , canvas: Bcc.BoundedContextCanvas
  }

init : Nav.Key -> Url.Url -> (Model, Cmd Msg)
init key url =
  let
    model =
      { key = key
      , self = url
      , canvas = { name = "", description = "", classification = Nothing}
      }
  in
    (
      model
    , loadBCC model
    )


-- UPDATE

type Msg
  = Loaded (Result Http.Error Bcc.BoundedContextCanvas)
  | Field Bcc.Msg
  | Save
  | Saved (Result Http.Error ())
  | Delete
  | Deleted (Result Http.Error ())
  | Back


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Field fieldMsg ->
      ({ model | canvas = Bcc.update fieldMsg model.canvas  }, Cmd.none)
    Save -> 
      (model, saveBCC model)
    Saved (Ok _) -> 
      (model, Cmd.none)
    Delete ->
      (model, deleteBCC model)
    Deleted (Ok _) ->
      (model, Route.pushUrl Route.Overview model.key)
    Loaded (Ok m) ->
      ({ model | canvas = m }, Cmd.none)    
    Back -> 
      (model, Route.goBack model.key)
    _ ->
      Debug.log ("BCC: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)

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


viewCanvas: Bcc.BoundedContextCanvas -> Html Bcc.Msg
viewCanvas model =
  Grid.row []
    [ Grid.col []
      [ Form.group []
        [ Form.label [for "name"] [ text "Name"]
        , Input.text [ Input.id "name", Input.value model.name, Input.onInput Bcc.SetName ] ]
      , Form.group []
        [ Form.label [for "description"] [ text "Description"]
        , Input.text [ Input.id "description", Input.value model.description, Input.onInput Bcc.SetDescription ]
        , Form.help [] [ text "Summary of purpose and responsibilities"] ]
      , Form.group []
        [ Form.label [for "classification"] [ text "Classification"]
        , div [] 
            (Radio.radioList "classification" 
            [ Radio.create [Radio.id "core", Radio.onClick (Bcc.SetClassification Bcc.Core), Radio.checked (model.classification == Just Bcc.Core)] "Core"
            , Radio.create [Radio.id "supporting", Radio.onClick (Bcc.SetClassification Bcc.Supporting), Radio.checked (model.classification == Just Bcc.Supporting)] "Supporting"
            , Radio.create [Radio.id "generic", Radio.onClick (Bcc.SetClassification Bcc.Generic), Radio.checked (model.classification == Just Bcc.Generic)] "Generic"
            -- , Radio.create [Radio.id "other", Radio.onClick (Bcc.SetClassification Bcc.Other), Radio.checked (model.classification == Just Bcc.Generic)] "Generic"
            ]
            )
        , Form.help [] [ text "Summary of purpose and responsibilities"] ]
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
      , body = Http.jsonBody <| modelEncoder model.canvas
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
        

modelEncoder: Bcc.BoundedContextCanvas -> Encode.Value
modelEncoder canvas = 
  Encode.object
    [ ("name", Encode.string canvas.name)
    , ("description", Encode.string canvas.description)
    , ("classification", classificationEncoder canvas.classification)
    ]

classificationEncoder : Maybe Bcc.Classification -> Encode.Value
classificationEncoder classification =
  case classification of
    Just c -> Encode.string (Bcc.classificationToString c)
    Nothing -> Encode.null

classificationDecoder: Decoder (Maybe Bcc.Classification)
classificationDecoder =
  Json.Decode.map Bcc.classificationParser string

modelDecoder: Decoder Bcc.BoundedContextCanvas
modelDecoder =
  Json.Decode.succeed Bcc.BoundedContextCanvas
    |> JP.required "name" string
    |> JP.optional "description" string ""
    |> JP.optional "classification" classificationDecoder Nothing 

    