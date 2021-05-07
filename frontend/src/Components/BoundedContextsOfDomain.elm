module Components.BoundedContextsOfDomain exposing (..)


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
import BoundedContext.Canvas exposing (BoundedContextCanvas)
import BoundedContext.StrategicClassification as StrategicClassification
import ContextMapping.Collaboration as Collaboration
import ContextMapping.Collaborator as Collaborator
import ContextMapping.Communication as Communication
import BoundedContext.Namespace as Namespace exposing (Namespace)

import Components.BoundedContextCard as BoundedContextCard

import List

type alias Model =
  { config : Api.Configuration
  , domain : Domain
  , contextItems : List BoundedContextCard.Model
  }


init : Api.Configuration -> Domain -> List BoundedContextCard.Item -> Collaboration.Collaborations -> Model
init config domain items collaborations =
  let
    communication = Communication.asCommunication collaborations
    communicationFor { context} =
        communication 
        |> Communication.communicationFor (
          context
          |> BoundedContext.id
          |> Collaborator.BoundedContext
        )
  in 
    { contextItems =
          items
          |> List.map (\i -> 
            BoundedContextCard.init (communicationFor i) i
            )
      , config = config
      , domain = domain
      }


type Msg
    = NoOp


viewWithActions : BoundedContextCard.Model -> Card.Config Never
viewWithActions model  =
  model
  |> BoundedContextCard.view
  |> Card.footer []
  [ Grid.simpleRow
    [ Grid.col [ Col.md7 ]
      [ ButtonGroup.linkButtonGroup []
        [ ButtonGroup.linkButton
          [ Button.roleLink
          , Button.attrs
            [ href
              ( model.contextItem.context
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
              ( model.contextItem.context
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


view : Model -> Html Msg
view { contextItems, domain } =
    let
        cards =
            contextItems
            |> List.sortBy (\{ contextItem } -> contextItem.context |> BoundedContext.name)
            |> List.map viewWithActions
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
        |> Html.map (\_ -> NoOp)

