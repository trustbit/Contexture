module Page.Searching.Searching exposing (..)

import Api as Api
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Grid.Row as Row
import Bootstrap.Utilities.Border as Border
import Bootstrap.Utilities.Spacing as Spacing
import Bounce exposing (Bounce)
import BoundedContext as BoundedContext
import BoundedContext.BoundedContextId as BoundedContextId
import BoundedContext.Canvas
import BoundedContext.Namespace as Namespace exposing (NamespaceTemplateId)
import Browser
import Components.BoundedContextCard as BoundedContextCard
import Components.BoundedContextsOfDomain as BoundedContext 
import ContextMapping.Collaboration as Collaboration exposing (Collaborations)
import Dict
import Domain exposing (Domain)
import Domain.DomainId as DomainId
import Html exposing (Html, div, text)
import Html.Attributes as Attributes exposing (..)
import Http
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import Page.Searching.Filter as Filter
import Page.Searching.Ports as Ports
import RemoteData
import Task
import Url
import Url.Builder
import Url.Parser
import Url.Parser.Query


initSearchResult : Api.Configuration -> BoundedContext.Presentation -> Collaboration.Collaborations -> List Domain -> List BoundedContextCard.Item -> List BoundedContext.Model
initSearchResult config presentation collaboration domains searchResults =
    let
        groupItemsByDomainId item grouping =
            grouping
                |> Dict.update
                    (item.context |> BoundedContext.domain |> DomainId.idToString)
                    (\maybeContexts ->
                        case maybeContexts of
                            Just boundedContexts ->
                                Just (item :: boundedContexts)

                            Nothing ->
                                Just (List.singleton item)
                    )

        boundedContextsPerDomain =
            searchResults
                |> List.foldl groupItemsByDomainId Dict.empty

        getContexts domain =
            boundedContextsPerDomain
                |> Dict.get (domain |> Domain.id |> DomainId.idToString)
                |> Maybe.withDefault []
    in
    domains
        |> List.map (\domain -> BoundedContext.init config presentation domain (getContexts domain) collaboration)
        |> List.filter (\i -> not <| List.isEmpty i.contextItems)


init apiBase initialQuery presentation =
    let
        ( filterModel, filterCmd ) =
            Filter.init apiBase initialQuery
    in
    ( { configuration = apiBase
      , domains = RemoteData.Loading
      , collaboration = RemoteData.Loading
      , searchResults = RemoteData.NotAsked
      , searchResponse = RemoteData.Loading
      , filter = filterModel
      , presentation = presentation
      }
    , Cmd.batch
        [ filterCmd |> Cmd.map FilterMsg
        , getDomains apiBase
        , getCollaborations apiBase
        ]
    )
    
type SearchResultPresentation
    = Textual BoundedContext.Presentation
    | Sunburst


type alias Model =
    { configuration : Api.Configuration
    , domains : RemoteData.WebData (List Domain)
    , searchResponse : RemoteData.WebData (List BoundedContextCard.Item)
    , collaboration : RemoteData.WebData Collaborations
    , searchResults : RemoteData.WebData (List BoundedContext.Model)
    , filter : Filter.Model
    , presentation : SearchResultPresentation
    }


updateFilter : (Filter.Model -> Filter.Model) -> Model -> Model
updateFilter apply model =
    { model | filter = apply model.filter }


type Msg
    = DomainsLoaded (Api.ApiResponse (List Domain))
    | CollaborationsLoaded (Api.ApiResponse Collaborations)
    | BoundedContextsFound (Api.ApiResponse (List BoundedContextCard.Item))
    | BoundedContextMsg BoundedContext.Msg
    | FilterMsg Filter.Msg
    | SwitchPresentation SearchResultPresentation


updateSearchResults presentation model =
    { model
        | searchResults =
            RemoteData.map3
                (initSearchResult model.configuration presentation)
                model.collaboration
                model.domains
                model.searchResponse
    }


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case (model.presentation,msg) of
        (_,BoundedContextMsg m) ->
            ( model, Cmd.none )

        (Textual presentation, BoundedContextsFound foundItems)->
            ( updateSearchResults presentation
                { model | searchResponse = foundItems |> RemoteData.fromResult }
            , Cmd.none
            )

        (Textual presentation, DomainsLoaded domains) ->
            ( updateSearchResults presentation
                { model | domains = domains |> RemoteData.fromResult }
            , Cmd.none
            )

        (Textual presentation, CollaborationsLoaded collaborations)->
            ( updateSearchResults presentation
                { model | collaboration = collaborations |> RemoteData.fromResult }
            , Cmd.none
            )

        (_, FilterMsg msg_) ->
            let
                ( filterModel, filterCmd, outMsg ) =
                    Filter.update msg_ model.filter
            in
            ( { model | filter = filterModel }
            , Cmd.batch
                (Cmd.map FilterMsg filterCmd
                    :: (case outMsg of
                            Filter.NoOp ->
                                []

                            Filter.FilterApplied query ->
                                [ findAll model.configuration query ]
                       )
                )
            )

        (_, SwitchPresentation newPresentation) ->
            ( { model | presentation = newPresentation }
                |>
                case newPresentation of
                    Textual presentation ->
                        updateSearchResults presentation
                    Sunburst ->
                        identity
            , Ports.storePresentation <|
                case newPresentation of
                    Textual BoundedContext.Full ->
                        "Textual:Full"

                    Textual BoundedContext.Condensed ->
                        "Textual:Condensed"
                    
                    Sunburst ->
                        "Sunburst"
            )
        _ ->
            (model, Cmd.none)


