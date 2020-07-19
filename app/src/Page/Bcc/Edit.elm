module Page.Bcc.Edit exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Html exposing (Html, div, text)
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
import Bootstrap.Text as Text
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
import BoundedContext.StrategicClassification as StrategicClassification
import BoundedContext.Canvas exposing (BoundedContextCanvas, BusinessDecisions, ModelTraits, UbiquitousLanguage)

import Page.ChangeKey as ChangeKey
import Page.Bcc.Edit.Dependencies as Dependencies
import Page.Bcc.Edit.Messages as Messages

-- MODEL

type alias CanvasModel =
  { boundedContext : BoundedContext.BoundedContext
  , canvas : BoundedContextCanvas
  }

type alias EditingCanvas =
  { edit : CanvasModel
    -- TODO: discuss we want this in edit or BCC - it's not persisted after all!
  , name : String
  , key : ChangeKey.Model
  , addingMessage : Messages.Model
  , addingDependencies : Dependencies.Model
  , problem : Maybe Problem
  }

type Problem
  = KeyProblem ChangeKey.KeyError
  | ContextProblem BoundedContext.Problem

type alias Model =
  { key: Nav.Key
  , self : Api.Configuration
  , edit: RemoteData.WebData EditingCanvas
  }

initWithCanvas : Api.Configuration -> CanvasModel -> (EditingCanvas, Cmd EditingMsg)
initWithCanvas config model =
  let
    (addingDependency, addingDependencyCmd) = Dependencies.init config model.boundedContext model.canvas.dependencies
    (changeKeyModel, changeKeyCmd) = ChangeKey.init config (model.boundedContext |> BoundedContext.key)
  in
    ( { addingMessage = Messages.init model.canvas.messages
      , addingDependencies = addingDependency
      , name = model.boundedContext |> BoundedContext.name
      , key = changeKeyModel
      , edit = model
      , problem = Nothing
      }
    , Cmd.batch
      [ addingDependencyCmd |> Cmd.map DependencyField
      , changeKeyCmd |> Cmd.map ChangeKeyMsg
      ]
    )

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
    , loadCanvas config contextId
    )

-- UPDATE

type Action t
  = Add t
  | Remove t

type StrategicClassificationMsg
  = SetDomainType StrategicClassification.DomainType
  | ChangeBusinessModel (Action StrategicClassification.BusinessModel)
  | SetEvolution StrategicClassification.Evolution

type FieldMsg
  = SetDescription String
  | ChangeStrategicClassification StrategicClassificationMsg
  | SetBusinessDecisions BusinessDecisions
  | SetUbiquitousLanguage UbiquitousLanguage
  | SetModelTraits ModelTraits

type EditingMsg
  = Field FieldMsg
  -- TODO the editing is actually part of the BoundedContext - move there or to the index page?!
  | SetName String
  | ChangeKeyMsg ChangeKey.Msg
  | DependencyField Dependencies.Msg
  | MessageField Messages.Msg

type Msg
  = Loaded (Result Http.Error CanvasModel)
  | Editing EditingMsg
  | Save
  | Saved (Result Http.Error ())

updateClassification : StrategicClassificationMsg -> StrategicClassification.StrategicClassification -> StrategicClassification.StrategicClassification
updateClassification msg classification =
  case msg of
    SetDomainType class ->
      { classification | domain = Just class}
    ChangeBusinessModel (Add business) ->
      { classification | business = business :: classification.business}
    ChangeBusinessModel (Remove business) ->
      { classification | business = classification.business |> List.filter (\bm -> bm /= business )}
    SetEvolution evo ->
      { classification | evolution = Just evo}

updateField : FieldMsg -> BoundedContextCanvas -> BoundedContextCanvas
updateField msg canvas =
  case msg of

    SetDescription description ->
      { canvas | description = description}

    ChangeStrategicClassification m ->
      { canvas | classification = updateClassification m canvas.classification }

    SetBusinessDecisions decisions ->
      { canvas | businessDecisions = decisions}
    SetUbiquitousLanguage language ->
      { canvas | ubiquitousLanguage = language}

    SetModelTraits traits ->
      { canvas | modelTraits = traits}

