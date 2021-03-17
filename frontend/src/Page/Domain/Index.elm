module Page.Domain.Index exposing (Msg, Model, update, view, initWithSubdomains, initWithoutSubdomains)

import Browser.Navigation as Nav

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events

import Json.Decode as Decode
import Json.Decode.Pipeline as JP

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Button as Button
import Bootstrap.Modal as Modal
import Bootstrap.Badge as Badge
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Border as Border
import Bootstrap.Text as Text

import Select as Autocomplete

import List.Split exposing (chunksOfLeft)

import RemoteData
import Url
import Http

import Route
import Api exposing (ApiResult, ApiResponse)

import Key
import Domain exposing (DomainRelation, Domain)
import Domain.DomainId exposing (DomainId)
import BoundedContext exposing (BoundedContext)

import Page.Domain.Create

-- MODEL

type alias DomainItem =
  { domain : Domain
  , subDomains : List Domain
  , contexts : List BoundedContext
  }

type alias SubdomainSelection = 
  { state : Autocomplete.State
  , selected : Maybe Domain.Domain }

type MoveDomainTarget
  = AsSubdomain SubdomainSelection
  | AsRoot

type alias MoveDomainModel =
  { domain : Domain
  , moveTo : MoveDomainTarget
  , canMoveToRoot : Bool
  , allDomains : RemoteData.WebData (List Domain.Domain)
  , modalVisibility : Modal.Visibility
  }

type alias DeleteDomainModel =
  { domain : Domain
  , modalVisibility : Modal.Visibility
  }

type alias Model =
  { navKey : Nav.Key
  , config : Api.Configuration
  , domainPosition : Domain.DomainRelation
  , createDomain : Page.Domain.Create.Model
  , domains : RemoteData.WebData (List DomainItem)
  , moveToNewDomain : Maybe MoveDomainModel
  , deleteDomain : Maybe DeleteDomainModel
   }

initSubdomainSelection : SubdomainSelection
initSubdomainSelection = 
  { state = Autocomplete.newState "sub-domain"
  , selected = Nothing
  }

initMove : Api.Configuration -> DomainItem -> (MoveDomainModel, Cmd MoveDomainMsg)
initMove config { domain } =
  let
    canMoveToRoot =  not ((domain |> Domain.domainRelation) == Domain.Root)
  in

  ( { allDomains = RemoteData.Loading
    , moveTo = if canMoveToRoot then AsRoot else AsSubdomain initSubdomainSelection
    , domain = domain
    , canMoveToRoot = canMoveToRoot
    , modalVisibility = Modal.shown
    }
  , findAllDomains config AllDomainsLoaded
  )

init : Api.Configuration -> Nav.Key -> Domain.DomainRelation -> (Model, Cmd Msg)
init config key domainPosition =
  let
    (createModel, createCmd) = Page.Domain.Create.init config key domainPosition
  in
  ( { navKey = key
    , config = config
    , domainPosition = domainPosition
    , createDomain = createModel
    , domains = RemoteData.Loading
    , moveToNewDomain = Nothing
    , deleteDomain = Nothing
    }
  , Cmd.batch
    [ domainsOf config domainPosition Loaded
    , createCmd |> Cmd.map CreateMsg ] )

initWithSubdomains : Api.Configuration -> Nav.Key -> DomainId -> (Model, Cmd Msg)
initWithSubdomains baseUrl key parentDomain =
  init baseUrl key (Domain.Subdomain parentDomain)

initWithoutSubdomains : Api.Configuration -> Nav.Key -> (Model, Cmd Msg)
initWithoutSubdomains baseUrl key =
  init baseUrl key Domain.Root


-- UPDATE

type MoveDomainMsg
  = AllDomainsLoaded (ApiResponse (List Domain.Domain))
  | WillMoveToRoot
  | WillMoveToSubdomain
  | MoveDomainTo
  | DomainMoved (ApiResponse ())
  | Cancel
  | SubdomainSelectMsg (Autocomplete.Msg Domain.Domain)
  | SubdomainSelected (Maybe Domain.Domain)

