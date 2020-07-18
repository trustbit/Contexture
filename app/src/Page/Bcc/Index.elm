module Page.Bcc.Index exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Json.Decode as Decode

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
import Bootstrap.Utilities.Border as Border
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
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

import Route
import Api exposing (ApiResponse, ApiResult)

import Key
import Domain exposing (Domain)
import Domain.DomainId exposing (DomainId)
import BoundedContext exposing (BoundedContext)
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import BoundedContext.Canvas as Bcc
import BoundedContext.Dependency as Dependency
import BoundedContext.StrategicClassification as StrategicClassification

-- MODEL

type alias BccItem = Bcc.BoundedContextCanvas

type alias MoveContextModel =
  { context : BoundedContext
  , allDomains : RemoteData.WebData (List Domain)
  , selectState : Autocomplete.State
  , selectedDomain : Maybe Domain
  , modalVisibility : Modal.Visibility
  }

type alias DeleteBoundedContextModel =
  { boundedContext : BoundedContext
  , modalVisibility : Modal.Visibility
  }

type alias Model =
  { navKey : Nav.Key
  , bccName : String
  , config : Api.Configuration
  , domain : DomainId
  , deleteContext : Maybe DeleteBoundedContextModel
  , moveContext : Maybe MoveContextModel
  , bccs : RemoteData.WebData (List BccItem) }

initMoveContext : BoundedContext -> MoveContextModel
initMoveContext context =
  { context = context
  , allDomains = RemoteData.Loading
  , selectState = Autocomplete.newState "move-find-domains"
  , selectedDomain = Nothing
  , modalVisibility = Modal.shown
  }

init: Api.Configuration -> Nav.Key -> DomainId -> (Model, Cmd Msg)
init config key domain =
  ( { navKey = key
    , bccs = RemoteData.Loading
    , config = config
    , domain = domain
    , deleteContext = Nothing
    , moveContext = Nothing
    , bccName = "" }
  , loadAll config domain )

-- UPDATE

type Msg
  = Loaded (Result Http.Error (List BccItem))
  | SetName String
  | CreateBcc
  | Created (Result Http.Error BoundedContext.BoundedContext)
  | ShouldDelete BoundedContext
  | CancelDelete
  | DeleteContext BoundedContextId
  | ContextDeleted (ApiResponse ())
  | StartToMoveContext BoundedContext
  | AllDomainsLoaded (ApiResponse (List Domain))
  | DomainSelectMsg (Autocomplete.Msg Domain)
  | DomainSelected (Maybe Domain)
  | MoveContext
  | ContextMoved (ApiResponse ())
  | CancelMoveContext

updateMove : Model -> (MoveContextModel -> MoveContextModel) -> Model
updateMove model updateFunction =
  let
    move = model.moveContext |> Maybe.map updateFunction
  in
    { model | moveContext = move}

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
      (model, BoundedContext.newBoundedContext model.config model.domain model.bccName Created)

    Created (Ok item) ->
      (model, Route.pushUrl (item |> BoundedContext.id |> Route.Bcc ) model.navKey)

    ShouldDelete context ->
      ({ model | deleteContext = Just { boundedContext = context, modalVisibility = Modal.shown } }, Cmd.none)

    CancelDelete ->
      ({ model | deleteContext = Nothing }, Cmd.none)

    DeleteContext contextId ->
      ({ model | deleteContext = Nothing }, BoundedContext.remove model.config contextId ContextDeleted)

    ContextDeleted (Ok _) ->
      (model, loadAll model.config model.domain)

    StartToMoveContext context ->
      ( { model | moveContext = Just (initMoveContext context) }
      , findAllDomains model.config AllDomainsLoaded
      )

    AllDomainsLoaded (Ok domains) ->
      ( updateMove model (\v -> { v | allDomains = RemoteData.succeed domains })
      , Cmd.none
      )

    DomainSelectMsg selMsg ->
      case model.moveContext of
        Just move ->
          let
            (updated, cmd) = Autocomplete.update selectConfig selMsg move.selectState
          in
            ( updateMove model (\v -> { v | selectState = updated}), cmd)
        Nothing ->
          (model, Cmd.none)

    DomainSelected selected ->
      ( updateMove model (\v -> { v | selectedDomain = selected}), Cmd.none)

    MoveContext ->
      case model.moveContext of
        Just { context, selectedDomain } ->
          case selectedDomain of
            Just domain ->
              ( model
              , BoundedContext.move
                  model.config
                  (context |> BoundedContext.id)
                  (domain |> Domain.id)
                  ContextMoved
              )
            Nothing -> (model, Cmd.none)
        Nothing -> (model, Cmd.none)
    ContextMoved (Ok _) ->
      ( { model | moveContext = Nothing }, loadAll model.config model.domain)

    CancelMoveContext ->
      ( { model | moveContext = Nothing }, Cmd.none)

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

