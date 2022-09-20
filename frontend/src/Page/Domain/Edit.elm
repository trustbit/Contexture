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
import Bootstrap.Utilities.Spacing as Spacing

import RemoteData

import Url
import Http

import Api
import Route

import ShortName
import Domain
import Domain.DomainId exposing (DomainId)

import Page.ChangeShortName as ChangeShortName
import Page.Domain.Index as Index
import Page.Bcc.Index


-- MODEL

type EditDomain
  = ChangingName String
  | ChangingVision String
  | ChangingShortName ChangeShortName.Model

type alias EditableDomain =
  { domain : Domain.Domain
  , editDomain : Maybe EditDomain
  }

type alias Model =
  { shortName : Nav.Key
  , config : Api.Configuration
  , edit : RemoteData.WebData EditableDomain
  , subDomains : Index.Model
  , contexts : Page.Bcc.Index.Model
  }

initEdit : Domain.Domain -> EditableDomain
initEdit domain =
  { domain = domain
  , editDomain = Nothing
  }

init : Nav.Key -> Api.Configuration -> DomainId -> (Model, Cmd Msg)
init shortName config domain =
  let
    (contexts, contextCmd) = Page.Bcc.Index.init config shortName domain
    (subDomainsModel, subDomainsCmd) = Index.init config shortName (Domain.Subdomain domain)
    model =
      { shortName = shortName
      , config = config
      , edit = RemoteData.Loading
      , contexts = contexts
      , subDomains = subDomainsModel
      }
  in
    ( model
    , Cmd.batch
      [ loadDomain config domain
      , contextCmd |> Cmd.map BccMsg
      , subDomainsCmd |> Cmd.map SubDomainMsg
      ]
    )

-- UPDATE

type Msg
  = Loaded (Api.ApiResponse Domain.Domain)
  | SubDomainMsg Index.Msg
  | Saved (Api.ApiResponse Domain.Domain)
  | BccMsg Page.Bcc.Index.Msg
  | StartToChangeName
  | UpdateName String
  | RenameDomain (Result Domain.Problem Domain.Name)
  | StartToChangeVision
  | UpdateVision String
  | RefineVision String
  | StartToChangeShortName
  | ChangeShortNameMsg ChangeShortName.Msg
  | AssignShortName (Maybe ShortName.ShortName)
  | StopToEdit
  | NoOp

