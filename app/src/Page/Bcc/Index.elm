module Page.Bcc.Index exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)

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
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Badge as Badge
import Bootstrap.Utilities.Spacing as Spacing

import List.Split exposing (chunksOfLeft)
import Url
import Http
import RemoteData
import Set

import Route

import BoundedContext
import BoundedContext.Canvas as Bcc
import BoundedContext.Dependency as Dependency
import BoundedContext.StrategicClassification as StrategicClassification exposing (StrategicClassification)

-- MODEL

type alias BccItem = Bcc.BoundedContextCanvas

type alias Model =
  { navKey : Nav.Key
  , bccName : String
  , baseUrl : Url.Url
  , bccs: RemoteData.WebData (List BccItem) }

init: Url.Url -> Nav.Key -> (Model, Cmd Msg)
init baseUrl key =
  ( { navKey = key
    , bccs = RemoteData.Loading
    , baseUrl = baseUrl
    , bccName = "" }
  , loadAll baseUrl )

-- UPDATE

type Msg
  = Loaded (Result Http.Error (List BccItem))
  | SetName String
  | CreateBcc
  | Created (Result Http.Error BoundedContext.BoundedContext)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Loaded (Ok items) ->
      ({ model | bccs = RemoteData.Success items }, Cmd.none)
    Loaded (Err e) ->
      ({ model | bccs = RemoteData.Failure e }, Cmd.none)
    SetName name ->
      ({ model | bccName = name}, Cmd.none)
    CreateBcc ->
      (model, createNewBcc model)
    Created (Ok item) ->
        (model, Route.pushUrl (item |> BoundedContext.id |> Route.Bcc ) model.navKey)
    _ ->
        Debug.log ("Overview: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
        (model, Cmd.none)

-- VIEW

createWithName : String -> Html Msg
createWithName name =
  Form.form [Html.Events.onSubmit CreateBcc]
    [ InputGroup.config (
        InputGroup.text
          [ Input.id name
          , Input.value name
          , Input.onInput SetName
          , Input.placeholder "Name of the new Bounded Context"
          ]
        )
      |> InputGroup.successors
        [ InputGroup.button
        [ Button.attrs
            [ Html.Attributes.type_ "submit"]
            , Button.primary
            , Button.disabled (name |> BoundedContext.isNameValid |> not)
            ]
        [ text "Create new Bounded Context"]
        ]
      |> InputGroup.view
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

viewItem : BccItem -> Card.Config Msg
viewItem item =
  let
    domainBadge =
      case item.classification.domain |> Maybe.map StrategicClassification.domainDescription of
        Just domain -> [ Badge.badgePrimary [ title domain.description ] [ text domain.name ] ]
        Nothing -> []
    businessBadges =
      item.classification.business
      |> List.map StrategicClassification.businessDescription
      |> List.map (\b -> Badge.badgeSecondary [ title b.description ] [ text b.name ])
    evolutionBadge =
      case item.classification.evolution |> Maybe.map StrategicClassification.evolutionDescription of
        Just evolution -> [ Badge.badgeInfo [ title evolution.description ] [ text evolution.name ] ]
        Nothing -> []
    badges =
      List.concat
        [ domainBadge
        , businessBadges
        , evolutionBadge
        ]

    messages =
      [ item.messages.commandsHandled, item.messages.eventsHandled, item.messages.queriesHandled ]
      |> List.map Set.size
      |> List.sum
      |> viewPillMessage "Handled Messages"
      |> List.append
        ( [ item.messages.commandsSent, item.messages.eventsPublished, item.messages.queriesInvoked]
          |> List.map Set.size
          |> List.sum
          |> viewPillMessage "Published Messages"
        )

    dependencies =
      item.dependencies.consumers
      |> Dependency.dependencyCount
      |> viewPillMessage "Consumers"
      |> List.append
        ( item.dependencies.suppliers
          |> Dependency.dependencyCount
          |> viewPillMessage "Suppliers"
        )
  in
  Card.config [ Card.attrs [class "mb-3"]]
    |> Card.headerH4 [] [ text (item.boundedContext |> BoundedContext.name) ]
    |> Card.block []
      ( List.concat
          [ if String.length item.description > 0
                then [ Block.text [] [ text item.description  ] ]
                else []
            , [ Block.custom (div [] badges) ]
          ]
      )
    |> Card.block []
      [ Block.custom (div [] dependencies)
      , Block.custom (div [] messages)
      ]
    |> Card.footer []
      [ Html.a
          [ href 
            ( item.boundedContext
              |> BoundedContext.id
              |> Route.Bcc
              |> Route.routeToString
            )
          , class "stretched-link"
          ]
          [ text "Edit Bounded Context" ]
      ]

viewLoaded : String -> List BccItem  -> List(Html Msg)
viewLoaded name items =
  if List.isEmpty items then
    [ Grid.row [ Row.attrs [ Spacing.pt3 ] ]
      [ Grid.col [ ]
        [ div [ Spacing.p5, class "shadow" ]
          [ Html.p
            [ class "lead", class "text-center" ]
            [ text "No existing bounded contexts found - do you want to create one?" ]
          , createWithName name
          ]
        ]
      ]
    ]
  else
    let
      cards =
        items
        |> List.sortBy (\i -> i.boundedContext |> BoundedContext.name)
        |> List.map viewItem
        |> chunksOfLeft 2
        |> List.map Card.deck
        |> div []
    in
      [ Grid.row [ Row.attrs [ Spacing.pt3 ] ]
        [ Grid.col []
          [ Html.h5 [ Spacing.mt3 ] [ text "Bounded Context of the Domain" ]
          , cards ]
        ]
        , Grid.row [ Row.attrs [Spacing.mt3]]
        [ Grid.col [] [ createWithName name ] ]
      ]

view : Model -> List (Html Msg)
view model =
  case model.bccs of
    RemoteData.Success contexts ->
      viewLoaded model.bccName contexts
    _ -> [ text "Loading your contexts"]


-- helpers

loadAll: Url.Url -> Cmd Msg
loadAll baseUrl =
  Http.get
    { url = { baseUrl | path = baseUrl.path ++ "/bccs" } |> Url.toString
    , expect = Http.expectJson Loaded bccItemsDecoder
    }

createNewBcc : Model -> Cmd Msg
createNewBcc model =
  let
      body =
          Encode.object
          [ ("name", Encode.string model.bccName) ]
      baseUrl = model.baseUrl
  in
      Http.post
      { url = { baseUrl | path = baseUrl.path ++ "/bccs" } |> Url.toString
      , body = Http.jsonBody body
      , expect = Http.expectJson Created BoundedContext.modelDecoder
      }

bccItemsDecoder: Decoder (List BccItem)
bccItemsDecoder =
  Decode.list Bcc.modelDecoder
