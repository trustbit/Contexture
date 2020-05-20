module Main exposing (..)

-- Press buttons to increment and decrement a counter.
--
-- Read how it works:
--   https://guide.elm-lang.org/architecture/buttons.html
--

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



-- MAIN


main =
  Browser.sandbox { init = init, update = update, view = view }



-- MODEL


type alias Model = 
  { name: String
  , description: String
  }


init : Model
init =
  { name = ""
  , description = ""}



-- UPDATE


type Msg
  = SetName String
  | SetDescription String


update : Msg -> Model -> Model
update msg model =
  case msg of
    SetName name ->
      { model | name = name }

    SetDescription description ->
      { model | description = description}



-- VIEW


view : Model -> Html Msg
view model =
  Grid.container [] 
    [ CDN.stylesheet
    , Grid.row []
      [ Grid.col []
        [ Form.group []
          [ Form.label [for "name"] [ text "Name"]
          , Input.text [ Input.id "name", Input.value model.name, Input.onInput SetName ] ]
        , Form.group []
          [ Form.label [for "description"] [ text "Description"]
          , Input.text [ Input.id "description", Input.value model.description, Input.onInput SetDescription ] ]
        ]
      ]
    , Grid.row []
      [ Grid.col [] 
        [ Form.label [] [ text <| "echo name: " ++ model.name ]
        , Html.br [] []
        , Form.label [] [ text <| "echo description: " ++ model.description ]
        ]
      ]
    ]