changeEdit : (EditableDomain -> EditableDomain) -> Model -> Model
changeEdit change model =
  { model | edit = model.edit |> RemoteData.map change }

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
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

    Saved (Ok domain) ->
      ({ model | edit = RemoteData.Success <| initEdit domain }, Cmd.none)

    StartToChangeName ->
      ( model |> changeEdit (\e -> { e | editDomain = Just (e.domain |> Domain.name |> ChangingName) })
      , Task.attempt (\_ -> NoOp) (Dom.focus "name")
      )

    UpdateName name ->
      ( model |> changeEdit (\e -> { e | editDomain = Just (ChangingName name) })
      , Cmd.none
      )

    RenameDomain (Ok newName) ->
      case model.edit of
        RemoteData.Success { domain }  ->
            ( model
            , Domain.renameDomain model.config (domain |> Domain.id) newName Saved
            )
        _ ->
          Debug.log ("Cannot save unloaded model: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
          (model, Cmd.none)

    StartToChangeShortName ->
      case model.edit of
        RemoteData.Success editable ->
          let
            (changeModel, changeCmd) =
              editable.domain
              |> Domain.shortName
              |> ChangeShortName.init model.config
          in
            ( model |> changeEdit (\e -> { e | editDomain = Just (ChangingShortName changeModel) } )
            , Cmd.batch
              [ Task.attempt (\_ -> NoOp) (Dom.focus "shortName")
              , changeCmd |> Cmd.map ChangeShortNameMsg
              ]
            )
        _ ->
          (model, Cmd.none)

    ChangeShortNameMsg changeMsg ->
      case model.edit of
        RemoteData.Success { editDomain } ->
          case editDomain of
            Just (ChangingShortName changingModel) ->
              let
                (changeModel, changeCmd) = ChangeShortName.update changeMsg changingModel
              in
                (model |> changeEdit (\e -> { e | editDomain = Just (ChangingShortName changeModel) })
                , changeCmd |> Cmd.map ChangeShortNameMsg
                )
            _ -> (model, Cmd.none)
        _ -> (model, Cmd.none)

    AssignShortName newShortName ->
      case model.edit of
        RemoteData.Success { domain } ->
          ( model
          , Domain.assignShortName model.config (domain |> Domain.id) newShortName Saved
          )
        _ ->
          Debug.log ("Cannot save unloaded model: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
          (model, Cmd.none)

    StartToChangeVision ->
      ( model |> changeEdit (\e -> { e | editDomain = Just (e.domain |> Domain.vision |> Maybe.withDefault "" |> ChangingVision) })
      , Task.attempt (\_ -> NoOp) (Dom.focus "vision")
      )

    UpdateVision name ->
      ( model |> changeEdit (\e -> { e | editDomain = Just (ChangingVision name) })
      , Cmd.none
      )

    RefineVision newVision ->
      case model.edit of
        RemoteData.Success { domain } ->
          let
              vision =
                if String.isEmpty newVision
                then Nothing
                else Just newVision
          in
            ( model
            , Domain.updateVision model.config (domain |> Domain.id) vision Saved
            )
        _ ->
          Debug.log ("Cannot save unloaded model: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
          (model, Cmd.none)

    StopToEdit ->
      ( model |> changeEdit (\e -> { e | editDomain = Nothing }), Cmd.none)

    _ ->
      let
        _ = Debug.log "BCC MSG" msg
      in
        (Debug.log "BCC Model" model, Cmd.none)

-- VIEW

view : Model -> Html Msg
view model =
  let
    detail =
      case model.edit of
        RemoteData.Success domain ->
          let
            backLink =
              case domain.domain |> Domain.domainRelation of
                Domain.Subdomain id ->
                  Route.Domain id
                Domain.Root ->
                  Route.Home
          in
          ( List.concat
            [ [ Grid.simpleRow
                [ Grid.col []
                    [ Button.linkButton
                      [ Button.attrs [ href (Route.routeToString backLink) ], Button.roleLink ]
                      [ text "Back" ]
                    , viewDomain domain
                    ]
                ]
              , Grid.row [ Row.attrs [ Spacing.mb4 ]]
                [ Grid.col [ Col.attrs [ Spacing.mt3 ] ]
                  [ Index.view model.subDomains |> Html.map SubDomainMsg ]
                ]
              ]
            , model.contexts
              |> Page.Bcc.Index.view
              |> List.map (Html.map BccMsg)
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

viewEditName : String -> Html Msg
viewEditName name =
  Form.form [ onSubmit (name |> Domain.asName |> RenameDomain) ]
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
          [ Button.secondary, Button.onClick StopToEdit ]
          [ text "X"]
        ]
      ]
    ]

viewEditShortName : ChangeShortName.Model -> Html Msg
viewEditShortName model =
  let
    shortNameIsValid =
      case model.value of
      Ok _ -> True
      Err _ -> False
    submit =
      case model.value of
        Ok v -> [ onSubmit (AssignShortName v) ]
        Err _ -> []
  in
  Form.form submit
    [ Grid.row [ Row.betweenXs ]
      [ Grid.col []
        [ ChangeShortName.view model |> Html.map ChangeShortNameMsg ]
      , Grid.col [ Col.sm3 ]
        [ Button.submitButton
          [ Button.primary
          , Button.disabled (shortNameIsValid |> not)
          ]
          [ text "Assign short name"]
        , Button.button
          [ Button.secondary, Button.onClick StopToEdit ]
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
          [ Button.secondary, Button.onClick StopToEdit ]
          [ text "X"]
        ]
      ]
    ]


viewDomain : EditableDomain -> Html Msg
viewDomain model =
  let
    displayDomain =
      Grid.row [ Row.attrs [ Spacing.mb3 ] ]
      [ Grid.col []
        [ Html.h3 [ ] [ text <| Domain.name model.domain ] ]
      , Grid.col [ Col.sm3 ]
        [ Button.button [ Button.outlinePrimary, Button.onClick StartToChangeName ] [ text "Change Domain Name" ] ]
      ]
    displayVision =
        Grid.simpleRow
        [ Grid.col []
          [ model.domain
            |> Domain.vision
            |> Maybe.map (\v -> Html.p [ class "text-muted" ] [ text v ])
            |> Maybe.withDefault (Html.p [ class "text-center", class "text-muted" ] [ Html.i [] [ text "This domain is not backed by any vision :-(" ] ])
          ]
        , Grid.col [ Col.sm3 ]
          [ Button.button [ Button.outlinePrimary, Button.onClick StartToChangeVision ] [ text "Refine Vision" ] ]
        ]
    displayShortName =
      Grid.simpleRow
        [ Grid.col []
          [ model.domain
            |> Domain.shortName
            |> Maybe.map (\v -> Html.p [] [ text <| ShortName.toString v])
            |> Maybe.withDefault (Html.p [ class "text-muted", class "text-center" ] [Html.i [] [text "No short name assigned" ] ] )
          ]
        , Grid.col [ Col.sm3 ]
          [ Button.button [ Button.outlinePrimary, Button.onClick StartToChangeShortName ] [ text "Assign short name"] ]
        ]
  in
    ( case model.editDomain of
        Just (ChangingName name) ->
          [ viewEditName name
          , displayVision
          , displayShortName
          ]
        Just (ChangingVision vision) ->
          [ displayDomain
          , viewEditVision vision
          , displayShortName
          ]
        Just (ChangingShortName shortNameModel) ->
          [ displayDomain
          , displayVision
          , viewEditShortName shortNameModel
          ]
        _ ->
          [ displayDomain
          , displayVision
          , displayShortName
          ]
    )
    |> div [ class "shadow", class "border", Spacing.p3 ]


-- HTTP

loadDomain: Api.Configuration -> DomainId -> Cmd Msg
loadDomain config domain =
  Http.get
    { url = Api.domain [] domain |> Api.url config 
    , expect = Http.expectJson Loaded Domain.domainDecoder
    }
