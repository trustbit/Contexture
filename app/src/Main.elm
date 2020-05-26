module Main exposing (..)

import Browser
import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)
import Bootstrap.CDN as CDN
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Button as Button

import Http
import Json.Encode as Encode
import Json.Decode exposing (Decoder, map2, field, string, int, at)


-- MAIN


main =
  Browser.element 
    { init = init "1234"
    , update = update
    , view = view
    , subscriptions = subscriptions 
    }


-- MODEL

type alias BoundedContextId = String

type alias BoundedContextCanvas = 
  { name: String
  , description: String
  }

type alias Model = 
  { id: BoundedContextId
  , exists: Bool
  , canvas: BoundedContextCanvas
  }


init : BoundedContextId -> () -> (Model, Cmd Msg)
init id _ =
  (
    { id = id
    , exists = False
    , canvas = { name = "", description = ""}
    }
  , loadBCC id
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


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Field fieldMsg ->
      ({ model | canvas = updateFields fieldMsg model.canvas  }, Cmd.none)
      
    Save -> 
      (model, saveBCC model)
    Saved result -> 
      case result of
        Ok _ -> 
          (model, Cmd.none)
        Err _ ->
          (model, Cmd.none)
    Loaded result ->
      case result of
        Ok m ->
          ({ model | exists = True, canvas = m }, Cmd.none)
        Err _ ->
          (model, Cmd.none)

updateFields: FieldMsg -> BoundedContextCanvas -> BoundedContextCanvas
updateFields msg canvas =
  case msg of
    SetName name ->
      { canvas | name = name}
      
    SetDescription description ->
      { canvas | description = description}
      
-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions _ =
  Sub.none

-- VIEW


view : Model -> Html Msg
view model =
  Grid.container [] 
    [ CDN.stylesheet
    , viewCanvas model.canvas |> Html.map Field
    , Grid.row []
      [ Grid.col [] 
        [ Form.label [] [ text <| "echo name: " ++ model.canvas.name ]
        , Html.br [] []
        , Form.label [] [ text <| "echo description: " ++ model.canvas.description ]
        , Html.br [] []
        , Button.button [ Button.primary, Button.onClick Save ] [ text "Save"]
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

loadBCC: BoundedContextId -> Cmd Msg
loadBCC id =
  Http.get
    { url = "http://localhost:3000/api/bccs/" ++ id
    , expect = Http.expectJson Loaded modelDecoder
    }

saveBCC: Model -> Cmd Msg
saveBCC model =
  if model.exists then
    Http.request
      { method = "PUT"
      , headers = []
      , url = "http://localhost:3000/api/bccs/" ++ model.id
      , body = Http.jsonBody <| modelEncoder model
      , expect = Http.expectWhatever Saved
      , timeout = Nothing
      , tracker = Nothing
      }
  else
    Http.post
      { url = "http://localhost:3000/api/bccs"
      , body = Http.jsonBody <| modelEncoder model
      , expect = Http.expectWhatever Saved
      }

modelEncoder: Model -> Encode.Value
modelEncoder model = 
  Encode.object
    [ ("id", Encode.string model.id)
    , ("name", Encode.string model.canvas.name)
    , ("description", Encode.string model.canvas.description)
    ]

modelDecoder: Decoder BoundedContextCanvas
modelDecoder =
  map2 BoundedContextCanvas
    (at ["name"] string)
    (at ["description"] string)
