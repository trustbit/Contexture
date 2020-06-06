module Bcc.Edit.Messages exposing (Msg(..), Model, view, update, init)

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Button as Button
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Utilities.Flex as Flex
import Bootstrap.Utilities.Spacing as Spacing

import Set

import Bcc

-- MODEL

type alias AddingMessage = 
  { commandsHandled : Bcc.Command
  , commandsSent : Bcc.Command
  , eventsHandled : Bcc.Event
  , eventsPublished : Bcc.Event
  , queriesHandled : Bcc.Query
  , queriesInvoked : Bcc.Query
  }

type alias Model =
  { adding: AddingMessage
  , messages: Bcc.Messages
  }

initAddingMessage = 
  { commandsHandled = ""
  , commandsSent = ""
  , eventsHandled = ""
  , eventsPublished = ""
  , queriesHandled = ""
  , queriesInvoked = ""
  }

init : Bcc.Messages -> Model
init messages =
  { adding = initAddingMessage
  , messages = messages }

-- UPDATE

type ChangeTypeMsg
  = FieldEdit Bcc.Message
  | MessageChanged Bcc.MessageAction

type Msg
  = CommandsHandled ChangeTypeMsg
  | CommandsSent ChangeTypeMsg
  | EventsHandled ChangeTypeMsg
  | EventsPublished ChangeTypeMsg
  | QueriesHandled ChangeTypeMsg
  | QueriesInvoked ChangeTypeMsg

updateAction : ChangeTypeMsg -> Bcc.MessageCollection -> (Bcc.Message, Bcc.MessageCollection)
updateAction msg existingMessages =
    case msg of
        FieldEdit m ->
            (m, existingMessages)
        MessageChanged changed ->
            ("", Bcc.updateMessageAction changed existingMessages)

update : Msg -> Model -> Model
update msg { adding,  messages} =
  case msg of
    CommandsHandled cmd ->
        let
          (edited, editMessages) = updateAction cmd messages.commandsHandled
        in
          { adding = { adding | commandsHandled = edited }, messages = { messages | commandsHandled = editMessages} }
    CommandsSent cmd ->
        let
          (edited, editMessages) = updateAction cmd messages.commandsSent
        in
          { adding = { adding | commandsSent = edited }, messages = { messages | commandsSent = editMessages} }
    EventsHandled event ->
        let
          (edited, editMessages) = updateAction event messages.eventsHandled
        in
          { adding = { adding | eventsHandled = edited }, messages = { messages | eventsHandled = editMessages} }
    EventsPublished event ->
        let
          (edited, editMessages) = updateAction event messages.eventsPublished
        in
          { adding = { adding | eventsPublished = edited }, messages = { messages | eventsPublished = editMessages} }
    QueriesHandled query ->
        let
          (edited, editMessages) = updateAction query messages.queriesHandled
        in
          { adding = { adding | queriesHandled = edited }, messages = { messages | queriesHandled = editMessages} }
    QueriesInvoked query ->
        let
          (edited, editMessages) = updateAction query messages.queriesInvoked
        in
          { adding = { adding | queriesInvoked = edited }, messages = { messages | queriesInvoked = editMessages} }

-- VIEW

viewMessageOption : Bcc.Message -> ListGroup.Item ChangeTypeMsg
viewMessageOption model =
  ListGroup.li 
    [ ListGroup.attrs [ Flex.block, Flex.justifyBetween, Flex.alignItemsCenter, Spacing.p1 ] ] 
    [ text model
    , Button.button [Button.danger, Button.small, Button.onClick (MessageChanged (Bcc.Remove model))] [ text "x"]
    ]

viewMessage : String -> String -> (Bcc.Message, Bcc.MessageCollection) -> Html ChangeTypeMsg
viewMessage id title (message, messages) =
  Form.group [Form.attrs [style "min-height" "250px"]]
    [ Form.label [for id] [ text title ]
    , ListGroup.ul 
      (
        messages
        |> Set.toList
        |> List.map viewMessageOption
      )
    , Form.form 
      [ Html.Events.onSubmit 
          (message
            |> Bcc.Add
            |> MessageChanged
          )
      , Flex.block, Flex.justifyBetween, Flex.alignItemsCenter
      ]
      [ InputGroup.config 
          ( InputGroup.text
            [ Input.id id
            , Input.value message
            , Input.onInput FieldEdit
            ]
          )
          |> InputGroup.successors
            [ InputGroup.button 
              [ Button.attrs 
                [ Html.Attributes.type_ "submit"]
                ,  Button.secondary
                , Button.disabled (String.length message <= 0)
                ]
              [ text "Add"] ]
          |> InputGroup.view
      ]
    ] 


view : Model -> Html Msg
view { adding, messages } =
  div []
    [ Html.h5 [ class "text-center" ] [ text "Messages Consumed and Produced" ]
    , Grid.row []
      [ Grid.col [] 
        [ Html.h6 [ class "text-center" ] [ text "Messages consumed"]
        , (adding.commandsHandled,  messages.commandsHandled)
            |> viewMessage "commandsHandled" "Commands handled"
            |> Html.map CommandsHandled
        , (adding.eventsHandled, messages.eventsHandled)
            |> viewMessage "eventsHandled" "Events handled"
            |> Html.map EventsHandled
        , (adding.queriesHandled,messages.queriesHandled)
            |> viewMessage "queriesHandled" "Queries handled"
            |> Html.map QueriesHandled
        ]
      , Grid.col []
        [ Html.h6 [ class "text-center" ] [ text "Messages produced"]
        , (adding.commandsSent, messages.commandsSent)
            |> viewMessage "commandsSent" "Commands sent"
            |> Html.map CommandsSent
        , (adding.eventsPublished, messages.eventsPublished)
            |> viewMessage "eventsPublished" "Events published"
            |> Html.map EventsPublished
        , (adding.queriesInvoked, messages.queriesInvoked)
            |> viewMessage "queriesInvoked" "Queries invoked" 
            |> Html.map QueriesInvoked
        ]
      ]
    ]
