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
import Bootstrap.Form.Select as Select
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Button as Button
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Text as Text
import Bootstrap.Utilities.Flex as Flex
import Bootstrap.Utilities.Spacing as Spacing


import Url

import Set
import Dict
import Http

import Route
import Bcc
import Bcc.Edit.Dependencies as Dependencies

-- MODEL

type alias EditingCanvas = 
  { canvas : Bcc.BoundedContextCanvas
  , addingMessage : AddingMessage
  , addingDependencies: Dependencies.DependenciesEdit
  }
type alias AddingMessage = 
  { commandsHandled : Bcc.Command
  , commandsSent : Bcc.Command
  , eventsHandled : Bcc.Event
  , eventsPublished : Bcc.Event
  , queriesHandled : Bcc.Query
  , queriesInvoked : Bcc.Query
  }

type alias Model = 
  { key: Nav.Key
  , self: Url.Url
  -- TODO: discuss we want this in edit or BCC - it's not persisted after all!
  , edit: EditingCanvas
  }

initAddingMessage = 
  { commandsHandled = ""
  , commandsSent = ""
  , eventsHandled = ""
  , eventsPublished = ""
  , queriesHandled = ""
  , queriesInvoked = ""
  }

init : Nav.Key -> Url.Url -> (Model, Cmd Msg)
init key url =
  let
    canvas = Bcc.init ()
    model =
      { key = key
      , self = url
      , edit = 
        { addingMessage = initAddingMessage
        , addingDependencies = Dependencies.initDependencies
        , canvas = canvas
        }
      }
  in
    (
      model
    , loadBCC model
    )


-- UPDATE

type MessageFieldMsg
  = CommandsHandled Bcc.Message
  | CommandsSent Bcc.Message
  | EventsHandled Bcc.Message
  | EventsPublished Bcc.Message
  | QueriesHandled Bcc.Message
  | QueriesInvoked Bcc.Message

type EditingMsg
  = Field Bcc.Msg
  | MessageField MessageFieldMsg
  | DependencyField Dependencies.Msg

type Msg
  = Loaded (Result Http.Error Bcc.BoundedContextCanvas)
  | Editing EditingMsg
  | Save
  | Saved (Result Http.Error ())
  | Delete
  | Deleted (Result Http.Error ())
  | Back

updateAddingMessage : MessageFieldMsg -> AddingMessage -> AddingMessage
updateAddingMessage msg model =
  case msg of
    CommandsHandled cmd ->
      { model | commandsHandled = cmd }
    CommandsSent cmd ->
      { model | commandsSent = cmd }
    EventsHandled event ->
      { model | eventsHandled = event }
    EventsPublished event ->
      { model | eventsPublished = event }
    QueriesHandled query ->
      { model | queriesHandled = query }
    QueriesInvoked query ->
      { model | queriesInvoked = query }

