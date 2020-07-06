module Page.Domain.Edit exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav
import Browser.Dom as Dom

import Task

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
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
import Domain.DomainId exposing (DomainId)

import Page.Domain.Index as Index
import Page.Bcc.Index


-- MODEL

type EditDomain
  = ChangeName String
  | ChangeVision String

type alias EditableDomain =
  { domain : Domain.Domain
  , editDomain : Maybe EditDomain
  }

type alias Model =
  { key : Nav.Key
  , self : Url.Url
  , edit : RemoteData.WebData EditableDomain
  , subDomains : Index.Model
  , contexts : Page.Bcc.Index.Model
  }

initEdit : Domain.Domain -> EditableDomain
initEdit domain =
  { domain = domain
  , editDomain = Nothing
  }

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

type Msg
  = Loaded (Result Http.Error Domain.Domain)
  | SubDomainMsg Index.Msg
  | Saved (Result Http.Error ())
  | BccMsg Page.Bcc.Index.Msg
  | StartToChangeName
  | UpdateName String
  | RenameDomain String
  | StartToChangeVision
  | UpdateVision String
  | RefineVision String
  | StopToChangeDomain
  | NoOp

changeEdit : (EditableDomain -> EditableDomain) -> Model -> Model
changeEdit change model =
  { model | edit = model.edit |> RemoteData.map change }

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Saved (Ok _) ->
      (model |> changeEdit (\e -> { e | editDomain = Nothing }), Cmd.none)
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
    Loaded (Ok m) ->
      ( { model
        | edit = RemoteData.Success <| initEdit m
        }
      , Cmd.none
      )
    StartToChangeName ->
      ( model |> changeEdit (\e -> { e | editDomain = Just (ChangeName e.domain.name) })
      , Task.attempt (\_ -> NoOp) (Dom.focus "name")
      )
    UpdateName name ->
      ( model |> changeEdit (\e -> { e | editDomain = Just (ChangeName name) })
      , Cmd.none
      )
    RenameDomain newName ->
      case model.edit of
        RemoteData.Success { domain }  ->
          let
              d = { domain | name = newName }
          in

            ( model |> changeEdit (\e -> { e | domain = d})
            , saveBCC model.self d
            )
        _ ->
          Debug.log ("Cannot save unloaded model: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
          (model, Cmd.none)

    StartToChangeVision ->
      ( model |> changeEdit (\e -> { e | editDomain = Just (ChangeVision e.domain.vision) })
      , Task.attempt (\_ -> NoOp) (Dom.focus "vision")
      )

    UpdateVision name ->
      ( model |> changeEdit (\e -> { e | editDomain = Just (ChangeVision name) })
      , Cmd.none
      )
    RefineVision newVision ->
      case model.edit of
        RemoteData.Success { domain } ->
          let
              d = { domain | vision = newVision }
          in

            ( model |> changeEdit (\e -> { e | domain = d})
            , saveBCC model.self d
            )
        _ ->
          Debug.log ("Cannot save unloaded model: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
          (model, Cmd.none)

    StopToChangeDomain ->
      ( model |> changeEdit (\e -> { e | editDomain = Nothing }), Cmd.none)

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
        RemoteData.Success domain2 ->
          let
            backLink =
              case domain2.domain.parentDomain of
                Just id ->
                  Route.Domain id
                Nothing ->
                  Route.Home
          in
          ( List.concat
            [ [ Grid.simpleRow
                [ Grid.col []
                    [ Button.linkButton
                      [ Button.attrs [ href (Route.routeToString backLink) ], Button.roleLink ]
                      [ text "Back" ]
                    , viewDomain2 domain2
                    ]
                ]
              , Grid.simpleRow
                [ Grid.col [ Col.attrs [ Spacing.mt2 ] ]
                  [ Index.view model.subDomains |> Html.map SubDomainMsg ]
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

viewBccCard : Page.Bcc.Index.Model -> List(Html Msg)
viewBccCard model =
  Page.Bcc.Index.view model
  |> List.map (Html.map BccMsg)

viewEditName : String -> Html Msg
viewEditName name =
  Form.form [ onSubmit (RenameDomain name) ]
    [ Grid.row [ Row.betweenXs ]
      [ Grid.col []
        [ Input.text
          [ Input.id "name"
          , Input.value name
          , Input.onInput UpdateName
          , Input.placeholder "Choose a domain name"
          , if name |> Domain.isNameValid then Input.success else Input.danger
          ]
        , Form.help []
          [ text "Naming is hard. Writing down the name of your domain and gaining agreement as a team will frame how you design the domain and its content." ]
        , Form.invalidFeedback [] [ text "A name for the Domain is required!" ]
        ]
      , Grid.col [ Col.sm3 ]
        [ Button.submitButton
          [ Button.primary
          , Button.disabled (name |> Domain.isNameValid |> not)
          ]
          [ text "Change Domain Name"]
        , Button.button
          [ Button.secondary, Button.onClick StopToChangeDomain ]
          [ text "X"]
        ]
      ]
    ]

viewEditVision : String -> Html Msg
viewEditVision vision =
  Form.form [ onSubmit (RefineVision vision) ]
    [ Grid.row [ Row.betweenXs ]
      [ Grid.col []
        [ Textarea.textarea
          [ Textarea.id "vision"
          , Textarea.onInput UpdateVision
          , Textarea.rows 5
          , Textarea.value vision
          ]
        , Form.help []
          [ text "A few sentences describing the why and what of the domain in business language. No technical details here." ]
        ]
      , Grid.col [ Col.sm3 ]
        [ Button.submitButton [ Button.primary ]
          [ text "Refine Vision" ]
        , Button.button
          [ Button.secondary, Button.onClick StopToChangeDomain ]
          [ text "X"]
        ]
      ]
    ]


viewDomain2 : EditableDomain -> Html Msg
viewDomain2 model =
  let
    displayDomain =
      Grid.row [ Row.attrs [ Spacing.mb3 ] ]
      [ Grid.col []
        [ Html.h3 [ ] [ text model.domain.name ] ]
      , Grid.col [ Col.sm3 ]
        [ Button.button [ Button.outlinePrimary, Button.onClick StartToChangeName ] [ text "Change Domain Name" ] ]
      ]
    displayVision =
        Grid.simpleRow
        [ Grid.col []
          [ if model.domain.vision |> String.isEmpty then
              Html.p [ class "text-center" ] [ Html.i [] [ text "This domain has no vision :-("] ]
            else
              Html.p [ class "text-muted" ] [ text model.domain.vision ]
          ]
        , Grid.col [ Col.sm3 ]
          [ Button.button [ Button.outlinePrimary, Button.onClick StartToChangeVision ] [ text "Refine Vision" ] ]
        ]
  in
    ( case model.editDomain of
        Just (ChangeName name) ->
          [ viewEditName name
          , displayVision
          ]
        Just (ChangeVision vision) ->
          [ displayDomain
          , viewEditVision vision
          ]
        _ ->
          [ displayDomain
          , displayVision
          ]
    )
    |> div [ class "shadow", class "border", Spacing.p3 ]


-- HTTP

loadDomain: Model -> Cmd Msg
loadDomain model =
  Http.get
    { url = Url.toString model.self
    , expect = Http.expectJson Loaded Domain.domainDecoder
    }

saveBCC: Url.Url -> Domain.Domain -> Cmd Msg
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
