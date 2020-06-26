module Bcc.Edit exposing (Msg, Model, update, view, init)

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
import Bootstrap.Text as Text
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Display as Display

import RemoteData
import Url
import Http

import Route

import Domain
import Bcc
import Bcc.Edit.Dependencies as Dependencies
import Bcc.Edit.Messages as Messages

-- MODEL

type alias EditingCanvas =
  { canvas : Bcc.BoundedContextCanvas
  , addingMessage : Messages.Model
  , addingDependencies : Dependencies.Model
  }

type alias Model =
  { key: Nav.Key
  , self: Url.Url
  -- TODO: discuss we want this in edit or BCC - it's not persisted after all!
  , edit: RemoteData.WebData EditingCanvas
  }

initWithCanvas : Url.Url -> Bcc.BoundedContextCanvas -> (EditingCanvas, Cmd EditingMsg)
initWithCanvas url canvas =
  let
    (addingDependency, addingDependencyCmd) = Dependencies.init url canvas.dependencies
  in
    ( { addingMessage = Messages.init canvas.messages
      , addingDependencies = addingDependency
      , canvas = canvas
      }
    , addingDependencyCmd |> Cmd.map DependencyField
    )

init : Nav.Key -> Url.Url -> (Model, Cmd Msg)
init key url =
  let
    model =
      { key = key
      , self = url
      , edit = RemoteData.Loading
      }
  in
    (
      model
    , loadBCC model
    )

-- UPDATE

type EditingMsg
  = Field Bcc.Msg
  | MessageField Messages.Msg
  | DependencyField Dependencies.Msg

type Msg
  = Loaded (Result Http.Error Bcc.BoundedContextCanvas)
  | Editing EditingMsg
  | Save
  | Saved (Result Http.Error ())
  | Delete
  | Deleted (Result Http.Error ())

