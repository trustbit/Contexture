module Page.Domain.Index exposing (Msg, Model, update, view, initWithSubdomains, initWithoutSubdomains)

import Browser.Navigation as Nav

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events

import Json.Decode as Decode exposing(Decoder)
import Json.Decode.Pipeline as JP

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Button as Button
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Modal as Modal
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Text as Text

import Select as Autocomplete

import List.Split exposing (chunksOfLeft)

import RemoteData
import Url
import Http

import Api exposing (ApiResult)
import Domain exposing (DomainRelation)
import Domain.DomainId exposing (DomainId)
import Page.Domain.Create
import Route
import BoundedContext exposing (BoundedContext)

-- MODEL

type alias DomainItem =
  { domain : Domain.Domain
  , subDomains : List Domain.Domain
  , contexts : List BoundedContext
  }

type MoveDomainTarget
  = AsSubdomain Autocomplete.State (Maybe Domain.Domain)
  | AsRoot

type alias MoveDomainModel =
  { domainItem : DomainItem
  , moveTo : MoveDomainTarget
  , canMoveToRoot : Bool
  , allDomains : RemoteData.WebData (List Domain.Domain)
  , modalVisibility : Modal.Visibility
  }

type alias Model =
  { navKey : Nav.Key
  , baseUrl : Api.Configuration
  , showDomains : Domain.DomainRelation
  , createDomain : Page.Domain.Create.Model
  , domains : RemoteData.WebData (List DomainItem)
  , moveToNewDomain : Maybe MoveDomainModel
   }

initMove : Api.Configuration -> DomainItem -> (MoveDomainModel, Cmd MoveDomainMsg)
initMove baseUrl model =
  let
    item = model.domain
    canMoveToRoot =  not (item.parentDomain == Nothing)
  in

  ( { allDomains = RemoteData.Loading
    , moveTo = if canMoveToRoot then AsRoot else AsSubdomain (Autocomplete.newState "sub-domain") Nothing
    , domainItem = model
    , canMoveToRoot = canMoveToRoot
    , modalVisibility = Modal.shown
    }
  , findAllDomains baseUrl AllDomainsLoaded
  )

init : Api.Configuration -> Nav.Key -> Domain.DomainRelation -> (Model, Cmd Msg)
init baseUrl key subDomains =
  let
    (createModel, createCmd) = Page.Domain.Create.init baseUrl key subDomains
  in
  ( { navKey = key
    , baseUrl = baseUrl
    , showDomains = subDomains
    , createDomain = createModel
    , domains = RemoteData.Loading
    , moveToNewDomain = Nothing
    }
  , Cmd.batch
    [ domainsOf baseUrl subDomains Loaded
    , createCmd |> Cmd.map CreateMsg ] )

initWithSubdomains : Api.Configuration -> Nav.Key -> DomainId -> (Model, Cmd Msg)
initWithSubdomains baseUrl key parentDomain =
  init baseUrl key (Domain.Subdomain parentDomain)

initWithoutSubdomains : Api.Configuration -> Nav.Key -> (Model, Cmd Msg)
initWithoutSubdomains baseUrl key =
  init baseUrl key Domain.Root


-- UPDATE

type MoveDomainMsg
  = AllDomainsLoaded (Result Http.Error (List Domain.Domain))
  | WillMoveToRoot
  | WillMoveToSubdomain
  | MoveDomainTo
  | DomainMoved (Result Http.Error ())
  | Cancel
  | SubdomainSelectMsg (Autocomplete.Msg Domain.Domain)
  | SubdomainSelected (Maybe Domain.Domain)

type Msg
  = Loaded (Result Http.Error (List DomainItem))
  | CreateMsg Page.Domain.Create.Msg
  | StartMoveToDomain DomainItem
  | MoveToDomain MoveDomainMsg

