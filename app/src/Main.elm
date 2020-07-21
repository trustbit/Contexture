module Main exposing (main)

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
import Bootstrap.Utilities.Spacing as Spacing

import Route exposing ( Route)
import Api

import Page.Domain.Index
import Page.Domain.Edit
import Page.Bcc.Edit
import Page.Bcc.Technical

-- MAIN

main =
  let
    -- TODO: how is the DEV story with elm-live / elm reactor with local flags - defaults are not possible?!
    -- elm-live with a custom index.html is not working?
    -- elm reactor with custom index.html works, but local routing+reloading is awkward
    -- use the following variant for local dev :-/
    initFunction = initWithDerivedUrl
    -- initFunction = init
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
    { baseUrl : Url.Url }

type Page
  = NotFoundPage
  | Domains Page.Domain.Index.Model
  | DomainsEdit Page.Domain.Edit.Model
  | BoundedContextCanvas Page.Bcc.Edit.Model
  | Technical Page.Bcc.Technical.Model

type alias Model =
  { key : Nav.Key
  , route : Route
  , navState: Navbar.State
  , baseUrl : Url.Url
  , page : Page }

initCurrentPage : ( Model, Cmd Msg ) -> ( Model, Cmd Msg )
initCurrentPage ( model, existingCmds ) =
    let
      ( currentPage, mappedPageCmds ) =
        case model.route of
          Route.NotFound ->
            ( NotFoundPage, Cmd.none )

          Route.Home ->
            let
              ( pageModel, pageCmds ) = Page.Domain.Index.initWithoutSubdomains (Api.config model.baseUrl) model.key
            in
              ( Domains pageModel, Cmd.map DomainMsg pageCmds )
          Route.Domain id ->
            let
              ( pageModel, pageCmds ) = Page.Domain.Edit.init model.key (Api.config model.baseUrl) id
            in
              ( DomainsEdit pageModel, Cmd.map DomainEditMsg pageCmds )
          Route.BoundedContextCanvas id ->
            let
              ( pageModel, pageCmds ) = Page.Bcc.Edit.init model.key (Api.config model.baseUrl) id
            in
              ( BoundedContextCanvas pageModel, Cmd.map BccMsg pageCmds )
          Route.TechnicalDescription id ->
            let
              ( pageModel, pageCmds ) = Page.Bcc.Technical.init model.key (Api.config model.baseUrl) id
            in
              ( Technical pageModel, Cmd.map TechnicalMsg pageCmds )


    in
    ( { model | page = currentPage }
    , Cmd.batch [ existingCmds, mappedPageCmds ]
    )

deriveBaseUrl : Url.Url -> Url.Url
deriveBaseUrl appUrl =
  let
    localDev = { protocol = Url.Http, host ="localhost", port_ = Just 3000, path = "/api", query = Nothing, fragment = Nothing}
  in
  case appUrl.port_ of
    -- local dev with elm-live
    Just 8000 -> localDev
    -- local deployed version
    Just 3000 -> localDev
    -- default: assume it's running on the same server with the same port in the root
    _ -> { appUrl | path = "/api",  query = Nothing, fragment = Nothing }

initWithDerivedUrl : () -> Url.Url -> Nav.Key -> (Model, Cmd Msg)
initWithDerivedUrl _ url key =
  init { baseUrl = deriveBaseUrl url} url key

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
  | DomainMsg Page.Domain.Index.Msg
  | DomainEditMsg Page.Domain.Edit.Msg
  | BccMsg Page.Bcc.Edit.Msg
  | TechnicalMsg Page.Bcc.Technical.Msg

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
    (DomainMsg m, Domains overview) ->
      let
        (updatedModel, updatedMsg) = Page.Domain.Index.update m overview
      in
        ({ model | page = Domains updatedModel}, updatedMsg |> Cmd.map DomainMsg)
    (DomainEditMsg m, DomainsEdit edit) ->
      let
        (updatedModel, updatedMsg) = Page.Domain.Edit.update m edit
      in
        ({ model | page = DomainsEdit updatedModel}, updatedMsg |> Cmd.map DomainEditMsg)
    (BccMsg m, BoundedContextCanvas bccModel) ->
      let
        (mo, msg2) = Page.Bcc.Edit.update m bccModel
      in
        ({ model | page = BoundedContextCanvas mo}, Cmd.map BccMsg msg2)
    (TechnicalMsg m, Technical technicalModel) ->
      let
        (updatedModel, technicalMsg) = Page.Bcc.Technical.update m technicalModel
      in
        ({ model | page = Technical updatedModel}, Cmd.map TechnicalMsg technicalMsg)

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
      |> Navbar.brand [ href "/" ] [ text "Contexture" ]
      |> Navbar.items []
      |> Navbar.view model.navState

view : Model -> Browser.Document Msg
view model =
  let
    content =
      case model.page of
        BoundedContextCanvas m ->
          Page.Bcc.Edit.view m |> Html.map BccMsg
        Domains o ->
          Page.Domain.Index.view o |> Html.map DomainMsg
        DomainsEdit o ->
          Page.Domain.Edit.view o |> Html.map DomainEditMsg
        Technical t ->
          Page.Bcc.Technical.view t |> Html.map TechnicalMsg
        NotFoundPage ->
          text "Not Found"
  in
    { title = "Contexture - Managing your Domains & Contexts"
    , body =
      [ CDN.stylesheet
      , div []
        [ menu model
        , div [ Spacing.pt3 ]
          [ content ]
        ]
      ]
    }


