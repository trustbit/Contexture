module Page.Bcc.CanvasV3 exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Button as Button
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Display as Display

import RemoteData
import Url
import Http

import Json.Decode as Decode
import Json.Decode.Pipeline as JP

import Api
import Route

import BoundedContext
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import BoundedContext.Canvas exposing (BoundedContextCanvas)


import Page.Bcc.Edit.Dependencies as Dependencies
import Page.Bcc.Edit.Messages as Messages
import Page.Bcc.Edit.UbiquitousLanguage as UbiquitousLanguage
import Page.Bcc.Edit.StrategicClassification as StrategicClassification
import Page.Bcc.Edit.Description as Description
import Page.Bcc.Edit.Name as Name
import Page.Bcc.Edit.ShortName as ShortName
import Page.Bcc.Edit.BusinessDecision as BusinessDecisionView
import Page.Bcc.Edit.DomainRoles as DomainRolesView
import Domain


-- MODEL

type alias CanvasModel =
  { boundedContext : BoundedContext.BoundedContext
  , canvas : BoundedContextCanvas
  , domain : Domain.Domain
  }

type alias EditingCanvas =
  { edit : CanvasModel
    -- TODO: discuss we want this in edit or BCC - it's not persisted after all!
  , name : Name.Model
  , shortName : ShortName.Model
  , addingMessage : Messages.Model
  , addingDependencies : Dependencies.Model
  , ubiquitousLanguage : UbiquitousLanguage.Model
  , businessDecisions : BusinessDecisionView.Model
  , domainRoles : DomainRolesView.Model
  , classification : StrategicClassification.Model
  , description : Description.Model
  }

type alias Model =
  { shortName: Nav.Key
  , self : Api.Configuration
  , edit: RemoteData.WebData EditingCanvas
  }

initWithCanvas : Api.Configuration -> CanvasModel -> (EditingCanvas, Cmd EditingMsg)
initWithCanvas config model =
  let
    (addingDependency, addingDependencyCmd) = Dependencies.init config model.boundedContext
    (changeShortNameModel, changeShortNameCmd) = ShortName.init config model.boundedContext
    (ubiquitousLanguageModel, ubiquitousLanguageCmd) = UbiquitousLanguage.init config (model.boundedContext |> BoundedContext.id)
    (businessDecisionsModel, businessDecisionsCmd) = BusinessDecisionView.init config (model.boundedContext |> BoundedContext.id)
    (domainRolesModel, domainRolesCmd) = DomainRolesView.init config (model.boundedContext |> BoundedContext.id)
    (classificationModel, classificationCmd) = StrategicClassification.init config (model.boundedContext |> BoundedContext.id) model.canvas.classification
    (descriptionModel, descriptionCmd) = Description.init config (model.boundedContext |> BoundedContext.id) model.canvas.description
    (messagesModel, messagesCmd) = Messages.init config (model.boundedContext |> BoundedContext.id) model.canvas.messages
    (nameModel, nameCmd) = Name.init config model.boundedContext
  in
    ( { addingMessage = messagesModel
      , addingDependencies = addingDependency
      , ubiquitousLanguage = ubiquitousLanguageModel
      , name = nameModel
      , shortName = changeShortNameModel
      , edit = model
      , businessDecisions = businessDecisionsModel
      , domainRoles = domainRolesModel
      , classification = classificationModel
      , description = descriptionModel
      }
    , Cmd.batch
      [ addingDependencyCmd |> Cmd.map DependencyField
      , changeShortNameCmd |> Cmd.map ShortNameField
      , domainRolesCmd |> Cmd.map DomainRolesField
      , ubiquitousLanguageCmd |> Cmd.map UbiquitousLanguageField
      , businessDecisionsCmd |> Cmd.map BusinessDecisionField
      , classificationCmd |> Cmd.map StrategicClassificationField
      , descriptionCmd |> Cmd.map DescriptionField
      , messagesCmd |> Cmd.map MessageField
      , nameCmd |> Cmd.map NameField
      ]
    )