type Msg
  = Loaded (ApiResponse (List DomainItem))
  | CreateMsg Page.Domain.Create.Msg
  | ShouldDelete Domain
  | CancelDelete
  | DeleteDomain DomainId
  | DomainDeleted (ApiResponse ())
  | StartMoveToDomain DomainItem
  | MoveToDomain MoveDomainMsg

updateMoveToDomain : Api.Configuration -> MoveDomainMsg -> MoveDomainModel -> (MoveDomainModel, Cmd MoveDomainMsg)
updateMoveToDomain baseUrl msg model =
  case (msg, model.moveTo) of
    (AllDomainsLoaded (Ok allDomains), _) ->
      ({ model | allDomains = RemoteData.succeed allDomains }, Cmd.none)

    (WillMoveToRoot, _) ->
      ({ model | moveTo = AsRoot }, Cmd.none)

    (WillMoveToSubdomain, _) ->
      ({ model | moveTo = AsSubdomain initSubdomainSelection }, Cmd.none)

    (SubdomainSelectMsg selMsg, AsSubdomain selected) ->
      let
        ( updated, cmd ) =
          Autocomplete.update selectConfig selMsg selected.state
      in
        ( { model | moveTo = AsSubdomain { selected | state = updated } }, cmd )

    (SubdomainSelected item, AsSubdomain selectedModel )->
      ({ model | moveTo = AsSubdomain { selectedModel | selected = item } }, Cmd.none)

    (MoveDomainTo, AsRoot) ->
      (model, Domain.moveDomain baseUrl (model.domain |> Domain.id) Domain.Root DomainMoved)

    (MoveDomainTo, AsSubdomain { selected }) ->
      case selected of
        Just parentDomain ->
          (model, Domain.moveDomain baseUrl (model.domain |> Domain.id) (parentDomain |> Domain.id |> Domain.Subdomain) DomainMoved)
        Nothing ->
          (model, Cmd.none)

    (Cancel, _) ->
      ({ model | modalVisibility = Modal.hidden }, Cmd.none)

    (DomainMoved (Ok _), _) ->
      ({ model | modalVisibility = Modal.hidden }, Cmd.none)

    (m, _) ->
      let
        _ = Debug.log "Move: unhandled message" m
      in
        (Debug.log "Move: Unhandled model" model, Cmd.none)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Loaded (Ok items) ->
      ({ model | domains = RemoteData.Success items }, Cmd.none)

    Loaded (Err e) ->
      ({ model | domains = RemoteData.Failure e }, Cmd.none)

    StartMoveToDomain item ->
      let
        (moveModel, moveCmd) = initMove model.config item
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
                  [ domainsOf model.config model.domainPosition Loaded ]
                _ -> []
            (moveModel_, moveCmd) = updateMoveToDomain model.config move moveModel
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
    ShouldDelete domain ->
      ( { model | deleteDomain = Just { domain = domain, modalVisibility = Modal.shown } }, Cmd.none)

    CancelDelete ->
      ( { model | deleteDomain = Nothing }, Cmd.none)

    DeleteDomain id ->
      ( { model | deleteDomain = Nothing }, Domain.remove model.config id DomainDeleted)

    DomainDeleted (Ok _) ->
      ( { model | deleteDomain = Nothing }, domainsOf model.config model.domainPosition Loaded)

    DomainDeleted (Err _) ->
      (model, Cmd.none)


-- VIEW

