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
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input

import Bootstrap.Button as Button

import Http
import Json.Encode as Encode
import Json.Decode exposing (Decoder, map2, field, string, int, at, list)

import Dict

import Bcc

-- MAIN


main =
  Browser.application 
    { init = init
    , update = update
    , view = view
    , subscriptions = subscriptions
    , onUrlChange = UrlChanged
    , onUrlRequest = LinkClicked 
    }


-- MODEL

type alias BccItem = 
  { id: Bcc.BoundedContextId
  , name: String }

type alias Model = 
  { key : Nav.Key
  , url : Url.Url
  , bccName : String
  , bccs: List BccItem
  , model: Maybe Bcc.Model }


init : () -> Url.Url -> Nav.Key -> (Model, Cmd Msg)
init _ url key =
  ( 
    { key = key
    , url = url
    , bccName = ""
    , bccs = []
    , model = Nothing }
  , loadAll () 
  )


-- UPDATE

type Msg
  = LinkClicked Browser.UrlRequest
  | UrlChanged Url.Url
  | Loaded (Result Http.Error (List BccItem))
  | SetName String
  | CreateBcc
  | Created (Result String Url.Url)
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
    Loaded result ->
      case result of
        Ok items ->
          ({ model | bccs = items }, Cmd.none)
        Err e ->
          Debug.log (Debug.toString e)
          (model, Cmd.none)
    SetName name ->
      ({ model | bccName = name}, Cmd.none)
    CreateBcc ->
      let
        body =
          Encode.object
            [ ("name", Encode.string model.bccName) ]
        create = 
          Http.post
            { url = "http://localhost:3000/api/bccs"
            , body = Http.jsonBody body 
            , expect = Http.expectStringResponse Created (extractHeader "location")
            }
      in
        (model, create)
    Created result ->
      case result of
        Ok id ->
          let
            (bccModel, bccMsg) = Bcc.init id
          in
            ({ model | model = Just bccModel }, bccMsg |> Cmd.map BccMsg )
        Err e ->
          (model, Cmd.none)
    BccMsg m ->
      case model.model of
        Just bccModel ->
          let
            (mo, msg2) = Bcc.update m bccModel
          in
            ({ model | model = Just mo}, Cmd.map BccMsg msg2)
        Nothing ->
          (model, Cmd.none)
      
-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions _ =
  Sub.none

-- VIEW


createWithName : String -> Html Msg
createWithName name =
  Grid.row []
    [ Grid.col []
      [ Form.group []
        [ Form.label [for "name"] [ text "Name"]
        , Input.text [ Input.id "name", Input.value name, Input.onInput SetName ] ]
      , Button.button [ Button.primary, Button.onClick CreateBcc ] [ text "Create new Bounded Context"]
      ]
    ]
    

viewExisting : List BccItem  -> Html Msg
viewExisting items =
   let
      renderItem item =
          Html.li [] 
            [ Html.a [ href ("http://localhost:3000/api/bccs/" ++ String.fromInt item.id)] [text item.name] ]
    in
      Grid.row []
      [ Grid.col []
        [ Form.label [] [ text "Existing BCs" ]
        , Html.ol [] (items |> List.map renderItem) ]
      ]

view : Model -> Browser.Document Msg
view model =
  let 
    content = 
      case model.model of
        Just m ->
          Bcc.view m |> Html.map BccMsg
        Nothing ->
          createWithName model.bccName
  in
    { title = "Bounded Context Wizard"
    , body = 
      [ CDN.stylesheet
      , Grid.container [] 
        [ viewExisting model.bccs
        , content ]
      ]
    }


-- helpers

extractHeader : String -> Http.Response String -> Result String Url.Url
extractHeader name resp =
    case resp of
      Http.GoodStatus_ r _ ->
        Dict.get name r.headers
          |> Maybe.andThen Url.fromString
          |> Result.fromMaybe ("header " ++ name ++ " not found")
      
      _ -> Err "request not successful"



loadAll: () -> Cmd Msg
loadAll _ =
  Http.get
    { url = "http://localhost:3000/api/bccs"
    , expect = Http.expectJson Loaded bccItemsDecoder
    }

bccItemsDecoder: Decoder (List BccItem)
bccItemsDecoder =
  Json.Decode.list bccItemDecoder


bccItemDecoder: Decoder BccItem
bccItemDecoder =
  map2 BccItem
    (at ["id"] int)
    (at ["name"] string)
    
