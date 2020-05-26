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
import Route exposing (Route)

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

type alias Overview = 
  { bccName : String
  , bccs: List BccItem }

type Page 
  = NotFoundPage
  | Main Overview
  | Bcc Bcc.Model

type alias Model = 
  { key : Nav.Key
  , route : Route
  , model : Page }


initOverview: () -> (Overview, Cmd MainMsg2)
initOverview _ =
  ( { bccs = []
    , bccName = "" }
  , loadAll() )

initCurrentPage : ( Model, Cmd Msg ) -> ( Model, Cmd Msg )
initCurrentPage ( model, existingCmds ) =
    let
      ( currentPage, mappedPageCmds ) =
        case model.route of
          Route.NotFound ->
            ( NotFoundPage, Cmd.none )

          Route.Main ->
            let
                ( pageModel, pageCmds ) = initOverview ()
            in
            ( Main pageModel, Cmd.map MainMsg pageCmds )
          Route.Bcc id ->
            case "http://localhost:3000/api/bcc" ++ Bcc.idToString id |> Url.fromString of
              Just url ->
                let
                  ( pageModel, pageCmds ) = Bcc.init url
                in
                  ( Bcc pageModel, Cmd.map BccMsg pageCmds )
              Nothing ->
                ( NotFoundPage, Cmd.none )

    in
    ( { model | model = currentPage }
    , Cmd.batch [ existingCmds, mappedPageCmds ]
    )

init : () -> Url.Url -> Nav.Key -> (Model, Cmd Msg)
init _ url key =
  let  
    model =
        { route = Route.parseUrl url
        , model = NotFoundPage
        , key = key
        }
  in
    initCurrentPage ( model, Cmd.none )
 
-- UPDATE

type MainMsg2
  = Loaded (Result Http.Error (List BccItem))
  | SetName String
  | CreateBcc
  | Created (Result Http.Error BccItem)

type Msg
  = LinkClicked Browser.UrlRequest
  | UrlChanged Url.Url
  | MainMsg MainMsg2
  | BccMsg Bcc.Msg


updateMain : MainMsg2 -> Overview -> (Overview, Cmd MainMsg2)
updateMain msg model =
  case msg of
    Loaded (Ok items) ->
      ({ model | bccs = items }, Cmd.none)
    Loaded (Err e) ->
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
            , expect = Http.expectJson Created bccItemDecoder
            }
      in
        (model, create)
    -- _ -> (model, msg)
    Created result ->
      (model, Cmd.none)
    --   case result of
    --     Ok id ->
    --       let
    --         i = CreatedBcc id
    --       in
    --         (model, Cmd.Cmd i)
    --     Err e ->
    --       (model, Cmd.none)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case (msg, model.model) of
    ( LinkClicked urlRequest, _ ) ->
      case urlRequest of
          Browser.Internal url ->
              ( model
              , Nav.pushUrl model.key (Url.toString url)
              )

          Browser.External url ->
              ( model
              , Nav.load url
              )
    ( UrlChanged url, _ ) ->
      let
        newRoute = Route.parseUrl url
      in
      ( { model | route = newRoute }, Cmd.none )
          |> initCurrentPage
    (MainMsg m, Main overview) ->
      let
        (updatedModel, updatedMsg) = updateMain m overview
      in
        ({ model | model = Main updatedModel}, updatedMsg |> Cmd.map MainMsg)
            -- case updatedMsg of
            --   Created result ->
            --     case result of
            --       Ok id ->
            --         let
            --           (bccModel, bccMsg) = Bcc.init id
            --         in
            --           ( { model | model = Bcc bccModel}, bccMsg |> Cmd.map BccMsg) 
            --       Err e ->
            --         (model, Cmd.none)
    (BccMsg m, Bcc bccModel) ->
      let
        (mo, msg2) = Bcc.update m bccModel
      in
        ({ model | model = Bcc mo}, Cmd.map BccMsg msg2)
    (_, _) ->
      Debug.log (Debug.toString msg ++ Debug.toString model)
      (model, Cmd.none)
        
        
-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions _ =
  Sub.none

-- VIEW


createWithName : String -> Grid.Column MainMsg2
createWithName name =
  Grid.col []
    [ Form.group []
      [ Form.label [for "name"] [ text "Name"]
      , Input.text [ Input.id "name", Input.value name, Input.onInput SetName ] ]
    , Button.button [ Button.primary, Button.onClick CreateBcc ] [ text "Create new Bounded Context"]
    ]
  

viewExisting : List BccItem  -> Grid.Column MainMsg2
viewExisting items =
   let
      renderItem item =
          Html.li [] 
            [ Html.a [ href ("/bccs/" ++ Bcc.idToString item.id)] [text item.name] ]
    in
      Grid.col []
        [ Form.label [] [ text "Existing BCs"]
        , Html.ol [] (items |> List.map renderItem) ]

viewOverview : Overview -> Html MainMsg2
viewOverview model =
  Grid.row []
    [ viewExisting model.bccs
    , createWithName model.bccName
    ]

view : Model -> Browser.Document Msg
view model =
  let 
    content = 
      case model.model of
        Bcc m ->
          Bcc.view m |> Html.map BccMsg
        Main o ->
          viewOverview o |> Html.map MainMsg
        NotFoundPage ->
          text "Not Found"
  in
    { title = "Bounded Context Wizard"
    , body = 
      [ CDN.stylesheet
      , Grid.container [] 
        [ content ]
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



loadAll: () -> Cmd MainMsg2
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
    (at ["id"] Bcc.idDecoder)
    (at ["name"] string)
    