updateMoveToDomain : Api.Configuration -> MoveDomainMsg -> MoveDomainModel -> (MoveDomainModel, Cmd MoveDomainMsg)
updateMoveToDomain baseUrl msg model =
  case (msg, model.moveTo) of
    (AllDomainsLoaded (Ok allDomains), _) ->
      ({ model | allDomains = RemoteData.succeed allDomains }, Cmd.none)
    (AllDomainsLoaded (Err e),_) ->
      let
        _ = Debug.log "domain loaded" e
      in
        (model, Cmd.none)
    (WillMoveToRoot, _) ->
      ({ model | moveTo = AsRoot }, Cmd.none)
    (WillMoveToSubdomain, _) ->
      ({ model | moveTo = AsSubdomain (Autocomplete.newState "sub-domain") Nothing }, Cmd.none)
    (SubdomainSelectMsg selMsg, AsSubdomain state selected) ->
      let
        ( updated, cmd ) =
          Autocomplete.update selectConfig selMsg state
      in
        ( { model | moveTo = AsSubdomain updated selected }, cmd )
    (SubdomainSelected item, AsSubdomain state _)->
      ({ model | moveTo = AsSubdomain state item }, Cmd.none)
    (MoveDomainTo, AsRoot) ->
      (model, Domain.moveDomain baseUrl model.domainItem.domain.id Domain.Root DomainMoved)
    (MoveDomainTo, AsSubdomain _ (Just selected)) ->
      (model, Domain.moveDomain baseUrl model.domainItem.domain.id (Domain.Subdomain selected.id) DomainMoved)
    (Cancel, _) ->
      ({ model | modalVisibility = Modal.hidden }, Cmd.none)
    (DomainMoved (Ok _), _) ->
      ({ model | modalVisibility = Modal.hidden }, Cmd.none)
    (m, _) ->
      let
        _ = Debug.log "domain moved" m
      in
      (model, Cmd.none)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Loaded (Ok items) ->
      ({ model | domains = RemoteData.Success items }, Cmd.none)

    Loaded (Err e) ->
      ({ model | domains = RemoteData.Failure e }, Cmd.none)

    StartMoveToDomain item ->
      let
        (moveModel, moveCmd) = initMove model.baseUrl item
      in
        ( { model | moveToNewDomain = Just moveModel }, moveCmd |> Cmd.map MoveToDomain)
    MoveToDomain move ->
      case model.moveToNewDomain of
        Just moveModel ->
          let
            -- TODO: remove the sub-msg. this is a code smell
            additional =
              case move of
                DomainMoved (Ok _) ->
                  [ domainsOf model.baseUrl model.showDomains Loaded ]
                _ -> []
            (moveModel_, moveCmd) = updateMoveToDomain model.baseUrl move moveModel
          in
            ( { model | moveToNewDomain = Just moveModel_ }
            , (moveCmd |> Cmd.map MoveToDomain) :: additional
              |> Cmd.batch )
        Nothing ->
          (model,Cmd.none)
    CreateMsg create ->
      let
        (createModel, createCmd) = Page.Domain.Create.update create model.createDomain
      in
        ({ model | createDomain = createModel }, createCmd |> Cmd.map CreateMsg)

-- VIEW

viewDomain : DomainItem -> Card.Config Msg
viewDomain item =
  Card.config [Card.attrs [ class "mb-3"] ]
    |> Card.headerH4 [] [ text item.domain.name ]
    |> Card.block []
      ( if String.length item.domain.vision > 0
        then [ Block.text [] [ text item.domain.vision ] ]
        else []
      )
    |> Card.block []
      [ item.subDomains
        |> List.map .name
        |> List.map (\name -> Html.li [] [text name] )
        |> Html.ul []
        |> Block.custom
      , item.contexts
        |> List.map BoundedContext.name
        |> List.map (\name -> Html.li [] [text name] )
        |> Html.ul []
        |> Block.custom
      ]
    |> Card.footer []
      [ Grid.simpleRow
        [ Grid.col []
          [ Button.linkButton
            [ Button.roleLink
            , Button.attrs [ href (Route.routeToString (Route.Domain item.domain.id)) ]
            ]
            [ text "View Domain" ]
          ]
        , Grid.col [ Col.textAlign Text.alignLgRight]
          [ Button.button
            [ Button.secondary
            , Button.onClick (StartMoveToDomain item)
            ]
            [ text "Move Domain"]
          ]
        ]
      ]

