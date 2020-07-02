module Page.Domain.Edit exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.Checkbox as Checkbox
import Bootstrap.Button as Button
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Text as Text
import Bootstrap.Utilities.Spacing as Spacing

import RemoteData

import Url
import Http

import Api
import Route

import Domain
import Page.Domain.Index as Index
import Page.Bcc.Index
import Domain.DomainId exposing (DomainId)

-- MODEL

type alias EditableDomain = Domain.Domain
  
type alias Model =
  { key: Nav.Key
  , self: Url.Url
  , edit: RemoteData.WebData EditableDomain
  , subDomains : Index.Model
  , contexts : Page.Bcc.Index.Model
  }

initEdit : Domain.Domain -> EditableDomain
initEdit domain =
  domain

init : Nav.Key -> Url.Url -> DomainId -> (Model, Cmd Msg)
init key url domain =
  let
    (contexts, contextCmd) = Page.Bcc.Index.init url key
    (subDomainsModel, subDomainsCmd) = Index.initWithSubdomains (Api.configFromScoped url) key domain
    model =
      { key = key
      , self = url
      , edit = RemoteData.Loading
      , contexts = contexts
      , subDomains = subDomainsModel
      }
  in
    (
      model
    , Cmd.batch
      [ loadDomain model
      , contextCmd |> Cmd.map BccMsg
      , subDomainsCmd |> Cmd.map SubDomainMsg
      ]
    )

-- UPDATE


type EditingMsg
  = SetName String
  | SetVision String

type Msg
  = Loaded (Result Http.Error Domain.Domain)
  | Editing EditingMsg
  | SubDomainMsg Index.Msg
  | Save
  | Saved (Result Http.Error ())
  | Delete
  | Deleted (Result Http.Error ())
  | BccMsg Page.Bcc.Index.Msg

updateEditField : EditingMsg -> EditableDomain -> EditableDomain
updateEditField msg model =
  case msg of
    SetName name ->
      { model | name = name}
    SetVision vision->
      { model | vision = vision}

updateEdit : EditingMsg -> RemoteData.WebData EditableDomain -> RemoteData.WebData EditableDomain
updateEdit msg model =
  case model of
    RemoteData.Success domain ->
      RemoteData.Success <| updateEditField msg domain
    _ -> model

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Editing editing ->
      ({ model | edit = updateEdit editing model.edit}, Cmd.none)
    Save ->
      case model.edit of
        RemoteData.Success domain ->
          (model, saveBCC model.self domain)
        _ ->
          Debug.log ("Cannot save unloaded model: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
          (model, Cmd.none)
    Saved (Ok _) ->
      (model, Cmd.none)
    Delete ->
      (model, deleteBCC model)
    Deleted (Ok _) ->
      (model, Route.pushUrl Route.Home model.key)
    Loaded (Ok m) ->
      ({ model | edit = RemoteData.Success <| initEdit m } , Cmd.none)
    Loaded (Err e) ->
      ({ model | edit = RemoteData.Failure e } , Cmd.none)
    BccMsg m ->
      let
        (bccModel, bccCmd) = Page.Bcc.Index.update m model.contexts
      in
        ({ model | contexts = bccModel}, bccCmd |> Cmd.map BccMsg)
    SubDomainMsg subMsg ->
      let
        (subModel, subCmd) = Index.update subMsg model.subDomains
      in
        ({ model | subDomains = subModel }, subCmd |> Cmd.map SubDomainMsg)
    _ ->
      Debug.log ("BCC: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)

-- VIEW

viewLabel : String -> String -> Html msg
viewLabel labelId caption =
  Form.label [ for labelId] [ Html.h6 [] [ text caption ] ]

view : Model -> Html Msg
view model =
  let
    detail =
      case model.edit of
        RemoteData.Success domain ->
          ( List.concat
            [ [ Grid.simpleRow
                [ Grid.col []
                    [ viewDomainCard domain ]
                ]
              , Grid.simpleRow
                [ Grid.col []
                  [ Html.h5 [ Spacing.mt3 ] [ text "Subdomains" ]
                  , Index.view model.subDomains |> Html.map SubDomainMsg ]
                ]
              , Grid.simpleRow
                [ Grid.col []
                  [ Html.h5 [ Spacing.mt3 ] [ text "Bounded Context of the domain" ] ]
                ]
              ]
            , viewBccCard model.contexts
            ]
          )
        _ ->
          [ Grid.row []
            [ Grid.col []
              [ Html.p [] [ text "Loading details..." ] ]
            ]
          ]
  in
    Grid.container [] detail


viewDomainCard : EditableDomain -> Html Msg
viewDomainCard model =
  let
    backLink =
      case model.parentDomain of
        Just id ->
          Route.Domain id
        Nothing ->
          Route.Home
  in
  Card.config []
  |> Card.header []
    [ Html.h5 [] [ text "Manage your domain"] ]
  |> Card.block []
    [ Block.custom <| (viewDomain model |> Html.map Editing) ]
  |> Card.footer []
    [ Grid.row []
      [ Grid.col []
        [ Button.linkButton
          [ Button.attrs [ href (Route.routeToString backLink) ], Button.roleLink ]
          [ text "Back" ] ]
      , Grid.col [ Col.textAlign Text.alignLgRight ]
        [ Button.button
          [ Button.secondary
          , Button.onClick Delete
          , Button.attrs
            [ title ("Delete " ++ model.name)
            , Spacing.mr3
            ]
          ]
          [ text "Delete" ]
        , Button.submitButton
          [ Button.primary
          , Button.onClick Save
          , Button.disabled (model.name |> Domain.isNameValid |> not)
          ]
          [ text "Save"]
        ]
      ]
    ]
  |> Card.view

viewBccCard : Page.Bcc.Index.Model -> List(Html Msg)
viewBccCard model =
  Page.Bcc.Index.view model
  |> List.map (Html.map BccMsg)


viewDomain : EditableDomain -> Html EditingMsg
viewDomain model =
  div []
    [ Form.group []
      [ viewLabel "name" "Name"
      , Input.text (
        List.concat
        [ [ Input.id "name", Input.value model.name, Input.onInput SetName ]
        , if model.name |> Domain.isNameValid then [] else [ Input.danger ]
        ]
      )
      , Form.invalidFeedback [] [ text "A name for the Domain is required!" ]
      ]
    , Html.hr [] []
    , Form.group []
        [ viewLabel "vision" "Vision Statement"
        , Textarea.textarea
          [ Textarea.id "vision"
          , Textarea.value model.vision
          , Textarea.onInput SetVision
          , Textarea.rows 5
          ]
        , Form.help [] [ text "Summary of purpose"] ]
    ]

-- HTTP

loadDomain: Model -> Cmd Msg
loadDomain model =
  Http.get
    { url = Url.toString model.self
    , expect = Http.expectJson Loaded Domain.domainDecoder
    }

saveBCC: Url.Url -> EditableDomain -> Cmd Msg
saveBCC url model =
  Http.request
    { method = "PATCH"
    , headers = []
    , url = Url.toString url
    , body = Http.jsonBody <| Domain.modelEncoder model
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
