module Page.Bcc.BoundedContext exposing (..)


import Json.Decode as Decode
import Json.Decode.Pipeline as JP

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Form.Input as Input
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Utilities.Border as Border
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Badge as Badge
import Bootstrap.Modal as Modal
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Text as Text

import Select as Autocomplete

import List.Split exposing (chunksOfLeft)
import Url
import Http
import RemoteData
import Set
import Dict as Dict exposing (Dict)

import Route
import Api exposing (ApiResponse, ApiResult)

import Key
import Domain exposing (Domain)
import Domain.DomainId exposing (DomainId)
import BoundedContext as BoundedContext exposing (BoundedContext)
import BoundedContext.BoundedContextId as BoundedContextId exposing (BoundedContextId)
import BoundedContext.Canvas exposing (BoundedContextCanvas)
import BoundedContext.StrategicClassification as StrategicClassification
import ContextMapping.Collaboration as Collaboration
import ContextMapping.Collaborator as Collaborator
import BoundedContext.Namespace as Namespace exposing (Namespace)
import List

type alias Item =
  { context : BoundedContext
  , canvas : BoundedContextCanvas
  , namespaces : List Namespace
  }


type alias Communication =
  { initiators : Dict String Collaboration.Collaborations
  , recipients : Dict String Collaboration.Collaborations
  }

type alias Model =
  { config : Api.Configuration
  , domain : Domain
  , contextItems : List Item
  , communication : Communication
  }


dictBcGet id = Dict.get (BoundedContextId.value id)
dictBcInsert id = Dict.insert (BoundedContextId.value id)


initCommunication : Collaboration.Collaborations -> Communication
initCommunication connections =
    let
        updateCollaborationLookup selectCollaborator dictionary collaboration =
            case selectCollaborator collaboration of
            Collaborator.BoundedContext bcId ->
                let
                    items =
                        dictionary
                        |> dictBcGet bcId
                        |> Maybe.withDefault []
                        |> List.append (List.singleton collaboration)
                in
                    dictionary |> dictBcInsert bcId items
            _ ->
                dictionary

        (bcInitiators, bcRecipients) =
            connections
            |> List.foldl(\collaboration (initiators, recipients) ->
                ( updateCollaborationLookup Collaboration.initiator initiators collaboration
                , updateCollaborationLookup Collaboration.recipient recipients collaboration
                )
            ) (Dict.empty, Dict.empty)
    in
       { initiators = bcInitiators, recipients = bcRecipients }

init : Api.Configuration -> Domain -> List Item -> Collaboration.Collaborations -> (Model, Cmd Msg)
init config domain items collaborations =
  ( { contextItems = items
    , config = config
    , domain = domain
    , communication = initCommunication collaborations
    }
  , Cmd.none
  )


type Msg
    = NoOp

viewLabelAsBadge label =
  let
    caption = label.name ++ " | " ++ label.value
  in
    Badge.badgeInfo
      [ Spacing.ml1
      , title <| "The label '" ++ label.name ++ "' has the value '" ++ label.value ++ "'"
      ]
      [ case Url.fromString label.value of
          Just link ->
            Html.span []
              [ text caption
              , Html.a [ link |> Url.toString |> href, target "_blank", Spacing.ml1 ] [ 0x0001F517 |> Char.fromCode  |> String.fromChar |> Html.text ]
              ]
          Nothing ->
            text caption
      ]

viewPillMessage : String -> Int -> List (Html msg)
viewPillMessage caption value =
  if value > 0 then
  [ Grid.simpleRow
    [ Grid.col [] [text caption]
    , Grid.col []
      [ Badge.pillWarning [] [ text (value |> String.fromInt)] ]
    ]
  ]
  else []


