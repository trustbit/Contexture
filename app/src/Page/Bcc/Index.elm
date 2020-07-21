module Page.Bcc.Index exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

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
import BoundedContext.Canvas exposing (BoundedContextCanvas)
import BoundedContext.Technical exposing (TechnicalDescription)
import BoundedContext.Dependency as Dependency
import BoundedContext.StrategicClassification as StrategicClassification

-- MODEL

type alias Item =
  { context : BoundedContext
  , canvas : BoundedContextCanvas
  , technical : TechnicalDescription
  }

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
  , boundedContextName : String
  , config : Api.Configuration
  , domain : DomainId
  , deleteContext : Maybe DeleteBoundedContextModel
  , moveContext : Maybe MoveContextModel
  , contextItems : RemoteData.WebData (List Item) }

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
    , contextItems = RemoteData.Loading
    , config = config
    , domain = domain
    , deleteContext = Nothing
    , moveContext = Nothing
    , boundedContextName = "" }
  , loadAll config domain )

-- UPDATE

type Msg
  = Loaded (Result Http.Error (List Item))
  | SetName String
  | CreateBoundedContext
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
      ({ model | contextItems = RemoteData.Success items }, Cmd.none)

    Loaded (Err e) ->
      ({ model | contextItems = RemoteData.Failure e }, Cmd.none)

    SetName name ->
      ({ model | boundedContextName = name}, Cmd.none)

    CreateBoundedContext ->
      (model, BoundedContext.newBoundedContext model.config model.domain model.boundedContextName Created)

    Created (Ok item) ->
      (model, Route.pushUrl (item |> BoundedContext.id |> Route.BoundedContextCanvas ) model.navKey)

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
  Form.form [Html.Events.onSubmit CreateBoundedContext]
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

urlAsLinkItem caption canBeLink =
  canBeLink
  |> Maybe.map (\value -> Html.a [ href <| Url.toString value, target "_blank" ] [ text caption] )

viewItem : Item -> Card.Config Msg
viewItem { context, canvas, technical } =
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
      canvas.dependencies.consumers
      |> Dependency.dependencyCount
      |> viewPillMessage "Consumers"
      |> List.append
        ( canvas.dependencies.suppliers
          |> Dependency.dependencyCount
          |> viewPillMessage "Suppliers"
        )

    technicalLinks =
      Html.ul [ class "list-inline"]
        ( [ urlAsLinkItem "Issue Tracker" technical.tools.issueTracker
          , urlAsLinkItem "Wiki" technical.tools.wiki
          , urlAsLinkItem "Repository" technical.tools.repository
          ]
          |> List.map (\li -> li |> Maybe.map (\item -> Html.li [ class "list-inline-item", class "pr-4" ] [ item ] ) )
          |> List.concatMap (\val ->
              case val of
                Just e -> [ e ]
                Nothing -> [ ]
            )
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
    |> Card.block []
      [ Block.custom technicalLinks ]
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
        , Grid.col [ Col.textAlign Text.alignLgRight ]
          [ ButtonGroup.buttonGroup [ ButtonGroup.small, ButtonGroup.attrs [ class "mt-auto", class "mb-auto" ] ]
            [ ButtonGroup.button
              [ Button.secondary
              , Button.onClick (StartToMoveContext context) ]
              [ text "Move Context"]
            , ButtonGroup.button
              [ Button.secondary
              , Button.onClick (ShouldDelete context)
              -- , Button.attrs [ Spacing.ml2 ]
              ]
              [ text "Delete" ]
            ]
          ]
        ]
      ]

viewLoaded : String -> List Item  -> List(Html Msg)
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
        |> List.sortBy (\{ context } -> context |> BoundedContext.name)
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
  case model.contextItems of
    RemoteData.Success contexts ->
      contexts
      |> viewLoaded model.boundedContextName
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
  let
    decoder =
      Decode.succeed Item
      |> JP.custom BoundedContext.modelDecoder
      |> JP.custom BoundedContext.Canvas.modelDecoder
      |> JP.custom BoundedContext.Technical.modelDecoder
  in Http.get
    { url = Api.boundedContexts domain |> Api.url config |> Url.toString
    , expect = Http.expectJson Loaded (Decode.list decoder)
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