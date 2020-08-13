module Page.Bcc.Technical exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Button as Button
import Bootstrap.Text as Text
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Display as Display

import RemoteData
import Url
import Http

import Json.Decode as Decode
import Json.Decode.Pipeline as JP

import Api exposing(ApiResponse)
import Route

import Key
import BoundedContext exposing (BoundedContext)
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import BoundedContext.Technical as Technical exposing(TechnicalDescription)

type alias LifecycleModel =
  { issueTracker : String
  , wiki : String
  , repository : String
  }

type alias DeploymentModel =
  { healthCheck : String
  , artifacts : String
  }

type alias DescriptionModel =
  { model : TechnicalModel
  , lifecycleTools : LifecycleModel
  , deployment : DeploymentModel 
  }

type alias TechnicalModel =
  { context : BoundedContext
  , description : TechnicalDescription
  }

type alias Model = 
  { key: Nav.Key
  , self : Api.Configuration
  , edit: RemoteData.WebData DescriptionModel
  }

urlAsEmptyString url =
  url
  |> Maybe.map Url.toString
  |> Maybe.withDefault ""

initDescriptionEdit : TechnicalModel -> DescriptionModel 
initDescriptionEdit model =
  { model = model
  , lifecycleTools = 
    { issueTracker = model.description.tools.issueTracker |> urlAsEmptyString
    , wiki = model.description.tools.wiki |> urlAsEmptyString
    , repository = model.description.tools.repository |> urlAsEmptyString
    }
  , deployment =
    { healthCheck = model.description.deployment.healthCheck |> urlAsEmptyString
    , artifacts = model.description.deployment.artifacts |> urlAsEmptyString
    }
  }


init : Nav.Key -> Api.Configuration -> BoundedContextId -> (Model, Cmd Msg)
init key config contextId =
  let
    model =
      { key = key
      , self = config
      , edit = RemoteData.Loading
      }
  in
    (
      model
    , loadTechnical config contextId
    )


type Msg
  = Loaded (ApiResponse TechnicalModel)
  | Save
  | Saved (ApiResponse ())
  | ChangeIssueTracker String
  | ChangeWiki String
  | ChangeRepository String

updateEdit : (DescriptionModel -> DescriptionModel) -> Model -> Model
updateEdit action model  =
  { model | edit = model.edit |> RemoteData.map action }

updateLifecycle : (LifecycleModel -> LifecycleModel) -> DescriptionModel -> DescriptionModel
updateLifecycle action model =
  { model | lifecycleTools = action model.lifecycleTools }

update : Msg -> Model ->  ( Model, Cmd Msg )
update msg model =
  case msg of
    Loaded result ->
      ( { model | edit = RemoteData.fromResult result |> RemoteData.map initDescriptionEdit }, Cmd.none )
    
    Save ->
      case model.edit of
        RemoteData.Success edit ->
          let
            description = 
              { tools =
                { issueTracker = Url.fromString edit.lifecycleTools.issueTracker
                , wiki = Url.fromString edit.lifecycleTools.wiki
                , repository = Url.fromString edit.lifecycleTools.repository
                }
              , deployment =
                { healthCheck = Url.fromString edit.deployment.healthCheck
                , artifacts = Url.fromString edit.deployment.artifacts
                }

              }    
          in
            ( model, saveTechnical model.self (edit.model.context |> BoundedContext.id) description )
        _ ->
          ( model, Cmd.none)
    
    Saved _ ->
      ( model, Cmd.none)

    ChangeIssueTracker url ->
      ( model |> updateEdit (\m -> m |> updateLifecycle (\l -> {l | issueTracker = url})), Cmd.none)

    ChangeWiki url ->
      ( model |> updateEdit (\m -> m |> updateLifecycle (\l -> {l | wiki = url})), Cmd.none)
    
    ChangeRepository url ->
      ( model |> updateEdit (\m -> m |> updateLifecycle (\l -> {l | repository = url})), Cmd.none)


