module Bcc.Edit.Messages exposing (Msg(..), Model, AddingMessage, view, update, initAddingMessage)

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

type alias Model = (AddingMessage, Bcc.Messages)

initAddingMessage = 
  { commandsHandled = ""
  , commandsSent = ""
  , eventsHandled = ""
  , eventsPublished = ""
  , queriesHandled = ""
  , queriesInvoked = ""
  }

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

updateAction : ChangeTypeMsg -> Bcc.MessageCollection -> (Bcc.Message,Bcc.MessageCollection)
updateAction msg existingMessages =
    case msg of
        FieldEdit m ->
            (m, existingMessages)
        MessageChanged changed ->
            ("", Bcc.updateMessageAction changed existingMessages)

update : Msg -> Model -> Model
update msg (adding, messages) =
  case msg of
    CommandsHandled cmd ->
        let
            (edit, editMessages) = updateAction cmd messages.commandsHandled
        in
            ({ adding | commandsHandled = edit }, { messages | commandsHandled = editMessages} )
    CommandsSent cmd ->
        let
            (edit, editMessages) = updateAction cmd messages.commandsSent
        in
            ({ adding | commandsSent = edit }, { messages | commandsSent = editMessages} )
    EventsHandled event ->
        let
            (edit, editMessages) = updateAction event messages.eventsHandled
        in
            ({ adding | eventsHandled = edit }, { messages | eventsHandled = editMessages} )
    EventsPublished event ->
        let
            (edit, editMessages) = updateAction event messages.eventsPublished
        in
            ({ adding | eventsPublished = edit }, { messages | eventsPublished = editMessages} )
    QueriesHandled query ->
        let
            (edit, editMessages) = updateAction query messages.queriesHandled
        in
            ({ adding | queriesHandled = edit }, { messages | queriesHandled = editMessages} )
    QueriesInvoked query ->
        let
            (edit, editMessages) = updateAction query messages.queriesInvoked
        in
            ({ adding | queriesInvoked = edit }, { messages | queriesInvoked = editMessages} )

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
    [ Form.label [for id] [ text title]
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
            [ InputGroup.button [ Button.attrs [ Html.Attributes.type_ "submit"],  Button.secondary] [ text "Add"] ]
          |> InputGroup.view
      ]
    ] 


view : Model -> Html Msg
view (addingMessage, messages) =
  div []
    [ Html.h5 [ class "text-center" ] [ text "Messages Consumed and Produced" ]
    , Grid.row []
      [ Grid.col [] 
        [ Html.h6 [ class "text-center" ] [ text "Messages consumed"]
        , (addingMessage.commandsHandled,  messages.commandsHandled)
            |> viewMessage "commandsHandled" "Commands handled"
            |> Html.map CommandsHandled
        , (addingMessage.eventsHandled, messages.eventsHandled)
            |> viewMessage "eventsHandled" "Events handled"
            |> Html.map EventsHandled
        , (addingMessage.queriesHandled,messages.queriesHandled)
            |> viewMessage "queriesHandled" "Queries handled"
            |> Html.map QueriesHandled
        ]
      , Grid.col []
        [ Html.h6 [ class "text-center" ] [ text "Messages produced"]
        , (addingMessage.commandsSent, messages.commandsSent)
            |> viewMessage "commandsSent" "Commands sent"
            |> Html.map CommandsSent
        , (addingMessage.eventsPublished, messages.eventsPublished)
            |> viewMessage "eventsPublished" "Events published"
            |> Html.map EventsPublished
        , (addingMessage.queriesInvoked, messages.queriesInvoked)
            |> viewMessage "queriesInvoked" "Queries invoked" 
            |> Html.map QueriesInvoked
        ]
      ]
    ]