init : Nav.Key -> Api.Configuration -> BoundedContextId -> (Model, Cmd Msg)
init shortName config contextId =
  let
    model =
      { shortName = shortName
      , self = config
      , edit = RemoteData.Loading
      }
  in
    (
      model
    , loadCanvas config contextId
    )

-- UPDATE

type EditingMsg
  = DescriptionField Description.Msg
  -- TODO the editing is actually part of the BoundedContext - move there or to the index page?!
  | NameField Name.Msg
  | ShortNameField ShortName.Msg
  | DependencyField Dependencies.Msg
  | MessageField Messages.Msg
  | UbiquitousLanguageField UbiquitousLanguage.Msg
  | BusinessDecisionField BusinessDecisionView.Msg
  | DomainRolesField DomainRolesView.Msg
  | StrategicClassificationField StrategicClassification.Msg

type Msg
  = Loaded (Result Http.Error CanvasModel)
  | Editing EditingMsg


updateEdit : EditingMsg -> EditingCanvas -> (EditingCanvas, Cmd EditingMsg)
updateEdit msg model =
  case msg of
    MessageField messageMsg ->
      Messages.update messageMsg model.addingMessage
      |> Tuple.mapFirst(\m -> { model | addingMessage = m})
      |> Tuple.mapSecond(Cmd.map MessageField)

    DescriptionField desMsg ->
      Description.update desMsg model.description
      |> Tuple.mapFirst(\m -> { model | description = m})
      |> Tuple.mapSecond(Cmd.map DescriptionField)

    NameField nameMsg ->
      Name.update nameMsg model.name
      |> Tuple.mapFirst(\m -> { model | name = m})
      |> Tuple.mapSecond(Cmd.map NameField)

    UbiquitousLanguageField languageMsg ->
      UbiquitousLanguage.update languageMsg model.ubiquitousLanguage
      |> Tuple.mapFirst(\m -> { model | ubiquitousLanguage = m})
      |> Tuple.mapSecond(Cmd.map UbiquitousLanguageField)

    BusinessDecisionField businessDecisionMsg ->
      BusinessDecisionView.update businessDecisionMsg model.businessDecisions
      |> Tuple.mapFirst(\d -> { model | businessDecisions = d })
      |> Tuple.mapSecond(Cmd.map BusinessDecisionField)

    DomainRolesField domainRolesMsg ->
      DomainRolesView.update domainRolesMsg model.domainRoles
      |> Tuple.mapFirst(\d -> { model | domainRoles = d })
      |> Tuple.mapSecond(Cmd.map DomainRolesField)

    StrategicClassificationField scMsg ->
      StrategicClassification.update scMsg model.classification
      |> Tuple.mapFirst(\d -> { model | classification = d })
      |> Tuple.mapSecond(Cmd.map StrategicClassificationField)

    ShortNameField changeMsg ->
      ShortName.update changeMsg model.shortName
      |> Tuple.mapFirst(\d -> { model | shortName = d })
      |> Tuple.mapSecond(Cmd.map ShortNameField)

    DependencyField dependency ->
      Dependencies.update dependency model.addingDependencies
      |> Tuple.mapFirst(\d -> { model | addingDependencies = d })
      |> Tuple.mapSecond(Cmd.map DependencyField)


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case (msg, model.edit) of
    (Editing editing, RemoteData.Success editable) ->
      updateEdit editing editable
      |> Tuple.mapFirst(\d -> { model | edit = RemoteData.Success <| d })
      |> Tuple.mapSecond(Cmd.map Editing)

    (Loaded (Ok m), _) ->
      initWithCanvas model.self m
      |> Tuple.mapFirst(\d -> { model | edit = RemoteData.Success <| d })
      |> Tuple.mapSecond(Cmd.map Editing)

    (Loaded (Err e),_) ->
      ({ model | edit = RemoteData.Failure e } , Cmd.none)

    _ ->
      let
        _ = Debug.log "bcc msg" msg
      in
      (Debug.log "bcc model" model, Cmd.none)

