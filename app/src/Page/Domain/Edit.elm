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

import Set

import Url
import Http

import Api
import Route

import Key
import Domain
import Domain.DomainId exposing (DomainId)

import Page.Domain.Index as Index
import Page.Bcc.Index


-- MODEL

type EditDomain
  = ChangingName String
  | ChangingVision String
  | ChangingKey String

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
  | Saved (Result Http.Error Domain.Domain)
  | BccMsg Page.Bcc.Index.Msg
  | StartToChangeName
  | UpdateName String
  | RenameDomain (Result Domain.Problem Domain.Name)
  | StartToChangeVision
  | UpdateVision String
  | RefineVision String
  | StartToChangeKey
  | UpdateKey String
  | AssignKey (Result Key.Problem Key.Key)
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
            , Domain.renameDomain (Api.configFromScoped model.self) (domain |> Domain.id) newName Saved
            )
        _ ->
          Debug.log ("Cannot save unloaded model: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
          (model, Cmd.none)

    StartToChangeKey ->
      let
        edit { domain } =
          domain
          |> Domain.key
          |> Maybe.map Key.toString
          |> Maybe.withDefault ""
          |> ChangingKey
      in
      ( model |> changeEdit (\e -> { e | editDomain = Just (edit e) })
      , Task.attempt (\_ -> NoOp) (Dom.focus "key")
      )

    UpdateKey key ->
      ( model |> changeEdit (\e -> { e | editDomain = Just (ChangingKey key) })
      , Cmd.none
      )

    AssignKey newKey ->
      case model.edit of
        RemoteData.Success { domain } ->
          let
            action = Domain.assignKey (Api.configFromScoped model.self) (domain |> Domain.id)
            cmd =
              case newKey of
                Ok k -> action (Just k) |> List.singleton
                Err Key.Empty -> action Nothing |> List.singleton
                Err e -> []
          in
            ( model
            , cmd |> List.map (\a -> a Saved) |> Cmd.batch
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
            , Domain.updateVision (Api.configFromScoped model.self) (domain |> Domain.id) vision Saved
            )
        _ ->
          Debug.log ("Cannot save unloaded model: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
          (model, Cmd.none)

    StopToEdit ->
      ( model |> changeEdit (\e -> { e | editDomain = Nothing }), Cmd.none)

    _ ->
      Debug.log ("BCC: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)

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
              , Grid.simpleRow
                [ Grid.col [ Col.attrs [ Spacing.mt3 ] ]
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

viewEditKey : String -> Html Msg
viewEditKey key =
  let
    currentKey = key |> Key.fromString
    keyIsValid =
      case currentKey of
      Ok _ -> True
      -- while an empty key is not valid it's Ok to enter empty string as they will be converted in to Nothing
      Err Key.Empty -> True
      Err _ -> False
  in
  Form.form [ onSubmit (currentKey |> AssignKey) ]
    [ Grid.row [ Row.betweenXs ]
      [ Grid.col []
        [ Input.text
          [ Input.id "key"
          , Input.value key
          , Input.onInput UpdateKey
          , Input.placeholder "Choose a domain key"
          , if keyIsValid
            then Input.success
            else Input.danger
          ]
        , Form.help []
          [ text "You can hoose a unique, readable key among all domains and bounded contexts, to help you identify this domain!" ]
        , Form.invalidFeedback
          []
          [ text <| "The key is invalid: "  ++
            ( case currentKey of
                Err Key.StartsWithNumber -> "it must not start with a number"
                Err Key.ContainsWhitespace -> "it should not contain any whitespaces"
                Err (Key.ContainsSpecialChars chars) -> "it should not contain the following charachters: " ++ String.join " " (chars |> Set.toList |> List.map String.fromChar)
                _ -> ""
            )
          ]
        ]
      , Grid.col [ Col.sm3 ]
        [ Button.submitButton
          [ Button.primary
          , Button.disabled (keyIsValid |> not)
          ]
          [ text "Assign Key"]
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
            |> Maybe.withDefault (Html.p [ class "text-center", class "text-muted" ] [ Html.i [] [ text "This domain is not backed by any vision :-("] ])
          ]
        , Grid.col [ Col.sm3 ]
          [ Button.button [ Button.outlinePrimary, Button.onClick StartToChangeVision ] [ text "Refine Vision" ] ]
        ]
    displayKey =
      Grid.simpleRow
        [ Grid.col []
          [ model.domain
            |> Domain.key
            |> Maybe.map (\v -> Html.p [] [ text <| Key.toString v])
            |> Maybe.withDefault (Html.p [ class "text-muted", class "text-center" ] [Html.i [] [text "No key assigned" ] ] )
          ]
        , Grid.col [ Col.sm3 ]
          [ Button.button [ Button.outlinePrimary, Button.onClick StartToChangeKey ] [ text "Assign Key"] ]
        ]
  in
    ( case model.editDomain of
        Just (ChangingName name) ->
          [ viewEditName name
          , displayVision
          , displayKey
          ]
        Just (ChangingVision vision) ->
          [ displayDomain
          , viewEditVision vision
          , displayKey
          ]
        Just (ChangingKey key) ->
          [ displayDomain
          , displayVision
          , viewEditKey key
          ]
        _ ->
          [ displayDomain
          , displayVision
          , displayKey
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