updateEdit : EditingMsg -> EditingCanvas -> (EditingCanvas, Cmd EditingMsg)
updateEdit msg model =
  case msg of
    MessageField messageMsg ->
      let
        updatedModel = Messages.update messageMsg model.addingMessage
        canvas = model.canvas
        c = { canvas | messages = updatedModel.messages}
      in
        ({ model | addingMessage = updatedModel, canvas = c }, Cmd.none)
    Field fieldMsg ->
      ({ model | canvas = Bcc.update fieldMsg model.canvas }, Cmd.none)
    DependencyField dependency ->
      let
        (addingDependencies, addingCmd) = Dependencies.update dependency model.addingDependencies
        canvas = model.canvas
        c =
          { canvas |
            dependencies =
              { consumers = addingDependencies.consumer.existingDependencies
              , suppliers = addingDependencies.supplier.existingDependencies
              }
          }
      in
        ({ model | canvas = c, addingDependencies = addingDependencies }, addingCmd |> Cmd.map DependencyField)


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case (msg, model.edit) of
    (Editing editing, RemoteData.Success editable) ->
      let
        (editModel, editCmd) = updateEdit editing editable
      in
        ({ model | edit = RemoteData.Success <| editModel }, editCmd |> Cmd.map Editing)
    (Save, RemoteData.Success editable) ->
      (model, saveBCC model.self editable)
    (Saved (Ok _),_) ->
      (model, Cmd.none)
    (Delete,_) ->
      (model, deleteBCC model)
    (Deleted (Ok _), RemoteData.Success editable) ->
      (model, Route.pushUrl (Route.Domain editable.canvas.domain) model.key)
    (Loaded (Ok m), _) ->
      let
        (canvasModel, canvasCmd) = initWithCanvas model.self m
      in
        ({ model | edit = RemoteData.Success <| canvasModel }, canvasCmd |> Cmd.map Editing)
    (Loaded (Err e),_) ->
      ({ model | edit = RemoteData.Failure e } , Cmd.none)
    _ ->
      Debug.log ("BCC: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)

-- VIEW

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

view : Model -> Html Msg
view model =
  let
    details =
      case model.edit of
        RemoteData.Success edit ->
          [ viewCanvas edit |> Html.map Editing
          , Grid.row [ Row.attrs [ Spacing.mt3, Spacing.mb3 ] ]
            [ Grid.col [] [ Html.hr [] [] ] ]
          , Grid.row []
            [ Grid.col []
              [ Button.linkButton
                [ Button.roleLink
                , Button.attrs [ href (Route.routeToString (Route.Domain edit.canvas.domain)) ]
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
              [ Button.button
                [ Button.secondary
                , Button.onClick Delete
                , Button.attrs
                  [ title ("Delete " ++ edit.canvas.name)
                  , Spacing.mr3
                  ]
                ]
                [ text "Delete" ]
              , Button.submitButton
                [ Button.primary
                , Button.onClick Save
                , Button.disabled (edit.canvas.name |> Bcc.ifNameValid (\_ -> True) (\_ -> False))
                ]
                [ text "Save"]
              ]
            ]
          ]
        _ ->
          [ Grid.row []
            [ Grid.col [] [ text "Loading Bounded Context details..."]]
          ]
  in
    Grid.containerFluid [] details

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
  List.concat
    [ [ Html.dl []
        (model
          |> List.concatMap (
            \(t, d) ->
              [ Html.dt [] [ text t ]
              , Html.dd [] [ text d ]
              ]
          )
        )
      ]
    , footer
    ]
  |> div []


viewRadioButton : String  -> Bool -> m -> Html m -> Radio.Radio m
viewRadioButton id checked msg title =
  Radio.createAdvanced
    [ Radio.id id, Radio.onClick msg, Radio.checked checked ]
    (Radio.label [] [ title ])

viewCheckbox : String -> String -> value -> List value -> Html (Bcc.Action value)
viewCheckbox id title value currentValues =
  Checkbox.checkbox
    [Checkbox.id id
    , Checkbox.onCheck(\isChecked -> if isChecked then Bcc.Add value else Bcc.Remove value )
    , Checkbox.checked (List.member value currentValues)
    ]
    title

viewStrategicClassification : Bcc.StrategicClassification -> List (Html Bcc.StrategicClassificationMsg)
viewStrategicClassification model =
  let
    domainDescriptions =
      [ ("Core domain", "A key strategic initiative")
      , ("Supporting domain", "Necessary but not a differentiator")
      , ("Generic","a common capability found in many domains")
      ]
    businessDescriptions =
      [ ("Revenue", "People pay directly for this")
      , ("Engagement","Users like it but they don't pay for it")
      , ("Compliance", "Protects your business reputation and existence")
      , ("Cost reduction", "Helps your business to reduce cost or effort")
      ]
    evolutionDescriptions =
      [ ("Genesis", "New unexplored domain")
      , ("Custom built", "Companies are building their own versions")
      , ("Product", "Off-the-shelf versions exist with differentiation")
      , ("Commodity", "Highly-standardised versions exist")
      ]
  in
  [ Grid.row []
      [ Grid.col [] [ viewCaption "" "Strategic Classification"]]
  , Grid.row []
      [ Grid.col []
        [ viewLabel "classification" "Domain"
        , div []
            ( Radio.radioList "classification"
              [ viewRadioButton "core" (model.domain == Just Bcc.Core) (Bcc.SetDomainType Bcc.Core) (text "Core")
              , viewRadioButton "supporting" (model.domain == Just Bcc.Supporting) (Bcc.SetDomainType Bcc.Supporting)  (text "Supporting")
              , viewRadioButton "generic" (model.domain == Just Bcc.Generic) (Bcc.SetDomainType Bcc.Generic)  (text "Generic")
              -- TODO: Other
              ]
            )
          , viewDescriptionList domainDescriptions Nothing
            |> viewInfoTooltip "How important is this context to the success of your organisation?"
          ]
        , Grid.col []
          [ viewLabel "businessModel" "Business Model"
          , div []
              [ viewCheckbox "revenue" "Revenue" Bcc.Revenue model.business
              , viewCheckbox "engagement" "Engagement" Bcc.Engagement model.business
              , viewCheckbox "Compliance" "Compliance" Bcc.Compliance model.business
              , viewCheckbox "costReduction" "Cost reduction" Bcc.CostReduction model.business
              -- TODO: Other
              ]
              |> Html.map Bcc.ChangeBusinessModel

          , viewDescriptionList businessDescriptions Nothing
            |> viewInfoTooltip "What role does the context play in your business model?"
          ]
        , Grid.col []
          [ viewLabel "evolution" "Evolution"
          , div []
              ( Radio.radioList "evolution"
                [ viewRadioButton "genesis" (model.evolution == Just Bcc.Genesis) (Bcc.SetEvolution Bcc.Genesis) (text "Genesis")
                , viewRadioButton "customBuilt" (model.evolution == Just Bcc.CustomBuilt) (Bcc.SetEvolution Bcc.CustomBuilt) (text "Custom built")
                , viewRadioButton "product" (model.evolution == Just Bcc.Product) (Bcc.SetEvolution Bcc.Product) (text "Product")
                , viewRadioButton "commodity" (model.evolution == Just Bcc.Commodity) (Bcc.SetEvolution Bcc.Commodity) (text "Commodity")
                -- TODO: Other
                ]
              )

            , viewDescriptionList evolutionDescriptions Nothing
            |> viewInfoTooltip "How evolved is the concept (see Wardley Maps)"
          ]
      ]
  ]


viewLeftside : EditingCanvas -> List (Html EditingMsg)
viewLeftside canvas =
  let
    model = canvas.canvas
  in
  List.concat
    [ [ Form.group []
        [ viewCaption "name" "Name"
        , Input.text (
            List.concat
            [ [ Input.id "name", Input.value model.name, Input.onInput Bcc.SetName ]
            , model.name |> Bcc.ifNameValid (\_ -> [ Input.danger ]) (\_ -> [])
            ])
        , Form.invalidFeedback [] [ text "A name for a Bounded Context is required!" ]
        , Form.help [] [ text "Naming is hard. Writing down the name of your context and gaining agreement as a team will frame how you design the context." ]
        ]
      , Form.group []
        [ viewCaption "description" "Description"
        , Textarea.textarea
          [ Textarea.id "description"
          , Textarea.value model.description
          , Textarea.onInput Bcc.SetDescription
          ]
        , Form.help [] [ text "A few sentences describing the why and what of the context in business language. No technical details here."] ]
      ]
      |> List.map (Html.map Field)
    , [ Form.group []
        ( model.classification
          |> viewStrategicClassification
          |> List.map(Html.map (Bcc.ChangeStrategicClassification >> Field))
        )
      ]
    , [ Form.group []
        [ viewCaption "businessDecisions" "Business Decisions"
          , Textarea.textarea
            [ Textarea.id "businessDecisions"
            , Textarea.rows 10
            , Textarea.value model.businessDecisions
            , Textarea.onInput Bcc.SetBusinessDecisions
            ]
          , Form.help [] [ text "What are the key business rules and policies within this context?"]
        ]
      , Form.group []
          [ viewCaption "ubiquitousLanguage" "Ubiquitous Language"
            , Textarea.textarea
              [ Textarea.id "ubiquitousLanguage"
              , Textarea.rows 10
              , Textarea.value model.ubiquitousLanguage
              , Textarea.onInput Bcc.SetUbiquitousLanguage
              ]
            , Form.help [] [ text "What are the key domain terms that exist within this context, and what do they mean?"]
          ]
      ]
      |> List.map (Html.map Field)
    ]


viewModelTraits : Bcc.BoundedContextCanvas -> Html Bcc.Msg
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
        , Input.onInput Bcc.SetModelTraits
        ]
    , viewDescriptionList traits (Just "https://github.com/ddd-crew/bounded-context-canvas/blob/master/resources/model-traits-worksheet.md")
      |> viewInfoTooltip "How can you characterise the behaviour of this bounded context?"
    ]


viewRightside : EditingCanvas -> List (Html EditingMsg)
viewRightside model =
  [ viewModelTraits model.canvas |> Html.map Field
  , model.addingMessage |> Messages.view |> Html.map MessageField
  , model.addingDependencies |> Dependencies.view |> Html.map DependencyField
  ]

viewCanvas : EditingCanvas -> Html EditingMsg
viewCanvas model =
  Grid.row []
    [ Grid.col [] (viewLeftside model)
    , Grid.col [] (viewRightside model)
    ]

-- HTTP

loadBCC: Model -> Cmd Msg
loadBCC model =
  Http.get
    { url = Url.toString model.self
    , expect = Http.expectJson Loaded Bcc.modelDecoder
    }

saveBCC: Url.Url -> EditingCanvas -> Cmd Msg
saveBCC url model =
    Http.request
      { method = "PUT"
      , headers = []
      , url = Url.toString url
      , body = Http.jsonBody <| Bcc.modelEncoder model.canvas
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