-- VIEW

view : Model -> Html Msg
view model =
  let
    details =
      case model.edit of
        RemoteData.Success edit ->
          [ viewActions edit
          , Grid.row [ Row.attrs [ Spacing.mt1, Spacing.mb1 ] ]
            [ Grid.col [] [ Html.hr [] [] ] ]
          , viewCanvas edit |> Html.map Editing
          ]
        _ ->
          [ Grid.row []
            [ Grid.col [] [ text "Loading Bounded Context details..."]]
          ]
  in
    Grid.containerFluid [] details

viewActions : EditingCanvas -> Html Msg
viewActions model =
  Grid.row []
    [ Grid.col []
      [ Button.linkButton
        [ Button.roleLink
        , Button.attrs
          [ href
            ( model.edit.boundedContext
              |> BoundedContext.domain
              |> Route.Domain
              |> Route.routeToString
            )
          ]
        ]
        [ text ("Back to Domain '" ++ (model.edit.domain |> Domain.name) ++ "'") ]
      ]
    , Grid.col []
      [ Button.linkButton
        [ Button.roleLink
        , Button.attrs
          [ target "_blank"
          , href "https://github.com/ddd-crew/bounded-context-canvas"
          , class "text-muted"
          ]
        ]
        [ text "Source of the descriptions & help text"]
      ]
    ]

viewCanvas : EditingCanvas -> Html EditingMsg
viewCanvas model =
  Grid.row []
    [ Grid.col [] (viewLeftside model)
    , Grid.col [] (viewRightside model)
    ]

viewLeftside : EditingCanvas -> List (Html EditingMsg)
viewLeftside canvas =
  [ canvas.name
    |> Name.view
    |> Html.map NameField
  , canvas.shortName
    |> ShortName.view
    |> Html.map ShortNameField
  , canvas.description
    |> Description.view
    |> Html.map DescriptionField
  , canvas.classification
    |> StrategicClassification.view
    |> Html.map StrategicClassificationField
  , Form.group []
    [ viewCaption "businessDecisions" "Business Decisions"
      , Form.help [] [ text "What are the key business rules and policies within this context?"]
      , BusinessDecisionView.view canvas.businessDecisions |> Html.map BusinessDecisionField
    ]
  , Form.group []
      [ viewCaption "ubiquitousLanguage" "Ubiquitous Language"
      , Form.help [] [ text "What are the key domain terms that exist within this context, and what do they mean?" ]
      , UbiquitousLanguage.view canvas.ubiquitousLanguage |> Html.map UbiquitousLanguageField
      ]
  ]


viewRightside : EditingCanvas -> List (Html EditingMsg)
viewRightside model =
  [ Form.group []
      [ viewCaption "domainRoles" "Domain Roles"
        , Form.help [] [ text "How can you characterise the behaviour of this bounded context?"]
        , DomainRolesView.view model.domainRoles |> Html.map DomainRolesField
      ]
  , model.addingMessage |> Messages.view |> Html.map MessageField
  , model.addingDependencies |> Dependencies.view |> Html.map DependencyField
  ]

-- view utilities

viewCaption : String -> String -> Html msg
viewCaption labelId caption =
  Form.label
    [ for labelId
    , Display.block
    , style "background-color" "lightGrey"
    , Spacing.p2
    ]
    [ text caption ]


-- HTTP

loadCanvas: Api.Configuration -> BoundedContextId -> Cmd Msg
loadCanvas config contextId =
  let
    decoder =
      Decode.succeed CanvasModel
      |> JP.custom BoundedContext.modelDecoder
      |> JP.custom BoundedContext.Canvas.modelDecoder
      |> JP.requiredAt [ "domain" ] Domain.domainDecoder
  in Http.get
    { url = Api.canvas contextId |> Api.url config 
    , expect = Http.expectJson Loaded decoder
    }