viewDomain : DomainItem -> Card.Config Msg
viewDomain item =
  Card.config [ Card.attrs [ class "mb-3", class "shadow"] ]
    |> Card.block []
      [ Block.titleH4 []
        [ text (item.domain |> Domain.name)
        , Html.small [ class "text-muted", class "float-right"]
          [ text (item.domain |> Domain.key |> Maybe.map Key.toString |> Maybe.withDefault "") ] 
        ]
      , item.domain
        |> Domain.vision
        |> Maybe.map (\v -> Block.text [ class "text-muted"] [ text v ] )
        |> Maybe.withDefault
          ( Block.text
            [ class "text-muted", class "text-center"]
            [ Html.i [] [ text "This domain is not backed by any vision :-(" ] ]
          )
      ]
    |> Card.block []
      [ item.subDomains
        |> List.map Domain.name
        |> List.map (\name -> Badge.badgePrimary [ Spacing.mr1, title <| "Subdomain " ++ name ] [text name] )
        |> Html.div []
        |> Block.custom
      , item.contexts
        |> List.map BoundedContext.name
        |> List.map (\name -> Badge.pillSecondary [ Spacing.mr1, title <| "Bounded context " ++ name ] [text name] )
        |> Html.div []
        |> Block.custom
      ]
    |> Card.footer []
      [ Grid.simpleRow
        [ Grid.col []
          [ Button.linkButton
            [ Button.roleLink
            , Button.attrs [ href (Route.routeToString (item.domain |> Domain.id |> Route.Domain)) ]
            ]
            [ text "View Domain" ]
          ]
        , Grid.col [ Col.textAlign Text.alignLgRight ]
          [ Button.button
            [ Button.secondary
            , Button.onClick (StartMoveToDomain item)
            ]
            [ text "Move Domain"]
          , Button.button
            [ Button.secondary
            , Button.onClick (ShouldDelete item.domain)
            , Button.attrs
              [ title ("Delete " ++ (item.domain |> Domain.name))
              , Spacing.ml3
              ]
            ]
            [ text "Delete" ]
          ]
        ]
      ]

