module Page.Bcc.BoundedContextCard exposing (init,Model,Item,view)

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Utilities.Border as Border
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Badge as Badge
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Text as Text

import Url
import Set
import List

import Route

import Key
import BoundedContext as BoundedContext exposing (BoundedContext)
import BoundedContext.Canvas exposing (BoundedContextCanvas)
import BoundedContext.StrategicClassification as StrategicClassification
import ContextMapping.Communication as Communication exposing(Communication)
import BoundedContext.Namespace as Namespace exposing (Namespace)


type alias Item =
  { context : BoundedContext
  , canvas : BoundedContextCanvas
  , namespaces : List Namespace
  }


type alias Model =
  { contextItem : Item
  , communication : Communication
  }


init : Communication -> Item -> Model
init communications item =
  { contextItem = item
  , communication = communications
  }

viewLabelAsBadge : Namespace.Label -> Html Never
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


viewItem : Communication -> Item -> Card.Config Never
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
        communication
        |> Communication.inboundCollaborators
        |> List.length
        |> viewPillMessage "Inbound Communication"
        |> List.append
        ( communication
            |> Communication.outboundCollaborators
            |> List.length
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
                    |> Route.Namespaces
                    |> Route.routeToString
                  )
                ]
              ]
              [ text "Namespaces" ]
            ]
          ]
        ]
      ]


view : Model -> Card.Config Never
view { communication, contextItem } =
  viewItem communication contextItem
