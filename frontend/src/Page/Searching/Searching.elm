module Page.Searching.Searching exposing (..)

import Api as Api
import Bootstrap.Button as Button
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Grid.Row as Row
import Bootstrap.Text as Text
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
import RemoteData
import Task
import Url
import Url.Builder
import Url.Parser
import Url.Parser.Query


initSearchResult : Api.Configuration -> Collaboration.Collaborations -> List Domain -> List BoundedContextCard.Item -> List BoundedContext.Model
initSearchResult config collaboration domains searchResults =
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
        |> List.map (\domain -> BoundedContext.init config domain (getContexts domain) collaboration)
        |> List.filter (\i -> not <| List.isEmpty i.contextItems)


init apiBase initialQuery =
    let
        ( filterModel, filterCmd ) =
            Filter.init apiBase initialQuery
    in
    ( { configuration = apiBase
      , domains = RemoteData.Loading
      , collaboration =  RemoteData.Loading
      , searchResults = RemoteData.NotAsked
      , searchResponse = RemoteData.Loading
      , filter = filterModel
      }
    , Cmd.batch
        [ filterCmd |> Cmd.map FilterMsg
        , getDomains apiBase
        , getCollaborations apiBase
        ]
    )


type alias Model =
    { configuration : Api.Configuration
    , domains : RemoteData.WebData (List Domain)
    , searchResponse : RemoteData.WebData (List BoundedContextCard.Item)
    , collaboration : RemoteData.WebData Collaborations
    , searchResults : RemoteData.WebData (List BoundedContext.Model)
    , filter : Filter.Model
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


updateSearchResults model =
    { model
        | searchResults =
            RemoteData.map3
                (initSearchResult model.configuration)
                model.collaboration
                model.domains
                model.searchResponse
    }


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        BoundedContextMsg m ->
            ( model, Cmd.none )

        BoundedContextsFound foundItems ->
            ( updateSearchResults
                { model | searchResponse = foundItems |> RemoteData.fromResult }
            , Cmd.none
            )

        DomainsLoaded domains ->
            ( updateSearchResults
                { model | domains = domains |> RemoteData.fromResult }
            , Cmd.none
            )
            
        CollaborationsLoaded collaborations ->
            ( updateSearchResults
                { model | collaboration = collaborations |> RemoteData.fromResult }
            , Cmd.none
            )

        FilterMsg msg_ ->
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


stickyTopAttributes = [ class "sticky-top", style "top" "5rem", style "height" "calc(100vh - 5rem)"]

viewItems : RemoteData.WebData (List BoundedContext.Model) -> List (Html Msg)
viewItems searchResults =
    case searchResults of
        RemoteData.Success items ->
            [ Grid.row [ ]
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
                    (items
                        |> List.map BoundedContext.view
                        |> List.map (Html.map BoundedContextMsg)
                    )
                ]
            ]

        e ->
            [ Grid.simpleRow [ Grid.col [] [ text <| "Could not execute search: " ++ Debug.toString e ] ]
            ]


view : Model -> Html Msg
view model =
    Grid.containerFluid []
        [ Grid.row [Row.attrs[]]
            [ Grid.col [Col.md4, Col.attrs stickyTopAttributes]
                [ model.filter |> Filter.view |> Html.map FilterMsg ]
            , Grid.col [ Col.attrs [ class "position-relative"]] (viewItems model.searchResults)
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
