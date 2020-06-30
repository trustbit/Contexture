module Page.Bcc.Edit.Messages exposing (
  Msg(..), Model,
  view, update, init,
  asMessages
  )

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
import Bootstrap.Utilities.Display as Display

import Message exposing (..)
import Set exposing (Set)

-- MODEL

type alias MessageReference t =
  { addingName : String
  , existingMessages : Set t }

type alias Model =
  { commandsHandled : MessageReference Command
  , commandsSent : MessageReference Command
  , eventsHandled : MessageReference Event
  , eventsPublished : MessageReference Event
  , queriesHandled : MessageReference Query
  , queriesInvoked : MessageReference Query
  }

asMessages : Model -> Messages
asMessages model =
  { commandsHandled = model.commandsHandled.existingMessages
  , commandsSent = model.commandsSent.existingMessages
  , eventsHandled = model.eventsHandled.existingMessages
  , eventsPublished = model.eventsPublished.existingMessages
  , queriesHandled = model.queriesHandled.existingMessages
  , queriesInvoked = model.queriesInvoked.existingMessages
  }


initReference : Set t -> MessageReference t
initReference messages =
  { addingName = ""
  , existingMessages = messages}

init : Messages -> Model
init messages =
  { commandsHandled = initReference messages.commandsHandled
  , commandsSent = initReference messages.commandsSent
  , eventsHandled = initReference messages.eventsHandled
  , eventsPublished = initReference messages.eventsPublished
  , queriesHandled = initReference messages.queriesHandled
  , queriesInvoked = initReference messages.queriesInvoked
  }
-- UPDATE

type Action t
  = Add t
  | Remove t

type alias MessageAction = Action Message

type ChangeTypeMsg
  = FieldEdit String
  | MessageChanged MessageAction

type Msg
  = CommandsHandled ChangeTypeMsg
  | CommandsSent ChangeTypeMsg
  | EventsHandled ChangeTypeMsg
  | EventsPublished ChangeTypeMsg
  | QueriesHandled ChangeTypeMsg
  | QueriesInvoked ChangeTypeMsg

updateMessageAction : MessageAction -> Set Message  -> Set Message
updateMessageAction action messages =
  case action of
    Add m ->
      Set.insert m messages
    Remove m ->
      Set.remove m messages

updateAction : ChangeTypeMsg -> MessageReference Message -> MessageReference Message
updateAction msg model =
  case msg of
    FieldEdit m ->
      { model | addingName = m }
    MessageChanged changed ->
      { model
      | existingMessages = updateMessageAction changed model.existingMessages
      , addingName = ""
      }


update : Msg -> Model -> Model
update msg model =
  case msg of
    CommandsHandled cmd ->
      { model | commandsHandled = updateAction cmd model.commandsHandled }
    CommandsSent cmd ->
      { model | commandsSent = updateAction cmd model.commandsSent }
    EventsHandled event ->
      { model | eventsHandled = updateAction event model.eventsHandled }
    EventsPublished event ->
      { model | eventsPublished = updateAction event model.eventsPublished }
    QueriesHandled query ->
      { model | queriesHandled = updateAction query model.queriesHandled }
    QueriesInvoked query ->
      { model | queriesInvoked = updateAction query model.queriesInvoked }

-- VIEW

viewMessageOption : Message -> ListGroup.Item ChangeTypeMsg
viewMessageOption model =
  ListGroup.li
    [ ListGroup.attrs [ Flex.block, Flex.justifyBetween, Flex.alignItemsCenter, Spacing.p1 ] ]
    [ text model
    , Button.button [Button.secondary, Button.small, Button.onClick (MessageChanged (Remove model))] [ text "x"]
    ]

viewMessage : String -> String -> MessageReference Message -> Html ChangeTypeMsg
viewMessage id title { addingName, existingMessages } =
  Form.group [Form.attrs [style "min-height" "250px"]]
    [ Form.label [for id] [ text title ]
    , ListGroup.ul
      (
        existingMessages
        |> Set.toList
        |> List.map viewMessageOption
      )
    , Form.form
      [ Html.Events.onSubmit
          (addingName
            |> Add
            |> MessageChanged
          )
      , Flex.block, Flex.justifyBetween, Flex.alignItemsCenter
      ]
      [ InputGroup.config
          ( InputGroup.text
            [ Input.id id
            , Input.value addingName
            , Input.onInput FieldEdit
            ]
          )
          |> InputGroup.successors
            [ InputGroup.button
              [ Button.attrs
                [ Html.Attributes.type_ "submit"]
                ,  Button.secondary
                , Button.disabled (String.length addingName <= 0)
                ]
              [ text "Add"] ]
          |> InputGroup.view
      ]
    ]


view : Model -> Html Msg
view model =
  div []
    [ Html.span
      [ class "text-center"
      , Display.block
      , style "background-color" "lightGrey"
      , Spacing.p2
      ]
      [ text "Messages Consumed and Produced" ]
    , Form.help [] [ text "What is the public interface or the contract of your bounded context? Which messages come in and which does it send out?" ]
    , Grid.row []
      [ Grid.col []
        [ Html.h6
          [ class "text-center", Spacing.p2 ]
          [ Html.strong [] [ text "Messages consumed" ] ]
        , model.commandsHandled
            |> viewMessage "commandsHandled" "Commands handled"
            |> Html.map CommandsHandled
        , model.eventsHandled
            |> viewMessage "eventsHandled" "Events handled"
            |> Html.map EventsHandled
        , model.queriesHandled
            |> viewMessage "queriesHandled" "Queries handled"
            |> Html.map QueriesHandled
        ]
      , Grid.col []
        [ Html.h6
          [ class "text-center", Spacing.p2 ]
          [ Html.strong [] [ text "Messages produced" ] ]
        , model.commandsSent
            |> viewMessage "commandsSent" "Commands sent"
            |> Html.map CommandsSent
        , model.eventsPublished
            |> viewMessage "eventsPublished" "Events published"
            |> Html.map EventsPublished
        , model.queriesInvoked
            |> viewMessage "queriesInvoked" "Queries invoked"
            |> Html.map QueriesInvoked
        ]
      ]
    ]
