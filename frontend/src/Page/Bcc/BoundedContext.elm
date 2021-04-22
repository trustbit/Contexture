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

import Page.Bcc.BoundedContextCard as BoundedContextCard

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
  , contextItems : List BoundedContextCard.Model
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
  let
    communication = initCommunication collaborations
  in 
    ( { contextItems = items |> List.map (BoundedContextCard.init communication)
      , config = config
      , domain = domain
      }
    , Cmd.none
    )


type Msg
    = NoOp


view : Model -> Html Msg
view { contextItems, domain } =
    let
        cards =
            contextItems
            |> List.sortBy (\{ contextItem } -> contextItem.context |> BoundedContext.name)
            |> List.map (BoundedContextCard.view) 
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

