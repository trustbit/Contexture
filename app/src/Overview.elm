module Overview exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Json.Encode as Encode
import Json.Decode exposing (Decoder, map2, field, string, int, at, list)

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Form.Input as Input
import Bootstrap.Button as Button

import Url
import Http

import Bcc
import Route

-- MODEL

type alias BccItem = 
  { id: Bcc.BoundedContextId
  , name: String }

type alias Model = 
  { navKey : Nav.Key
  , bccName : String
  , baseUrl : String
  , bccs: List BccItem }

init: String -> Nav.Key -> (Model, Cmd Msg)
init baseUrl key =
  ( { navKey = key
    , bccs = []
    , baseUrl = baseUrl
    , bccName = "" }
  , loadAll baseUrl )

-- UPDATE

type Msg
  = Loaded (Result Http.Error (List BccItem))
  | SetName String
  | CreateBcc
  | Created (Result Http.Error BccItem)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Loaded (Ok items) ->
      ({ model | bccs = items }, Cmd.none)
    SetName name ->
      ({ model | bccName = name}, Cmd.none)
    CreateBcc ->
      (model, createNewBcc model)
    Created (Ok item) ->
        (model, Route.pushUrl (Route.Bcc item.id) model.navKey)
    _ -> 
        Debug.log ("Overview: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
        (model, Cmd.none)

-- VIEW

createWithName : String -> Html Msg
createWithName name =
    Form.form [Html.Events.onSubmit CreateBcc]
        [ Fieldset.config
          |> Fieldset.legend [] [ text "Create a Bounded Context Canvas"]
          |> Fieldset.children
            [ Form.group []
                [ Form.label [for "name"] [ text "Name"]
                , Input.text [ Input.id "name", Input.value name, Input.onInput SetName ] ]
            , Button.submitButton [ Button.primary] [ text "Fill out the Rest!"] ]
           |> Fieldset.view
        ]


viewExisting : List BccItem  -> Html Msg
viewExisting items =
   let
      renderItem item =
          Html.li [] 
            [ Html.a [ href ("/bccs/" ++ Bcc.idToString item.id)] [text item.name] ]
    in
      div []
        [ Html.h3 [] [ text "Existing BCs"]
        , Html.ol [] (items |> List.map renderItem) ]

view : Model -> Html Msg
view model =
  Grid.row []
    [ Grid.col [] [viewExisting model.bccs]
    , Grid.col [] [createWithName model.bccName]
    ]

-- helpers

loadAll: String -> Cmd Msg
loadAll baseUrl =
  Http.get
    { url = baseUrl ++ "/api/bccs"
    , expect = Http.expectJson Loaded bccItemsDecoder
    }

createNewBcc : Model -> Cmd Msg
createNewBcc model = 
    let 
        body =
            Encode.object
            [ ("name", Encode.string model.bccName) ]
    in 
        Http.post
        { url = model.baseUrl ++ "/api/bccs"
        , body = Http.jsonBody body 
        , expect = Http.expectJson Created bccItemDecoder
        }

bccItemsDecoder: Decoder (List BccItem)
bccItemsDecoder =
  Json.Decode.list bccItemDecoder


bccItemDecoder: Decoder BccItem
bccItemDecoder =
  map2 BccItem
    (at ["id"] Bcc.idDecoder)
    (at ["name"] string)
    