viewLoaded : Page.Domain.Create.Model -> List DomainItem  -> List (Html Msg)
viewLoaded create items =
  if List.isEmpty items then
    [ Grid.row []
      [ Grid.col [ Col.attrs [ Spacing.pt2, Spacing.pl5, Spacing.pr5] ]
        [ Html.p
          [ class "lead", class "text-center" ]
          [ text "No existing domains found - do you want to create one?"]
        , create |> Page.Domain.Create.view |> Html.map CreateMsg
        ]
      ]
    ]
  else
    [ Grid.row [ Row.attrs [ Spacing.pt3 ] ]
        [ Grid.col
          []
          ( items
            |> List.map viewDomain
            |> chunksOfLeft 2
            |> List.map Card.deck
          )
        ]
    , Grid.row [ Row.attrs [ Spacing.mt3 ] ]
        [ Grid.col []
          [ create |> Page.Domain.Create.view |> Html.map CreateMsg ]
        ]
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
        |> List.filter (\i -> i.name |> containsLowerString)
        |> Just


selectConfig : Autocomplete.Config MoveDomainMsg Domain.Domain
selectConfig =
    Autocomplete.newConfig
        { onSelect = SubdomainSelected
        , toLabel = .name
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
        |> Autocomplete.withPrompt "Search for a Dependency"
        -- |> Autocomplete.withItemHtml renderItem

viewSelect item data state selected =
  case data of
    RemoteData.Success allDomains ->
      let
        selectedItem =
          case selected of
            Just s -> [ s ]
            _ -> []

        relevantDomains =
          allDomains
          |> List.filter (\d -> not (d.id == item.id) )
        autocompleteSelect =
          Autocomplete.view
            selectConfig
            state
            relevantDomains
            selectedItem
        in
          Fieldset.config
              |> Fieldset.attrs [ Spacing.ml4 ]
              |> Fieldset.children (autocompleteSelect |> Html.map SubdomainSelectMsg |> List.singleton)
              |> Fieldset.view
    _ -> Html.p[] [ text "Loading domains" ]

viewMove : MoveDomainModel -> Html MoveDomainMsg
viewMove model =
  Modal.config Cancel
  |> Modal.hideOnBackdropClick True
  |> Modal.h5 [] [ text <| "Move " ++ model.domainItem.domain.name ]
  |> Modal.body []
    [ Html.p []
      ( Radio.radioList "move-target"
        [ Radio.create
          [ Radio.id "root-domain"
          , Radio.checked (model.moveTo == AsRoot && model.canMoveToRoot)
          , Radio.onClick WillMoveToRoot
          , Radio.attrs [ disabled (not model.canMoveToRoot) ]
          ]
          "Promote to root domain"
        , Radio.create
          [ Radio.id "sub-domain"
          , Radio.checked (not (model.moveTo == AsRoot) || not model.canMoveToRoot )
          , Radio.onClick WillMoveToSubdomain
          ]
          "Make a subdomain of"
        ]
      )
    , case model.moveTo of
        AsRoot -> div [][]
        AsSubdomain state selected ->
          viewSelect model.domainItem.domain model.allDomains state selected
    ]
  |> Modal.footer []
      [ Button.button
          [ Button.primary
          , Button.disabled
            ( case model.moveTo of
                AsSubdomain _ Nothing -> True
                _ -> False
            )
          , Button.attrs [ Html.Events.onClick MoveDomainTo ]
          ]
          [ text "Move domain" ]
      ]
  |> Modal.view model.modalVisibility

view : Model -> Html Msg
view model =
  let
    details =
      case model.domains of
        RemoteData.Success items ->
          items
          |> viewLoaded model.createDomain
          |> List.append [
              model.moveToNewDomain
              |> Maybe.map viewMove
              |> Maybe.map (Html.map MoveToDomain)
              |> Maybe.withDefault (Html.div [] [])
          ]
        _ ->
          [ Grid.row []
              [ Grid.col [] [ text "Loading your domains"] ]
          ]
  in
    case model.showDomains of
      Domain.Subdomain _ -> div [] details
      Domain.Root -> Grid.container [] details



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

domainsOf : Api.Configuration -> DomainRelation -> ApiResult (List DomainItem) msg
domainsOf base relation =
  let
    include = [ Api.Subdomains, Api.BoundedContexts ]
    (api, predicate) =
      case relation of
        Domain.Root ->
          (Api.domains include, Domain.isSubDomain >> not)
        Domain.Subdomain id ->
          (Api.subDomains include id, Domain.isSubDomain)

    filter : Api.ApiResponse (List DomainItem) -> Api.ApiResponse (List DomainItem)
    filter result =
      case result of
         Ok items -> items |> List.filter (.domain >> predicate) |> Ok
         Err e -> Err e
    decoder =
      Decode.succeed DomainItem
        |> JP.custom Domain.domainDecoder
        |> JP.required "domains" (Decode.list Domain.domainDecoder)
        |> JP.required "bccs" (Decode.list BoundedContext.modelDecoder)
    request toMsg =
      Http.get
        { url = api |> Api.url base |> Url.toString
        , expect = Http.expectJson (filter >> toMsg) (Decode.list decoder)
        }
  in
    request