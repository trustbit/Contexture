module Page.Search.Searching exposing (..)

import Api as Api
import Bootstrap.Button as Button
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Grid.Row as Row
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
import Page.Search.Filter as Filter
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


init apiBase domains collaboration initialQuery =
    let
        ( filterModel, filterCmd ) =
            Filter.init apiBase initialQuery
    in
    ( { configuration = apiBase
      , domains = domains
      , collaboration = collaboration
      , searchResults = RemoteData.Loading
      , filter = filterModel
      }
    , Cmd.batch
        [ filterCmd |> Cmd.map FilterMsg ]
    )


type alias Model =
    { configuration : Api.Configuration
    , domains : List Domain
    , collaboration : Collaborations
    , searchResults : RemoteData.WebData (List BoundedContext.Model)
    , filter : Filter.Model
    }


updateFilter : (Filter.Model -> Filter.Model) -> Model -> Model
updateFilter apply model =
    { model | filter = apply model.filter }


type Msg
    = BoundedContextsFound (Api.ApiResponse (List BoundedContextCard.Item))
    | BoundedContextMsg BoundedContext.Msg
    | FilterMsg Filter.Msg


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        BoundedContextMsg m ->
            ( model, Cmd.none )

        BoundedContextsFound foundItems ->
            ( { model
                | searchResults =
                    foundItems
                        |> RemoteData.fromResult
                        |> RemoteData.map (initSearchResult model.configuration model.collaboration model.domains)
              }
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


viewItems : List BoundedContext.Model -> List (Html Msg)
viewItems items =
    items
        |> List.map BoundedContext.view
        |> List.map (Html.map BoundedContextMsg)


view : Model -> Html Msg
view model =
    case model.searchResults of
        RemoteData.Success items ->
            Grid.container []
                ((model.filter |> Filter.view |> Html.map FilterMsg)
                    :: viewItems items
                )

        e ->
            text <| "Could not execute search: " ++ Debug.toString e


findAll : Api.Configuration -> List Filter.FilterParameter -> Cmd Msg
findAll config query =
    Http.get
        { url =
            Api.allBoundedContexts []
                |> Api.urlWithQueryParameters config (query |> List.map (\q -> Url.Builder.string q.name q.value))
        , expect = Http.expectJson BoundedContextsFound (Decode.list BoundedContextCard.decoder)
        }