view : Model -> Html Msg
view model =
  let
    details =
      case model.edit of
        RemoteData.Success edit ->
          [ Grid.simpleRow
            [ Grid.col []
                [ Button.linkButton
                  [ Button.attrs [ href (edit.model.context |> BoundedContext.domain |> Route.Domain |> Route.routeToString ) ], Button.roleLink ]
                  [ text "Back" ]
                ]
            ]
          , viewTechnical edit
          , Grid.row [ Row.attrs [ Spacing.mt3, Spacing.mb3 ] ]
            [ Grid.col [] [ Html.hr [] [] ] ]
          -- , viewActions edit
          ]
        _ ->
          [ Grid.row []
            [ Grid.col [] [ text "Loading Bounded Context details..."]]
          ]
  in
    Grid.container [] details

urlInput : String -> String -> String -> String -> (String -> Msg) -> Html Msg
urlInput name description helpText value cmd =
  let
    urlValid maybeUrl =
      case Url.fromString maybeUrl of
        Just _ -> Input.success
        Nothing ->
          if String.isEmpty maybeUrl
          then Input.success
          else Input.danger
  in
    Form.group []
      [ Form.label [ for name] [ text description ]
      , Input.url 
        [ Input.id name
        , Input.value value
        , urlValid value
        , Input.onInput cmd ]
      , Form.help [] [ text helpText ]
      ]
  

viewLifecycle : LifecycleModel -> List (Html Msg)
viewLifecycle model =
  [ urlInput "issueTracker" "Issue Tracker" "A link to the issue tracker" model.issueTracker ChangeIssueTracker
  , urlInput "wiki" "Wiki" "A link to the wiki / documentation" model.wiki ChangeWiki
  , urlInput "repository" "Repository" "A link to the source code repository" model.repository ChangeRepository
  ]

viewTechnical : DescriptionModel -> Html Msg
viewTechnical { model, lifecycleTools, deployment } =
  Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
    |> Card.block []
      [ Block.titleH4 []
        [ text <| "Change technical description for " ++ (model.context |> BoundedContext.name)
        , Html.small [ class "text-muted", class "float-right" ]
          [ text (model.context |> BoundedContext.key |> Maybe.map Key.toString |> Maybe.withDefault "") ]
        ]
      ]
    |> Card.block []
      [ Block.custom (
          Form.form []
            [ Fieldset.config
              |> Fieldset.asGroup
              |> Fieldset.legend [] [ text "Lifecyle tools"]
              |> Fieldset.children (viewLifecycle lifecycleTools)
              |> Fieldset.view
            ]
        )
      ]
    |> Card.footer []
      [ Grid.simpleRow
        [ Grid.col [ Col.offsetMd9, Col.md3, Col.attrs [ class "text-right" ] ]
          [ Button.button
            [ Button.primary
            , Button.onClick Save
            ]
            [ text "Save"]
          ]
        ]
      ]
    |> Card.view

loadTechnical: Api.Configuration -> BoundedContextId -> Cmd Msg
loadTechnical config contextId =
  let
    decoder =
      Decode.succeed TechnicalModel
      |> JP.custom BoundedContext.modelDecoder
      |> JP.custom Technical.modelDecoder
  in Http.get
    { url = Api.boundedContext contextId |> Api.url config |> Url.toString
    , expect = Http.expectJson Loaded decoder
    }

saveTechnical : Api.Configuration -> BoundedContextId -> TechnicalDescription -> Cmd Msg
saveTechnical config contextId model =
  Http.request
    { method = "PATCH"
    , headers = []
    , url =
      contextId
      |> Api.boundedContext
      |> Api.url config
      |> Url.toString
    , body = Http.jsonBody <| Technical.modelEncoder model
    , expect = Http.expectWhatever Saved
    , timeout = Nothing
    , tracker = Nothing
    }