viewItem : Communication -> Item -> Card.Config Msg
viewItem communication { context, canvas, namespaces } =
  let
    domainBadge =
      case canvas.classification.domain |> Maybe.map StrategicClassification.domainDescription of
        Just domain -> [ Badge.badgePrimary [ title domain.description ] [ text domain.name ] ]
        Nothing -> []
    businessBadges =
      canvas.classification.business
      |> List.map StrategicClassification.businessDescription
      |> List.map (\b -> Badge.badgeSecondary [ title b.description ] [ text b.name ])
    evolutionBadge =
      case canvas.classification.evolution |> Maybe.map StrategicClassification.evolutionDescription of
        Just evolution -> [ Badge.badgeInfo [ title evolution.description ] [ text evolution.name ] ]
        Nothing -> []
    badges =
      List.concat
        [ domainBadge
        , businessBadges
        , evolutionBadge
        ]

    messages =
      [ canvas.messages.commandsHandled, canvas.messages.eventsHandled, canvas.messages.queriesHandled ]
      |> List.map Set.size
      |> List.sum
      |> viewPillMessage "Handled Messages"
      |> List.append
        ( [ canvas.messages.commandsSent, canvas.messages.eventsPublished, canvas.messages.queriesInvoked]
          |> List.map Set.size
          |> List.sum
          |> viewPillMessage "Published Messages"
        )

    dependencies =
        communication.initiators
        |> dictBcGet (context |> BoundedContext.id)
        |> Maybe.map (List.length)
        |> Maybe.withDefault 0
        |> viewPillMessage "Inbound Communication"
        |> List.append
        ( communication.recipients
            |> dictBcGet (context |> BoundedContext.id)
            |> Maybe.map (List.length)
            |> Maybe.withDefault 0
            |> viewPillMessage "Outbound Communication"
        )

    namespaceBlocks =
      namespaces
      |> List.map (\namespace ->
        ListGroup.li []
          [ Html.h6 []
            [ text namespace.name
            , Html.small [ class "text-muted"] [ text " Namespace" ]
            ]
          , div [] (
              namespace.labels
              |> List.map viewLabelAsBadge
            )
          ]
      )

  in
  Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
    |> Card.block []
      [ Block.titleH4 []
        [ text (context |> BoundedContext.name)
        , Html.small [ class "text-muted", class "float-right" ]
          [ text (context |> BoundedContext.key |> Maybe.map Key.toString |> Maybe.withDefault "") ]
        ]
      , if String.length canvas.description > 0
        then Block.text [ class "text-muted"] [ text canvas.description  ]
        else Block.text [class "text-muted", class "text-center" ] [ Html.i [] [ text "No description :-(" ] ]
      , Block.custom (div [] badges)
      ]
    |> Card.block []
      [ Block.custom (div [] dependencies)
      , Block.custom (div [] messages)
      ]
    |> (\t ->
        if List.isEmpty namespaceBlocks
        then t
        else t |> Card.listGroup namespaceBlocks
    )
    |> Card.footer []
      [ Grid.simpleRow
        [ Grid.col [ Col.md7 ]
          [ ButtonGroup.linkButtonGroup []
            [ ButtonGroup.linkButton
              [ Button.roleLink
              , Button.attrs
                [ href
                  ( context
                    |> BoundedContext.id
                    |> Route.BoundedContextCanvas
                    |> Route.routeToString
                  )
                ]
              ]
              [ text "Canvas" ]
            , ButtonGroup.linkButton
              [ Button.roleLink
              , Button.attrs
                [ href
                  ( context
                    |> BoundedContext.id
                    |> Route.TechnicalDescription
                    |> Route.routeToString
                  )
                ]
              ]
              [ text "Technical Description" ]
            ]
          ]
        ]
      ]



view : Model -> Html Msg
view { communication, contextItems, domain } =
    let
        cards =
            contextItems
            |> List.sortBy (\{ context } -> context |> BoundedContext.name)
            |> List.map (viewItem communication)
            |> chunksOfLeft 2
            |> List.map Card.deck
            |> div []

        contextCount = contextItems |> List.length |> String.fromInt
    in
        Card.config []
        |> Card.headerH5 []
            [ text <| "Bounded Contexts of '" ++ (domain |> Domain.name) ++ "' (" ++ contextCount ++ ")" ]
        |> Card.block []
            [ Block.custom cards ]
        |> Card.view