updateEdit : EditingMsg -> EditingCanvas -> (EditingCanvas, Cmd EditingMsg)
updateEdit msg model =
  case msg of
    MessageField messageMsg ->
      let
        updatedModel = Messages.update messageMsg model.addingMessage
      in
        ({ model | addingMessage = updatedModel }, Cmd.none)

    Field fieldMsg ->
      let
        canvasModel = model.edit
      in
        ( { model | edit = { canvasModel | canvas = updateField fieldMsg model.edit.canvas } }, Cmd.none)

    SetName name ->
      ({ model | name = name}, Cmd.none)

    ChangeKeyMsg changeMsg ->
      let
        (changeKeyModel, changeKeyCmd) = ChangeKey.update changeMsg model.key
        problem =
          case changeKeyModel.value of
            Ok _ -> Nothing
            Err e -> e |> KeyProblem |> Just
      in
        ( { model | key = changeKeyModel, problem = problem }
        , changeKeyCmd |> Cmd.map ChangeKeyMsg
        )

    DependencyField dependency ->
      let
        (addingDependencies, addingCmd) = Dependencies.update dependency model.addingDependencies
      in
        ( { model | addingDependencies = addingDependencies }
        , addingCmd |> Cmd.map DependencyField
        )

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case (msg, model.edit) of
    (Editing editing, RemoteData.Success editable) ->
      let
        (editModel, editCmd) = updateEdit editing editable
      in
        ({ model | edit = RemoteData.Success <| editModel }, editCmd |> Cmd.map Editing)
    (Save, RemoteData.Success editable) ->
      case
        editable.key.value
        |> Result.mapError KeyProblem
        |> Result.map (\k -> BoundedContext.changeKey k editable.edit.boundedContext)
        |> Result.andThen (BoundedContext.changeName editable.name >> Result.mapError ContextProblem)
      of
        Ok context ->
          let
            c = editable.edit.canvas
            canvas =
              { c
              | dependencies = editable.addingDependencies |> Dependencies.asDependencies
              , messages = editable.addingMessage |> Messages.asMessages
              }
          in
            (model, saveCanvas model.self context canvas)
        Err err ->
          let
            _ = Debug.log "error" err
          in (Debug.log "namechange" model, Cmd.none)

    (Saved (Ok _),_) ->
      (model, Cmd.none)

    (Loaded (Ok m), _) ->
      let
        (canvasModel, canvasCmd) = initWithCanvas model.self m
      in
        ({ model | edit = RemoteData.Success <| canvasModel }, canvasCmd |> Cmd.map Editing)
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
          [ viewCanvas edit |> Html.map Editing
          , Grid.row [ Row.attrs [ Spacing.mt3, Spacing.mb3 ] ]
            [ Grid.col [] [ Html.hr [] [] ] ]
          , viewActions edit
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
        [ text "Back" ]
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
    , Grid.col [ Col.textAlign Text.alignLgRight]
      [ Button.submitButton
        [ Button.primary
        , Button.onClick Save
        , Button.disabled
          ( (model.name |> BoundedContext.isNameValid |> not)
          || (model.problem |> Maybe.map (\_ -> True) |> Maybe.withDefault False)
          )
        ]
        [ text "Save"]
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
  let
    model = canvas.edit.canvas
  in
  List.concat
    [ [ Form.group []
        [ viewCaption "name" "Name"
        , Input.text
          [ Input.id "name", Input.value canvas.name, Input.onInput SetName
          , if canvas.name |> BoundedContext.isNameValid
            then Input.success
            else Input.danger
          ]
        , Form.help [] [ text "Naming is hard. Writing down the name of your context and gaining agreement as a team will frame how you design the context." ]
        , Form.invalidFeedback [] [ text "A name for a Bounded Context is required!" ]
        ]
      , Form.group []
        [ viewCaption "key" "Key"
        , ChangeKey.view canvas.key |> Html.map ChangeKeyMsg
        ]
      , Form.group []
        [ viewCaption "description" "Description"
        , Textarea.textarea
          [ Textarea.id "description"
          , Textarea.value model.description
          , Textarea.onInput (SetDescription >> Field)
          ]
        , Form.help [] [ text "A few sentences describing the why and what of the context in business language. No technical details here."] ]
      ]
    , [ model.classification
        |> viewStrategicClassification
        |> Html.map (ChangeStrategicClassification >> Field)
      ]
    , [ Form.group []
        [ viewCaption "businessDecisions" "Business Decisions"
          , Textarea.textarea
            [ Textarea.id "businessDecisions"
            , Textarea.rows 10
            , Textarea.value model.businessDecisions
            , Textarea.onInput SetBusinessDecisions
            ]
          , Form.help [] [ text "What are the key business rules and policies within this context?"]
        ]
      , Form.group []
          [ viewCaption "ubiquitousLanguage" "Ubiquitous Language"
            , Textarea.textarea
              [ Textarea.id "ubiquitousLanguage"
              , Textarea.rows 10
              , Textarea.value model.ubiquitousLanguage
              , Textarea.onInput SetUbiquitousLanguage
              ]
            , Form.help [] [ text "What are the key domain terms that exist within this context, and what do they mean?"]
          ]
      ]
      |> List.map (Html.map Field)
    ]

viewRightside : EditingCanvas -> List (Html EditingMsg)
viewRightside model =
  [ viewModelTraits model.edit.canvas |> Html.map Field
  , model.addingMessage |> Messages.view |> Html.map MessageField
  , model.addingDependencies |> Dependencies.view |> Html.map DependencyField
  ]

viewStrategicClassification : StrategicClassification.StrategicClassification -> Html StrategicClassificationMsg
viewStrategicClassification model =
  let
    domainDescriptions =
      [ StrategicClassification.Core, StrategicClassification.Supporting, StrategicClassification.Generic ]
      |> List.map StrategicClassification.domainDescription
      |> List.map (\d -> (d.name, d.description))
    businessDescriptions =
      [ StrategicClassification.Revenue, StrategicClassification.Engagement, StrategicClassification.Compliance, StrategicClassification.CostReduction ]
      |> List.map StrategicClassification.businessDescription
      |> List.map (\d -> (d.name, d.description))
    evolutionDescriptions =
      [ StrategicClassification.Genesis, StrategicClassification.CustomBuilt, StrategicClassification.Product, StrategicClassification.Commodity ]
      |> List.map StrategicClassification.evolutionDescription
      |> List.map (\d -> (d.name, d.description))
  in
  Form.group []
    [ Grid.row []
      [ Grid.col [] [ viewCaption "" "Strategic Classification"]]
    , Grid.row []
      [ Grid.col []
        [ viewLabel "classification" "Domain"
        , div []
            ( Radio.radioList "classification"
              [ viewRadioButton "core" model.domain StrategicClassification.Core SetDomainType StrategicClassification.domainDescription
              , viewRadioButton "supporting" model.domain StrategicClassification.Supporting SetDomainType StrategicClassification.domainDescription
              , viewRadioButton "generic" model.domain StrategicClassification.Generic SetDomainType StrategicClassification.domainDescription
              -- TODO: Other
              ]
            )
          , viewDescriptionList domainDescriptions Nothing
            |> viewInfoTooltip "How important is this context to the success of your organisation?"
          ]
        , Grid.col []
          [ viewLabel "businessModel" "Business Model"
          , div []
              [ viewCheckbox "revenue" StrategicClassification.businessDescription StrategicClassification.Revenue model.business
              , viewCheckbox "engagement" StrategicClassification.businessDescription StrategicClassification.Engagement model.business
              , viewCheckbox "Compliance" StrategicClassification.businessDescription StrategicClassification.Compliance model.business
              , viewCheckbox "costReduction" StrategicClassification.businessDescription StrategicClassification.CostReduction model.business
              -- TODO: Other
              ]
              |> Html.map ChangeBusinessModel

          , viewDescriptionList businessDescriptions Nothing
            |> viewInfoTooltip "What role does the context play in your business model?"
          ]
        , Grid.col []
          [ viewLabel "evolution" "Evolution"
          , div []
              ( Radio.radioList "evolution"
                [ viewRadioButton "genesis" model.evolution StrategicClassification.Genesis SetEvolution StrategicClassification.evolutionDescription
                , viewRadioButton "customBuilt" model.evolution StrategicClassification.CustomBuilt SetEvolution StrategicClassification.evolutionDescription
                , viewRadioButton "product" model.evolution StrategicClassification.Product SetEvolution StrategicClassification.evolutionDescription
                , viewRadioButton "commodity" model.evolution StrategicClassification.Commodity SetEvolution StrategicClassification.evolutionDescription
                -- TODO: Other
                ]
              )
            , viewDescriptionList evolutionDescriptions Nothing
            |> viewInfoTooltip "How evolved is the concept (see Wardley Maps)"
          ]
      ]
    ]

viewModelTraits : BoundedContextCanvas -> Html FieldMsg
viewModelTraits model =
  let
    traits =
      [ ("Specification Model", "Produces a document describing a job/request that needs to be performed. Example: Advertising Campaign Builder")
      , ("Execution Model", "Performs or tracks a job. Example: Advertising Campaign Engine")
      , ("Audit Model", "Monitors the execution. Example: Advertising Campaign Analyser")
      , ("Approver", "Receives requests and determines if they should progress to the next step of the process. Example: Fraud Check")
      , ("Enforcer", "Ensures that other contexts carry out certain operations. Example: GDPR Context (ensures other contexts delete all of a user’s data)")
      , ("Octopus Enforcer", "Ensures that multiple/all contexts in the system all comply with a standard rule. Example: GDPR Context (as above)")
      , ("Interchanger", "Translates between multiple ubiquitous languages.")
      , ("Gateway", "Sits at the edge of a system and manages inbound and/or outbound communication. Example: IoT Message Gateway")
      , ("Gateway Interchange", "The combination of a gateway and an interchange.")
      , ("Dogfood Context", "Simulates the customer experience of using the core bounded contexts. Example: Whitelabel music store")
      , ("Bubble Context", "Sits in-front of legacy contexts providing a new, cleaner model while legacy contexts are being replaced.")
      , ("Autonomous Bubble", "Bubble context which has its own data store and synchronises data asynchronously with the legacy contexts.")
      , ("Brain Context (likely anti-pattern)", "Contains a large number of important rules and many other contexts depend on it. Example: rules engine containing all the domain rules")
      , ("Funnel Context", "Receives documents from multiple upstream contexts and passes them to a single downstream context in a standard format (after applying its own rules).")
      , ("Engagement Context", "Provides key features which attract users to keep using the product. Example: Free Financial Advice Context")
      ]
  in
    Form.group []
      [ viewCaption "modelTraits" "Model traits"
      , Input.text
        [ Input.id "modelTraits"
        , Input.value model.modelTraits
        , Input.onInput SetModelTraits
        ]
    , viewDescriptionList traits (Just "https://github.com/ddd-crew/bounded-context-canvas/blob/master/resources/model-traits-worksheet.md")
      |> viewInfoTooltip "How can you characterise the behaviour of this bounded context?"
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

viewLabel : String -> String -> Html msg
viewLabel labelId caption =
  Form.label
    [ for labelId ]
    [ Html.b [] [ text caption ] ]

viewRadioButton : String  -> Maybe value -> value -> (value -> m) -> (value -> StrategicClassification.Description) -> Radio.Radio m
viewRadioButton id currentValue option toMsg toTitle =
  Radio.createAdvanced
    [ Radio.id id, Radio.onClick (toMsg option), Radio.checked (currentValue == Just option) ]
    (Radio.label [] [ text (toTitle option).name ])

viewCheckbox : String -> (value -> StrategicClassification.Description) -> value -> List value -> Html (Action value)
viewCheckbox id description value currentValues =
  Checkbox.checkbox
    [Checkbox.id id
    , Checkbox.onCheck(\isChecked -> if isChecked then Add value else Remove value )
    , Checkbox.checked (List.member value currentValues)
    ]
    (description value).name

viewInfoTooltip : String -> Html msg -> Html msg
viewInfoTooltip title description =
  Form.help []
    [ Html.details []
      [ Html.summary []
        [ text title ]
      , Html.p [ ] [ description ]
      ]
    ]

viewDescriptionList : List (String, String) -> Maybe String -> Html msg
viewDescriptionList model sourceReference =
  let
    footer =
      case sourceReference of
        Just reference ->
          [ Html.footer
            [ class "blockquote-footer"]
            [ Html.a
              [target "_blank"
              , href reference
              ]
              [ text "Source of the descriptions"]
            ]
          ]
        Nothing -> []
  in
    Html.dl []
      ( model
        |> List.concatMap (
          \(t, d) ->
            [ Html.dt [] [ text t ]
            , Html.dd [] [ text d ]
            ]
        )
      )
    :: footer
    |> div []


-- HTTP

loadCanvas: Api.Configuration -> BoundedContextId -> Cmd Msg
loadCanvas config contextId =
  let
    decoder =
      Decode.succeed CanvasModel
      |> JP.custom BoundedContext.modelDecoder
      |> JP.custom BoundedContext.Canvas.modelDecoder
  in Http.get
    { url = Api.boundedContext contextId |> Api.url config |> Url.toString
    , expect = Http.expectJson Loaded decoder
    }

saveCanvas : Api.Configuration -> BoundedContext.BoundedContext -> BoundedContextCanvas -> Cmd Msg
saveCanvas config context canvas =
  Http.request
    { method = "PATCH"
    , headers = []
    , url =
      context
      |> BoundedContext.id
      |> Api.boundedContext
      |> Api.url config
      |> Url.toString
    , body = Http.jsonBody <| BoundedContext.Canvas.modelEncoder context canvas
    , expect = Http.expectWhatever Saved
    , timeout = Nothing
    , tracker = Nothing
    }
