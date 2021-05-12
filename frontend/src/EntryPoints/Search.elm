module EntryPoints.Search exposing (main)

import Url
import Http
import RemoteData

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col

import Api as Api
import Components.BoundedContextCard as BoundedContextCard
import BoundedContext as BoundedContext
import BoundedContext.BoundedContextId as BoundedContextId
import Domain.DomainId as DomainId
import BoundedContext.Canvas
import BoundedContext.Namespace as Namespace
import Browser
import ContextMapping.Collaboration as Collaboration exposing (Collaborations)
import Domain exposing (Domain)
import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import Components.BoundedContextsOfDomain as BoundedContext
import Url
import Url.Builder exposing (QueryParameter)
import Url.Parser
import Url.Parser.Query
import Dict
import BoundedContext.Namespace exposing (NamespaceTemplateId)


main =
    Browser.element
        { init = init
        , update = update
        , view = view
        , subscriptions = subscriptions
        }


initModel : Api.Configuration -> Collaboration.Collaborations -> List BoundedContextCard.Item -> List Domain -> List BoundedContext.Model
initModel config collaboration items domains =
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
            items
            |> List.foldl groupItemsByDomainId Dict.empty

        getContexts domain =
            boundedContextsPerDomain
            |> Dict.get (domain |> Domain.id |> DomainId.idToString)
            |> Maybe.withDefault []

    in
        domains
        |> List.map (\domain -> BoundedContext.init config domain (getContexts domain) collaboration)
        |> List.filter(\i -> not <| List.isEmpty i.contextItems)

initFilter : List QueryParameter -> Filter
initFilter query = 
    { query = query
    , namespaceFilter = RemoteData.Loading
    }

init : Decode.Value -> ( Model, Cmd Msg )
init flag =
    case flag |> Decode.decodeValue flagsDecoder of
        Ok decoded ->
            (
                { configuration = decoded.apiBase
                , domains = decoded.domains
                , collaboration = decoded.collaboration
                , items = RemoteData.Loading
                , models = RemoteData.Loading
                , filter = initFilter decoded.initialQuery
                }
            , Cmd.batch 
                [ findAll decoded.apiBase decoded.initialQuery
                , getNamespaceFilters decoded.apiBase
                ]
            )

        Err e ->
            ( Debug.log "Error on initializing"
                { configuration = Api.baseConfig ""
                , domains = []
                , filter = initFilter []
                , collaboration = []
                , items = RemoteData.Failure <| Http.BadBody (Debug.toString e)
                , models = RemoteData.Failure <| Http.BadBody (Debug.toString e)
                }
            , Cmd.none
            )


type alias Flags =
    { collaboration : Collaborations
    , domains : List Domain
    , apiBase : Api.Configuration
    , initialQuery : List QueryParameter
    }


baseConfiguration =
    Decode.string
        |> Decode.andThen
            (\v ->
                case v |> Url.fromString of
                    Just url ->
                        url |> Api.config |> Decode.succeed
                    Nothing ->
                        if not <| String.isEmpty v
                        then v |> Api.baseConfig |> Decode.succeed
                        else Decode.fail <| "Could not decode url from " ++ v
            )

queryDecoder =
    Decode.map2 QueryParameter
        (Decode.field "name" Decode.string)
        (Decode.field "value" Decode.string)


flagsDecoder =
    Decode.map4 Flags
        (Decode.field "collaboration" (Decode.list Collaboration.decoder))
        (Decode.field "domains" (Decode.list Domain.domainDecoder))
        (Decode.field "apiBase" baseConfiguration)
        (Decode.field "initialQuery" (Decode.list queryDecoder))

type alias QueryParameter =
    { name : String
    , value : String
    }

type alias NamespaceFilterDescription =
    { name : String
    , description : Maybe String
    , templateId : Maybe NamespaceTemplateId
    , labels : List String
    }

type alias NamespaceFilter =
    { withTemplate : NamespaceFilterDescription
    , withoutTemplate : NamespaceFilterDescription
    }

namespaceFilterDescriptionDecoder =
    Decode.map4 NamespaceFilterDescription
        (Decode.field "name" Decode.string)
        (Decode.maybe (Decode.field "description" Decode.string))
        (Decode.maybe (Decode.field "templateId" Decode.string))
        (Decode.field "labels" (Decode.list Decode.string))

namespaceFilterDecoder =
    Decode.map2 NamespaceFilter
        (Decode.field "withTemplate" namespaceFilterDescriptionDecoder)
        (Decode.field "withoutTemplate" namespaceFilterDescriptionDecoder)

type alias Filter =
    { namespaceFilter : RemoteData.WebData NamespaceFilter
    , query : List QueryParameter
    }

type alias Model =
    { configuration : Api.Configuration
    , domains : List Domain
    , collaboration : Collaborations
    , items : RemoteData.WebData (List BoundedContextCard.Item)
    , models : RemoteData.WebData (List BoundedContext.Model)
    , filter : Filter
    }


type Msg
    = BoundedContextsFound (Api.ApiResponse (List BoundedContextCard.Item))
    | NamespaceFiltersLoaded (Api.ApiResponse NamespaceFilter)
    | BoundedContextMsg BoundedContext.Msg


updateModels : Model -> Model
updateModels model =
    { model
    | models =
        model.items
        |> RemoteData.map (\items ->
            initModel model.configuration model.collaboration items model.domains
        )
    }


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        BoundedContextMsg m ->
            ( model, Cmd.none )

        BoundedContextsFound found ->
            ( updateModels { model | items = RemoteData.fromResult found }, Cmd.none)

        NamespaceFiltersLoaded namespaces ->
            ( { model | filter = model.filter |> (\filter -> { filter | namespaceFilter = RemoteData.fromResult namespaces} ) }
            , Cmd.none
            )


viewItems : List BoundedContext.Model -> List (Html Msg)
viewItems items =
    items
    |> List.map BoundedContext.view
    |> List.map (Html.map BoundedContextMsg)


viewFilter : Filter -> Html Msg
viewFilter { query}  =
    if not <| List.isEmpty query then
        Grid.simpleRow
            [ Grid.col []
                [ Html.h6[] [text "Filter parameters"]
                , Html.ul []
                    (query |> List.map (\q -> Html.li [] [ text <| q.name ++ ": " ++ q.value ]))
                ]
            ]
    else
        Grid.simpleRow []


view : Model -> Html Msg
view model =
    case model.models of
        RemoteData.Success items ->
            Grid.container [ ]
                ( viewFilter model.filter
                :: viewItems items
                )
        e ->
            text <| "Could not load data: " ++ (Debug.toString e)


subscriptions : Model -> Sub Msg
subscriptions model =
    Sub.none


findAll : Api.Configuration -> List QueryParameter -> Cmd Msg
findAll config query =
  Http.get
    { url = Api.allBoundedContexts [] |> Api.urlWithQueryParameters config (query |> List.map (\q -> Url.Builder.string q.name q.value))
    , expect = Http.expectJson BoundedContextsFound (Decode.list BoundedContextCard.decoder)
    }

getNamespaceFilters : Api.Configuration -> Cmd Msg
getNamespaceFilters config =
  Http.get
    { url = Api.withoutQuery [ "search", "namespaces"] |> Api.url config
    , expect = Http.expectJson NamespaceFiltersLoaded namespaceFilterDecoder
    }