updateEdit : EditingMsg -> EditingCanvas -> EditingCanvas
updateEdit msg model =
  case msg of
    Field (Bcc.ChangeMessages change) ->
      let
        addingMessageModel = model.addingMessage
        addingMessage = 
          case change of
            Bcc.CommandHandled _ ->
              { addingMessageModel | commandsHandled = "" }
            Bcc.CommandSent _ ->
              { addingMessageModel | commandsSent = "" }
            Bcc.EventsHandled _ ->
              { addingMessageModel | eventsHandled = "" }
            Bcc.EventsPublished _ ->
              { addingMessageModel | eventsPublished = "" }
            Bcc.QueriesHandled _ ->
              { addingMessageModel | queriesHandled = "" }
            Bcc.QueriesInvoked _ ->
              { addingMessageModel | queriesInvoked = "" }
      in
        { model | canvas = Bcc.update (Bcc.ChangeMessages change) model.canvas, addingMessage = addingMessage }
    Field fieldMsg ->
      { model | canvas = Bcc.update fieldMsg model.canvas }
    DependencyField dependency ->
      let 
        (addingDependencies, dependencies) = Dependencies.update dependency (model.addingDependencies, model.canvas.dependencies)
        canvas = model.canvas
        c = { canvas | dependencies = dependencies}
      in
        { model | canvas = c, addingDependencies = addingDependencies }
    MessageField fieldMsg ->
      { model | addingMessage = updateAddingMessage fieldMsg model.addingMessage }

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Editing editing ->
      ({ model | edit = updateEdit editing model.edit}, Cmd.none)
    Save -> 
      (model, saveBCC model)
    Saved (Ok _) -> 
      (model, Cmd.none)
    Delete ->
      (model, deleteBCC model)
    Deleted (Ok _) ->
      (model, Route.pushUrl Route.Overview model.key)
    Loaded (Ok m) ->
      let
        editing = 
          { canvas = m
          , addingMessage = initAddingMessage
          , addingDependencies = Dependencies.initDependencies
          }
      in
        ({ model | edit = editing } , Cmd.none)    
    Back -> 
      (model, Route.goBack model.key)
    _ ->
      Debug.log ("BCC: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)

-- VIEW

view : Model -> Html Msg
view model =
  div []
      [ viewCanvas model.edit |> Html.map Editing
      , Grid.row []
        [ Grid.col [] 
          [ Button.button [Button.secondary, Button.onClick Back] [text "Back"]
          , Button.submitButton [ Button.primary, Button.onClick Save ] [ text "Save"]
          , Button.button 
            [ Button.danger
            , Button.onClick Delete
            , Button.attrs [ title ("Delete " ++ model.edit.canvas.name) ] 
            ]
            [ text "X" ]
          ]
        ]
      ]

viewRadioButton : String -> String -> Bool -> Bcc.Msg -> Radio.Radio Bcc.Msg
viewRadioButton id title checked msg =
  Radio.create [Radio.id id, Radio.onClick msg, Radio.checked checked] title

viewLeftside : Bcc.BoundedContextCanvas -> List (Html EditingMsg)
viewLeftside model =
  [ Form.group []
    [ Form.label [for "name"] [ text "Name"]
    , Input.text [ Input.id "name", Input.value model.name, Input.onInput Bcc.SetName ] ]
  , Form.group []
    [ Form.label [for "description"] [ text "Description"]
    , Input.text [ Input.id "description", Input.value model.description, Input.onInput Bcc.SetDescription ]
    , Form.help [] [ text "Summary of purpose and responsibilities"] ]
  , Grid.row []
    [ Grid.col [] 
      [ Form.label [for "classification"] [ text "Bounded Context classification"]
      , div [] 
          (Radio.radioList "classification" 
          [ viewRadioButton "core" "Core" (model.classification == Just Bcc.Core) (Bcc.SetClassification Bcc.Core) 
          , viewRadioButton "supporting" "Supporting" (model.classification == Just Bcc.Supporting) (Bcc.SetClassification Bcc.Supporting) 
          , viewRadioButton "generic" "Generic" (model.classification == Just Bcc.Generic) (Bcc.SetClassification Bcc.Generic) 
          -- TODO: Other
          ]
          )
      , Form.help [] [ text "How can the Bounded Context be classified?"] ]
      , Grid.col []
        [ Form.label [for "businessModel"] [ text "Business Model"]
        , div [] 
            (Radio.radioList "businessModel" 
            [ viewRadioButton "revenue" "Revenue" (model.businessModel == Just Bcc.Revenue) (Bcc.SetBusinessModel Bcc.Revenue) 
            , viewRadioButton "engagement" "Engagement" (model.businessModel == Just Bcc.Engagement) (Bcc.SetBusinessModel Bcc.Engagement) 
            , viewRadioButton "Compliance" "Compliance" (model.businessModel == Just Bcc.Compliance) (Bcc.SetBusinessModel Bcc.Compliance) 
            , viewRadioButton "costReduction" "Cost reduction" (model.businessModel == Just Bcc.CostReduction) (Bcc.SetBusinessModel Bcc.CostReduction) 
            -- TODO: Other
            ]
            )
        , Form.help [] [ text "What's the underlying business model of the Bounded Context?"] ]
      , Grid.col []
        [ Form.label [for "evolution"] [ text "Evolution"]
        , div [] 
            (Radio.radioList "evolution" 
            [ viewRadioButton "genesis" "Genesis" (model.evolution == Just Bcc.Genesis) (Bcc.SetEvolution Bcc.Genesis) 
            , viewRadioButton "customBuilt" "Custom built" (model.evolution == Just Bcc.CustomBuilt) (Bcc.SetEvolution Bcc.CustomBuilt) 
            , viewRadioButton "product" "Product" (model.evolution == Just Bcc.Product) (Bcc.SetEvolution Bcc.Product) 
            , viewRadioButton "commodity" "Commodity" (model.evolution == Just Bcc.Commodity) (Bcc.SetEvolution Bcc.Commodity) 
            -- TODO: Other
            ]
            )
        , Form.help [] [ text "How does the context evolve? How novel is it?"] ]
    ]
  , Form.group []
    [ Form.label [for "businessDecisions"] [ text "Business Decisions"]
      , Textarea.textarea [ Textarea.id "businessDecisions", Textarea.rows 4, Textarea.value model.businessDecisions, Textarea.onInput Bcc.SetBusinessDecisions ]
      , Form.help [] [ text "Key business rules, policies and decisions"] ]
  , Form.group []
    [ Form.label [for "ubiquitousLanguage"] [ text "Ubiquitous Language"]
      , Textarea.textarea [ Textarea.id "ubiquitousLanguage", Textarea.rows 4, Textarea.value model.ubiquitousLanguage, Textarea.onInput Bcc.SetUbiquitousLanguage ]
      , Form.help [] [ text "Key domain terminology"] ]
  ]
  |> List.map (Html.map Field)

viewMessageOption : (Bcc.MessageAction   -> Bcc.MessageMsg) -> Bcc.Message -> ListGroup.Item Bcc.MessageMsg
viewMessageOption remove model =
  ListGroup.li 
    [ ListGroup.attrs [ Flex.block, Flex.justifyBetween, Flex.alignItemsCenter, Spacing.p1 ] ] 
    [ text model
    , Button.button [Button.danger, Button.small, Button.onClick (remove (Bcc.Remove model))] [ text "x"]
    ]

type alias MessageEdit =
  { messages: Set.Set Bcc.Message
  , message : Bcc.Message
  , modifyMessageCmd : Bcc.MessageAction -> Bcc.MessageMsg
  , updateNewMessageText : String -> MessageFieldMsg
  }

viewMessage : String -> String -> MessageEdit -> Html EditingMsg
viewMessage id title edit =
  Form.group [Form.attrs [style "min-height" "250px"]]
    [ Form.label [for id] [ text title]
    , ListGroup.ul 
      (
        edit.messages
        |> Set.toList
        |> List.map (viewMessageOption edit.modifyMessageCmd)
      )
      |> Html.map (Bcc.ChangeMessages >> Field)
    , Form.form 
      [ Html.Events.onSubmit 
          (edit.message
            |> Bcc.Add
            |> edit.modifyMessageCmd
            |> Bcc.ChangeMessages
            |> Field
          )
      , Flex.block, Flex.justifyBetween, Flex.alignItemsCenter
      ]
      [ InputGroup.config 
          ( InputGroup.text
            [ Input.id id
            , Input.value edit.message
            , Input.onInput edit.updateNewMessageText 
            ]
          )
          |> InputGroup.successors
            [ InputGroup.button [ Button.attrs [ Html.Attributes.type_ "submit"],  Button.secondary] [ text "Add"] ]
          |> InputGroup.view
          |> Html.map MessageField
      ]
    ] 

viewMessages : EditingCanvas -> Html EditingMsg
viewMessages editing =
  let
    messages = editing.canvas.messages
    addingMessage = editing.addingMessage
  in
  div []
    [ Html.h5 [ class "text-center" ] [ text "Messages Consumed and Produced" ]
    , Grid.row []
      [ Grid.col [] 
        [ Html.h6 [ class "text-center" ] [ text "Messages consumed"]
        , { messages = messages.commandsHandled
          , message = addingMessage.commandsHandled
          , modifyMessageCmd = Bcc.CommandHandled
          , updateNewMessageText = CommandsHandled
          } |> viewMessage "commandsHandled" "Commands handled"
        , { messages = messages.eventsHandled
          , message = addingMessage.eventsHandled
          , modifyMessageCmd = Bcc.EventsHandled
          , updateNewMessageText = EventsHandled
          } |> viewMessage "eventsHandled" "Events handled"
        , { messages = messages.queriesHandled
          , message = addingMessage.queriesHandled
          , modifyMessageCmd = Bcc.QueriesHandled
          , updateNewMessageText = QueriesHandled
          } |> viewMessage "queriesHandled" "Queries handled"
        ]
      , Grid.col []
        [ Html.h6 [ class "text-center" ] [ text "Messages produced"]
        , { messages = messages.commandsSent
          , message = addingMessage.commandsSent
          , modifyMessageCmd = Bcc.CommandSent
          , updateNewMessageText = CommandsSent
          } |> viewMessage "commandsSent" "Commands sent"
        , { messages = messages.eventsPublished
          , message = addingMessage.eventsPublished
          , modifyMessageCmd = Bcc.EventsPublished
          , updateNewMessageText = EventsPublished
          } |> viewMessage "eventsPublished" "Events published"
        , { messages = messages.queriesInvoked
          , message = addingMessage.queriesInvoked
          , modifyMessageCmd = Bcc.QueriesInvoked
          , updateNewMessageText = QueriesInvoked
          } |> viewMessage "queriesInvoked" "Queries invoked"
        ]
      ]
    ]

viewRightside : EditingCanvas -> List (Html EditingMsg)
viewRightside model =
  [ Form.group []
    [ Form.label [for "modelTraits"] [ text "Model traits"]
    , Input.text [ Input.id "modelTraits", Input.value model.canvas.modelTraits, Input.onInput Bcc.SetModelTraits ] |> Html.map Field
    , Form.help [] [ text "draft, execute, audit, enforcer, interchange, gateway, etc."] ]
    , viewMessages model
    , Dependencies.view (model.addingDependencies, model.canvas.dependencies) |> Html.map DependencyField
  ]

viewCanvas : EditingCanvas -> Html EditingMsg
viewCanvas model =
  Grid.row []
    [ Grid.col [] (viewLeftside model.canvas)
    , Grid.col [] (viewRightside model)
    ]


-- HTTP

loadBCC: Model -> Cmd Msg
loadBCC model =
  Http.get
    { url = Url.toString model.self
    , expect = Http.expectJson Loaded Bcc.modelDecoder
    }

saveBCC: Model -> Cmd Msg
saveBCC model =
    Http.request
      { method = "PUT"
      , headers = []
      , url = Url.toString model.self
      , body = Http.jsonBody <| Bcc.modelEncoder model.edit.canvas
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
