module Main exposing (..)

import Browser
import Browser.Navigation as Nav
import Url
import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Bootstrap.CDN as CDN
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Navbar as Navbar

import Route exposing ( Route)

import Bcc
import Bcc.Edit
import Overview

-- MAIN

main =
  let
    -- TODO: how is the DEV story with elm-live / elm reactor with local flags - defaults are not possible?! 
    -- elm-live with a custom index.html is not working?
    -- elm reactor with custom index.html works, but local routing+reloading is awkward
    -- use the following variant for local dev :-/
    -- initFunction = initLocal
    -- initFunction = init
    initFunction = initServer
  in
    Browser.application
      { init = initFunction
      , update = update
      , view = view
      , subscriptions = subscriptions
      , onUrlChange = UrlChanged
      , onUrlRequest = LinkClicked
      }

-- MODEL

type alias Flags =
    { baseUrl : String }

type Page 
  = NotFoundPage
  | Overview Overview.Model
  | Bcc Bcc.Edit.Model

type alias Model = 
  { key : Nav.Key
  , route : Route
  , navState: Navbar.State
  , baseUrl : String
  , page : Page }

initCurrentPage : ( Model, Cmd Msg ) -> ( Model, Cmd Msg )
initCurrentPage ( model, existingCmds ) =
    let
      ( currentPage, mappedPageCmds ) =
        case model.route of
          Route.NotFound ->
            ( NotFoundPage, Cmd.none )

          Route.Overview ->
            let
              ( pageModel, pageCmds ) = Overview.init model.baseUrl model.key
            in
              ( Overview pageModel, Cmd.map OverviewMsg pageCmds )
          Route.Bcc id ->
            case model.baseUrl ++ "/api/bccs/" ++ Bcc.idToString id |> Url.fromString of
              Just url ->
                let
                  ( pageModel, pageCmds ) = Bcc.Edit.init model.key url
                in
                  ( Bcc pageModel, Cmd.map BccMsg pageCmds )
              Nothing ->
                ( NotFoundPage, Cmd.none )

    in
    ( { model | page = currentPage }
    , Cmd.batch [ existingCmds, mappedPageCmds ]
    )

initServer : () -> Url.Url -> Nav.Key -> (Model, Cmd Msg)
initServer _ url key =
  init { baseUrl =  Url.toString { url | path = "",  query = Nothing, fragment = Nothing } } url key

initLocal : () -> Url.Url -> Nav.Key -> (Model, Cmd Msg)
initLocal _ url key =
  init { baseUrl = "http://localhost:3000" } url key

init : Flags -> Url.Url -> Nav.Key -> (Model, Cmd Msg)
init flag url key =
  let
    (navState, navCmd) = Navbar.initialState NavMsg
    model =
        { route = Route.parseUrl url
        , page = NotFoundPage
        , navState = navState
        , key = key
        , baseUrl = flag.baseUrl
        }
  in
    initCurrentPage ( model, navCmd )
 
-- UPDATE

type Msg
  = LinkClicked Browser.UrlRequest
  | UrlChanged Url.Url
  | NavMsg Navbar.State
  | OverviewMsg Overview.Msg
  | BccMsg Bcc.Edit.Msg


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case (msg, model.page) of
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
    (  NavMsg state, _) ->
      ( { model | navState = state }
      , Cmd.none
      )
    (OverviewMsg m, Overview overview) ->
      let
        (updatedModel, updatedMsg) = Overview.update m overview
      in
        ({ model | page = Overview updatedModel}, updatedMsg |> Cmd.map OverviewMsg)
    (BccMsg m, Bcc bccModel) ->
      let
        (mo, msg2) = Bcc.Edit.update m bccModel
      in
        ({ model | page = Bcc mo}, Cmd.map BccMsg msg2)
    (_, _) ->
      Debug.log ("Main: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)
        
        
-- SUBSCRIPTIONS

subscriptions : Model -> Sub Msg
subscriptions model =
      Navbar.subscriptions model.navState NavMsg


-- VIEW

menu : Model -> Html Msg
menu model =
  Navbar.config NavMsg
      |> Navbar.withAnimation
      |> Navbar.primary
      |> Navbar.brand [ href "/" ] [ text "Bounded Context Wizard" ]
      |> Navbar.items []
      |> Navbar.view model.navState

view : Model -> Browser.Document Msg
view model =
  let 
    content = 
      case model.page of
        Bcc m ->
          Bcc.Edit.view m |> Html.map BccMsg
        Overview o ->
          Overview.view o |> Html.map OverviewMsg
        NotFoundPage ->
          text "Not Found"
  in
    { title = "Bounded Context Wizard"
    , body = 
      [ CDN.stylesheet
      , div [] 
        [ menu model
        , Grid.containerFluid [] 
          [ content ]
        ]
      ]
    }


