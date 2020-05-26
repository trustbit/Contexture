module Bcc exposing (Msg, Model, BoundedContextId, idToString, idDecoder, idParser, update, view, init)

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Button as Button

import Url
import Url.Parser exposing (Parser, custom)

import Http
import Json.Encode as Encode
import Json.Decode exposing (Decoder, map2, field, string, int, at, nullable)
import Json.Decode.Pipeline as JP


-- MODEL

type BoundedContextId 
  = BoundedContextId Int

idToString : BoundedContextId -> String
idToString bccId =
  case bccId of
    BoundedContextId id -> String.fromInt id

idParser : Parser (BoundedContextId -> a) a
idParser =
    custom "BCCID" <|
        \bccId ->
            Maybe.map BoundedContextId (String.toInt bccId)

type alias BoundedContextCanvas = 
  { name: String
  , description: String
  }

type alias Model = 
  { url: Url.Url
  , canvas: BoundedContextCanvas
  }


init : Url.Url -> (Model, Cmd Msg)
init url =
    (
      { url = url
      , canvas = { name = "", description = ""}
      }
    , loadBCC url
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
          Debug.log (Debug.toString m)
          ({ model | canvas = m }, Cmd.none)
        Err e ->
          Debug.log (Debug.toString e)
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
    Html.div []
        [ viewCanvas model.canvas |> Html.map Field
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

loadBCC: Url.Url -> Cmd Msg
loadBCC url =
  Http.get
    { url = Url.toString url
    , expect = Http.expectJson Loaded modelDecoder
    }

saveBCC: Model -> Cmd Msg
saveBCC model =
    Http.request
      { method = "PUT"
      , headers = []
      , url = Url.toString model.url
      , body = Http.jsonBody <| modelEncoder model
      , expect = Http.expectWhatever Saved
      , timeout = Nothing
      , tracker = Nothing
      }

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
    

idDecoder : Decoder BoundedContextId
idDecoder =
  Json.Decode.map BoundedContextId int