viewDelete : DeleteBoundedContextModel -> Html Msg
viewDelete model =
  Modal.config CancelDelete
  |> Modal.hideOnBackdropClick True
  |> Modal.h5 [] [ text <| "Delete " ++ (model.boundedContext |> BoundedContext.name) ]
  |> Modal.body [] [  text "Should the bounded context and all of it's data be deleted?" ]
  |> Modal.footer []
    [ Button.button [ Button.outlinePrimary, Button.onClick CancelDelete ] [ text "Cancel" ]
    , Button.button [ Button.primary, Button.onClick (model.boundedContext |> BoundedContext.id |> DeleteContext ) ] [ text "Delete Bounded Context" ] ]
  |> Modal.view model.modalVisibility

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
  Card.config [ Card.attrs [ class "mb-3", class "shadow" ] ]
    |> Card.block []
      [ Block.titleH4 []
        [ text (item.boundedContext |> BoundedContext.name)
        , Html.small [ class "text-muted", class "float-right" ]
          [ text (item.boundedContext |> BoundedContext.key |> Maybe.map Key.toString |> Maybe.withDefault "") ]
        ]
      , if String.length item.description > 0
        then Block.text [ class "text-muted"] [ text item.description  ]
        else Block.text [class "text-muted", class "text-center" ] [ Html.i [] [ text "No description :-(" ] ]
      , Block.custom (div [] badges)
      ]
    |> Card.block []
      [ Block.custom (div [] dependencies)
      , Block.custom (div [] messages)
      ]
    |> Card.footer []
      [ Grid.simpleRow
        [ Grid.col [ Col.md6]
          [ Button.linkButton
            [ Button.roleLink
            , Button.attrs
              [ href
                ( item.boundedContext
                  |> BoundedContext.id
                  |> Route.Bcc
                  |> Route.routeToString
                )
              ]
            ]
            [ text "Bounded Context Canvas" ]
          ]
        , Grid.col [ Col.textAlign Text.alignLgRight ]
          [ Button.button
            [ Button.secondary
            , Button.onClick (StartToMoveContext item.boundedContext) ]
            [ text "Move Context"]
          , Button.button
            [ Button.secondary
            , Button.onClick (ShouldDelete item.boundedContext)
            , Button.attrs [ Spacing.ml2 ]
            ]
            [ text "Delete" ]
          ]
        ]
      ]

viewLoaded : String -> List BccItem  -> List(Html Msg)
viewLoaded name items =
  if List.isEmpty items then
    [ Grid.row [ Row.attrs [ Spacing.pt3 ] ]
      [ Grid.col [ ]
        [ div [ Spacing.p5, class "shadow", Border.all ]
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

filterAutocomplete : Int -> String -> List Domain.Domain -> Maybe (List Domain.Domain)
filterAutocomplete minChars query items =
  if String.length query < minChars then
    Nothing
  else
    let
      lowerQuery = query |> String.toLower
      containsLowerString text =
        text
        |> String.toLower
        |> String.contains lowerQuery
      in
        items
        |> List.filter (\i -> i |> Domain.name |> containsLowerString)
        |> Just

selectConfig : Autocomplete.Config Msg Domain.Domain
selectConfig =
    Autocomplete.newConfig
        { onSelect = DomainSelected
        , toLabel = Domain.name
        , filter = filterAutocomplete 2
        }
        |> Autocomplete.withCutoff 12
        |> Autocomplete.withInputClass "text-control border rounded form-control-lg"
        |> Autocomplete.withInputWrapperClass ""
        |> Autocomplete.withItemClass " border p-2 "
        |> Autocomplete.withMenuClass "bg-light"
        |> Autocomplete.withNotFound "No matches"
        |> Autocomplete.withNotFoundClass "text-danger"
        |> Autocomplete.withHighlightedItemClass "bg-white"
        |> Autocomplete.withPrompt "Search for a domain"

viewMove : MoveContextModel -> Html Msg
viewMove model =
  let
    select =
      case model.allDomains of
        RemoteData.Success data ->
          let
            selectedItem =
              case model.selectedDomain of
                Just s -> [ s ]
                _ -> []

            relevantDomains =
              data
              |> List.filter (\d -> not ((d |> Domain.id) == (model.context |> BoundedContext.domain)))

            autocompleteSelect =
              Autocomplete.view
                selectConfig
                model.selectState
                relevantDomains
                selectedItem
            in
              Fieldset.config
                  |> Fieldset.attrs [ Spacing.ml4 ]
                  |> Fieldset.children (autocompleteSelect |> List.singleton)
                  |> Fieldset.view
        _ -> Html.p [] [ text "Loading domains" ]
  in
    Modal.config CancelMoveContext
    |> Modal.hideOnBackdropClick True
    |> Modal.h5 [] [ text <| "Move " ++ (model.context |> BoundedContext.name) ]
    |> Modal.body []
      [ Html.p [] [ text "Select the new domain of the context" ]
      , select |> Html.map DomainSelectMsg
      ]
    |> Modal.footer []
      [ Button.button
        [ Button.primary
        , Button.disabled (model.selectedDomain == Nothing)
        , Button.attrs [ Html.Events.onClick MoveContext ]
        ]
        [ text "Move context to domain" ]
      ]
    |> Modal.view model.modalVisibility

view : Model -> List (Html Msg)
view model =
  case model.bccs of
    RemoteData.Success contexts ->
      contexts
      |> viewLoaded model.bccName
      |> List.append
        ( [ model.deleteContext
            |> Maybe.map viewDelete
          , model.moveContext
            |> Maybe.map viewMove
          ] |> List.map (Maybe.withDefault (text ""))
        )

    _ -> [ text "Loading your contexts"]


-- helpers

loadAll : Api.Configuration -> DomainId -> Cmd Msg
loadAll config domain =
  Http.get
    { url = Api.boundedContexts domain |> Api.url config |> Url.toString
    , expect = Http.expectJson Loaded (Decode.list Bcc.modelDecoder)
    }

findAllDomains : Api.Configuration -> ApiResult (List Domain.Domain) msg
findAllDomains base =
  let
    request toMsg =
      Http.get
        { url = Api.domains [] |> Api.url base |> Url.toString
        , expect = Http.expectJson toMsg Domain.domainsDecoder
        }
  in
    request