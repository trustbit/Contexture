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
      , canvas = Bcc.init ()
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

viewRadioButton : String -> String -> Bool -> Bcc.Msg -> Radio.Radio Bcc.Msg
viewRadioButton id title checked msg =
  Radio.create [Radio.id id, Radio.onClick msg, Radio.checked checked] title

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
      , Grid.row []
        [ Grid.col [] 
          [ Form.label [for "classification"] [ text "Bounded Context classification"]
          , div [] 
              (Radio.radioList "classification" 
              [ viewRadioButton "core" "Core" (model.classification == Just Bcc.Core) (Bcc.SetClassification Bcc.Core) 
              , viewRadioButton "supporting" "Supporting" (model.classification == Just Bcc.Supporting) (Bcc.SetClassification Bcc.Supporting) 
              , viewRadioButton "generic" "Generic" (model.classification == Just Bcc.Generic) (Bcc.SetClassification Bcc.Generic) 
              -- TODO: Other
              ]
              )
          , Form.help [] [ text "How can the Bounded Context be classified?"] ]
          , Grid.col []
            [ Form.label [for "businessModel"] [ text "Business Model"]
            , div [] 
                (Radio.radioList "businessModel" 
                [ viewRadioButton "revenue" "Revenue" (model.businessModel == Just Bcc.Revenue) (Bcc.SetBusinessModel Bcc.Revenue) 
                , viewRadioButton "engagement" "Engagement" (model.businessModel == Just Bcc.Engagement) (Bcc.SetBusinessModel Bcc.Engagement) 
                , viewRadioButton "Compliance" "Compliance" (model.businessModel == Just Bcc.Compliance) (Bcc.SetBusinessModel Bcc.Compliance) 
                , viewRadioButton "costReduction" "Cost reduction" (model.businessModel == Just Bcc.CostReduction) (Bcc.SetBusinessModel Bcc.CostReduction) 
                -- TODO: Other
                ]
                )
            , Form.help [] [ text "What's the underlying business model of the Bounded Context?"] ]
          , Grid.col []
            [ Form.label [for "evolution"] [ text "Evolution"]
            , div [] 
                (Radio.radioList "evolution" 
                [ viewRadioButton "genesis" "Genesis" (model.evolution == Just Bcc.Genesis) (Bcc.SetEvolution Bcc.Genesis) 
                , viewRadioButton "customBuilt" "Custom built" (model.evolution == Just Bcc.CustomBuilt) (Bcc.SetEvolution Bcc.CustomBuilt) 
                , viewRadioButton "product" "Product" (model.evolution == Just Bcc.Product) (Bcc.SetEvolution Bcc.Product) 
                , viewRadioButton "commodity" "Commodity" (model.evolution == Just Bcc.Commodity) (Bcc.SetEvolution Bcc.Commodity) 
                -- TODO: Other
                ]
                )
            , Form.help [] [ text "How does the context evolve? How novel is it?"] ]
        ]
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
        

modelEncoder : Bcc.BoundedContextCanvas -> Encode.Value
modelEncoder canvas = 
  Encode.object
    [ ("name", Encode.string canvas.name)
    , ("description", Encode.string canvas.description)
    , ("classification", maybeStringEncoder Bcc.classificationToString canvas.classification)
    , ("businessModel", maybeStringEncoder Bcc.businessModelToString canvas.businessModel)
    , ("evolution", maybeStringEncoder Bcc.evolutionToString canvas.evolution)
    ]

maybeStringEncoder : (t -> String) -> Maybe t -> Encode.Value
maybeStringEncoder encoder value =
  case value of
    Just v -> Encode.string (encoder v)
    Nothing -> Encode.null

maybeStringDecoder : (String -> Maybe v) -> Decoder (Maybe v)
maybeStringDecoder parser =
  Json.Decode.map parser string

modelDecoder: Decoder Bcc.BoundedContextCanvas
modelDecoder =
  Json.Decode.succeed Bcc.BoundedContextCanvas
    |> JP.required "name" string
    |> JP.optional "description" string ""
    |> JP.optional "classification" (maybeStringDecoder Bcc.classificationParser) Nothing 
    |> JP.optional "businessModel" (maybeStringDecoder Bcc.businessModelParser) Nothing
    |> JP.optional "evolution" (maybeStringDecoder Bcc.evolutionParser) Nothing 

    