stickyTopAttributes =
    [ class "sticky-top", style "top" "5rem", style "height" "calc(100vh - 5rem)" ]


viewItems : RemoteData.WebData (List BoundedContext.Model) -> List (Html Msg)
viewItems searchResults =
    case searchResults of
        RemoteData.Success items ->
            [ Grid.row []
                [ Grid.col [ Col.xs3 ]
                    [ Html.h5 [] [ text "Search results" ] ]
                , if List.isEmpty items then
                    Grid.col [] [ text "No items found!" ]

                  else
                    Grid.col []
                        [ Html.b [] [ text (items |> List.length |> String.fromInt) ]
                        , text " Domain(s) with "
                        , Html.b [] [ text (items |> List.map (\b -> b.contextItems |> List.length) |> List.sum |> String.fromInt) ]
                        , text " Bounded Context(s)"
                        ]
                ]
            , Grid.row [ Row.attrs [ Spacing.mt2, Border.top ] ]
                [ Grid.col []
                    (
                            items
                            |> List.sortBy (\b -> b.domain |> Domain.name)
                            |> List.map BoundedContext.view
                            |> List.map (Html.map BoundedContextMsg)       
                    )
                ]
            ]

        e ->
            [ Grid.simpleRow [ Grid.col [] [ text <| "Could not execute search: " ++ Debug.toString e ] ]
            ]
            
viewSunburst configuration query =
     Html.node "visualization-sunburst"
        [ attribute "baseApi" (Api.withoutQuery [] |> Api.url configuration)
        , attribute "query" (query |> List.map (\q -> Url.Builder.string q.name q.value) |> Url.Builder.toQuery)
        ]   
        []


presentationOptionView presentation =
    Card.config []
        |> Card.block []
            [ Block.titleH5 [] [ text "Presentation mode" ]
            , Block.text []
                [ ButtonGroup.radioButtonGroup []
                    [ ButtonGroup.radioButton
                        (presentation == Textual BoundedContext.Full)
                        [ Button.secondary, Button.onClick <| SwitchPresentation (Textual BoundedContext.Full) ]
                        [ text "Full" ]
                    , ButtonGroup.radioButton
                        (presentation == Textual BoundedContext.Condensed)
                        [ Button.secondary, Button.onClick <| SwitchPresentation (Textual BoundedContext.Condensed) ]
                        [ text "Condensed" ]
                    , ButtonGroup.radioButton
                        (presentation == Sunburst)
                        [ Button.secondary, Button.onClick <| SwitchPresentation Sunburst]
                        [ text "Sunburst" ]
                    ]
                ]
            ]
        |> Card.view


view : Model -> Html Msg
view model =
    Grid.containerFluid []
        [ Grid.simpleRow
            [ Grid.col [ Col.md4, Col.attrs stickyTopAttributes ]
                [ Grid.row [ Row.attrs [ Spacing.mb3 ] ]
                    [ Grid.col [] [ model.filter |> Filter.view |> Html.map FilterMsg ] ]
                , Grid.row [ Row.attrs [ Spacing.mb3 ] ]
                    [ Grid.col [] [ model.presentation |> presentationOptionView ] ]
                ]
            , Grid.col [ Col.attrs [ class "position-relative" ] ] (
                case model.presentation of
                    Textual _ ->
                        viewItems model.searchResults
                    Sunburst ->
                        [ viewSunburst model.configuration model.filter.initialParameters]
                )
            ]
        ]


findAll : Api.Configuration -> List Filter.FilterParameter -> Cmd Msg
findAll config query =
    Http.get
        { url =
            Api.allBoundedContexts []
                |> Api.urlWithQueryParameters config (query |> List.map (\q -> Url.Builder.string q.name q.value))
        , expect = Http.expectJson BoundedContextsFound (Decode.list BoundedContextCard.decoder)
        }


getDomains : Api.Configuration -> Cmd Msg
getDomains config =
    Http.get
        { url = Api.domains [] |> Api.url config
        , expect = Http.expectJson DomainsLoaded (Decode.list Domain.domainDecoder)
        }


getCollaborations : Api.Configuration -> Cmd Msg
getCollaborations config =
    Http.get
        { url = Api.collaborations |> Api.url config
        , expect = Http.expectJson CollaborationsLoaded (Decode.list Collaboration.decoder)
        }