viewLoaded : Page.Domain.Create.Model -> List DomainItem  -> List (Html Msg)
viewLoaded create items =
  if List.isEmpty items then
    let
      caption =
        case create.relation of
          Domain.Root -> "No existing domains found - do you want to create one?"
          Domain.Subdomain _ -> "No subdomains found - do you want to create one?"
    in  
    [ Grid.row []
      [ Grid.col []
        [ div [ Spacing.p5, class "shadow", Border.all]
          [ Html.p
            [ class "lead", class "text-center" ]
            [ text caption ]
          , create |> Page.Domain.Create.view |> Html.map CreateMsg
          ]
        ]
      ]
    ]
  else
    let
      domainCards =
        items
        |> List.map viewDomain
        |> chunksOfLeft 2
        |> List.map Card.deck
      createDomainAction =
        create |> Page.Domain.Create.view |> Html.map CreateMsg
    in
      case create.relation of 
        Domain.Root ->
          [ Grid.simpleRow
            [ Grid.col[] domainCards ]
          , Grid.row [ Row.attrs [ Spacing.mt3 ] ]
            [ Grid.col [] [ createDomainAction ] ]
          ]
        Domain.Subdomain _ ->
          [ Card.config []
            |> Card.headerH5 [] [ text "Subdomains"]
            |>Card.block []
                ( domainCards |> List.map Block.custom )
            |> Card.footer []
              [ createDomainAction ]
            |> Card.view
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


selectConfig : Autocomplete.Config MoveDomainMsg Domain.Domain
selectConfig =
    Autocomplete.newConfig
        { onSelect = SubdomainSelected
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

viewSelect : Domain -> (RemoteData.WebData (List Domain)) -> Autocomplete.State -> Maybe Domain -> Html MoveDomainMsg
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
          |> List.filter (\d -> not (d == item))

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
    _ -> Html.p [] [ text "Loading domains" ]

viewMoveSubdomainOrRoot : MoveDomainModel -> MoveDomainTarget -> Modal.Config MoveDomainMsg -> Modal.Config MoveDomainMsg
viewMoveSubdomainOrRoot { domain, allDomains } target modal =
  let
    rootIsChecked = target == AsRoot
  in
    modal
    |> Modal.body []
      [ Html.p []
        ( Radio.radioList "move-target"
          [ Radio.create
            [ Radio.id "root-domain"
            , Radio.checked rootIsChecked
            , Radio.onClick WillMoveToRoot 
            ]
            "Promote to root domain"
          , Radio.create
            [ Radio.id "sub-domain"
            , Radio.checked (not rootIsChecked)
            , Radio.onClick WillMoveToSubdomain
            ]
            "Make a subdomain of"
          ]
        )
      , case target of
          AsRoot -> div [][]
          AsSubdomain { state, selected } ->
            viewSelect domain allDomains state selected
      ]
    |> Modal.footer []
      [ Button.button
        [ Button.primary
        , Button.disabled
          ( case target of
              AsSubdomain { selected } -> selected == Nothing
              _ -> False
          )
        , Button.attrs [ Html.Events.onClick MoveDomainTo ]
        ]
        [ text "Move domain" ]
      ]

viewMoveSubdomainOnly : MoveDomainModel -> SubdomainSelection -> Modal.Config MoveDomainMsg -> Modal.Config MoveDomainMsg
viewMoveSubdomainOnly { domain, allDomains } { state, selected } modal =
  modal
  |> Modal.body []
    [ Html.p []
      [ text "This domain is already a root domain, but you can chose a new parent and convert it into a subdomain."]
    , viewSelect domain allDomains state selected
    ]
  |> Modal.footer []
    [ Button.button
      [ Button.primary
      , Button.disabled (selected == Nothing)
      , Button.attrs [ Html.Events.onClick MoveDomainTo ]
      ]
      [ text "Move domain to new parent" ]
    ]


viewMove : MoveDomainModel -> Html MoveDomainMsg
viewMove model =
  Modal.config Cancel
  |> Modal.hideOnBackdropClick True
  |> Modal.h5 [] [ text <| "Move " ++ (model.domain |> Domain.name) ]
  |>
    ( case model.moveTo of
        AsRoot -> 
          viewMoveSubdomainOrRoot model model.moveTo
        AsSubdomain selected ->
          if model.canMoveToRoot
          then viewMoveSubdomainOrRoot model model.moveTo
          else viewMoveSubdomainOnly model selected
    )
  |> Modal.view model.modalVisibility

viewDelete : DeleteDomainModel -> Html Msg
viewDelete model =
  Modal.config CancelDelete
  |> Modal.hideOnBackdropClick True
  |> Modal.h5 [] [ text <| "Delete " ++ (model.domain |> Domain.name) ]
  |> Modal.body [] [  text "Should the domain, all of it's sub-domains and bounded contexts be deleted?" ]
  |> Modal.footer []
    [ Button.button [ Button.outlinePrimary, Button.onClick CancelDelete ] [ text "Cancel delete" ] 
    , Button.button [ Button.primary, Button.onClick (model.domain |> Domain.id |> DeleteDomain ) ] [ text "Delete domain" ] ]
  |> Modal.view model.modalVisibility

view : Model -> Html Msg
view model =
  let
    details =
      case model.domains of
        RemoteData.Success items ->
          items
          |> viewLoaded model.createDomain
          |> List.append
            ( [ model.moveToNewDomain
                |> Maybe.map viewMove
                |> Maybe.map (Html.map MoveToDomain)
              , model.deleteDomain
                |> Maybe.map  viewDelete
              ] |> List.map (Maybe.withDefault (text ""))
            )
        _ ->
          [ Grid.row []
              [ Grid.col [] [ text "Loading your domains"] ]
          ]
  in
    case model.domainPosition of
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
    isRootDomain domain =
      (domain |> Domain.domainRelation) == Domain.Root

    (api, predicate) =
      case relation of
        Domain.Root ->
          (Api.domains include, isRootDomain)
        Domain.Subdomain id ->
          (Api.subDomains include id, isRootDomain >> not)

    filter : Api.ApiResponse (List DomainItem) -> Api.ApiResponse (List DomainItem)
    filter result =
      case result of
         Ok items -> items |> List.filter (.domain >> predicate) |> Ok
         Err e -> Err e
    decoder =
      Decode.succeed DomainItem
        |> JP.custom Domain.domainDecoder
        |> JP.optional "subdomains" (Decode.list Domain.domainDecoder) []
        |> JP.optional "boundedContexts" (Decode.list BoundedContext.modelDecoder) []
    request toMsg =
      Http.get
        { url = api |> Api.url base |> Url.toString
        , expect = Http.expectJson (filter >> toMsg) (Decode.list decoder)
        }
  in
    request