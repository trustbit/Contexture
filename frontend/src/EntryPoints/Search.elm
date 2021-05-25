module EntryPoints.Search exposing (main)

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
import RemoteData
import Task
import Url
import Url.Builder exposing (QueryParameter)
import Url.Parser
import Url.Parser.Query


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
        |> List.filter (\i -> not <| List.isEmpty i.contextItems)


initFilter : List QueryParameter -> Filter
initFilter query =
    { query = query
    , namespaceFilter = RemoteData.Loading
    , selectedFilters = Dict.empty
    , bounce = Bounce.init
    }


init : Decode.Value -> ( Model, Cmd Msg )
init flag =
    case flag |> Decode.decodeValue flagsDecoder of
        Ok decoded ->
            ( { configuration = decoded.apiBase
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
                        if not <| String.isEmpty v then
                            v |> Api.baseConfig |> Decode.succeed

                        else
                            Decode.fail <| "Could not decode url from " ++ v
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


type alias LabelFilterOption =
    { name : String
    , values : List String
    }


type alias LabelFilter =
    { name : String
    , value : String
    , filterOn : NamespaceFilterDescription
    , basedOn : Maybe LabelFilterOption
    }


type alias NamespaceFilterDescription =
    { name : String
    , description : Maybe String
    , templateId : Maybe NamespaceTemplateId
    , labels : List LabelFilterOption
    }


type alias NamespaceFilter =
    { withTemplate : List NamespaceFilterDescription
    , withoutTemplate : List NamespaceFilterDescription
    }


labelFilterDecoder =
    Decode.map2 LabelFilterOption
        (Decode.field "name" Decode.string)
        (Decode.field "values" (Decode.list Decode.string))


namespaceFilterDescriptionDecoder =
    Decode.map4 NamespaceFilterDescription
        (Decode.field "name" Decode.string)
        (Decode.maybe (Decode.field "description" Decode.string))
        (Decode.maybe (Decode.field "templateId" Decode.string))
        (Decode.field "labels" (Decode.list labelFilterDecoder))


namespaceFilterDecoder =
    Decode.map2 NamespaceFilter
        (Decode.field "withTemplate" <| Decode.list namespaceFilterDescriptionDecoder)
        (Decode.field "withoutTemplate" <| Decode.list namespaceFilterDescriptionDecoder)


type alias Filter =
    { namespaceFilter : RemoteData.WebData NamespaceFilter
    , query : List QueryParameter
    , selectedFilters : Dict.Dict String LabelFilter
    , bounce : Bounce
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
    | FilterLabelNameChanged LabelFilter String
    | FilterLabelValueChanged LabelFilter String
    | ApplyFilters
    | BounceMsg


updateModels : Model -> Model
updateModels model =
    { model
        | models =
            model.items
                |> RemoteData.map
                    (\items ->
                        initModel model.configuration model.collaboration items model.domains
                    )
    }


updateFilter : (Filter -> Filter) -> Model -> Model
updateFilter apply model =
    { model | filter = apply model.filter }


asFilterKey : NamespaceFilterDescription -> Maybe String -> String
asFilterKey namespace name =
    case name of
        Just n ->
            namespace.name ++ "---" ++ n

        Nothing ->
            namespace.name


buildQuery selectedFilters =
    selectedFilters
        |> Dict.toList
        |> List.map Tuple.second
        |> List.concatMap
            (\t ->
                [ { name = "Label.Name", value = t.name }, { name = "Label.Value", value = t.value } ]
                    |> List.filter (\f -> not <| String.isEmpty f.value)
            )


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        BoundedContextMsg m ->
            ( model, Cmd.none )

        BoundedContextsFound found ->
            ( updateModels { model | items = RemoteData.fromResult found }, Cmd.none )

        NamespaceFiltersLoaded namespaces ->
            ( model
                |> updateFilter
                    (\f ->
                        { f
                            | namespaceFilter =
                                namespaces
                                    |> RemoteData.fromResult
                        }
                    )
            , Cmd.none
            )

        FilterLabelNameChanged basis text ->
            ( model
                |> updateFilter
                    (\f ->
                        { f
                            | selectedFilters =
                                f.selectedFilters
                                    |> Dict.insert
                                        (asFilterKey basis.filterOn Nothing)
                                        { basis
                                            | name = text
                                            , basedOn =
                                                basis.filterOn.labels
                                                    |> List.filter (\l -> String.toLower l.name == String.toLower text)
                                                    |> List.head
                                        }
                            , bounce = Bounce.push f.bounce
                        }
                    )
            , Bounce.delay 300 BounceMsg
            )

        FilterLabelValueChanged label value ->
            ( model
                |> updateFilter
                    (\f ->
                        { f
                            | selectedFilters =
                                f.selectedFilters
                                    |> Dict.insert
                                        (asFilterKey label.filterOn Nothing)
                                        { label | value = value }
                            , bounce = Bounce.push f.bounce
                        }
                    )
            , Bounce.delay 300 BounceMsg
            )

        ApplyFilters ->
            let
                query =
                    buildQuery model.filter.selectedFilters
            in
            ( model
                |> updateFilter (\f -> { f | query = query })
            , findAll model.configuration query
            )

        BounceMsg ->
            let
                newBounce =
                    Bounce.pop model.filter.bounce
            in
            ( model
                |> updateFilter (\filter -> { filter | bounce = newBounce })
            , if Bounce.steady newBounce then
                Task.perform (\_ -> ApplyFilters) (Task.succeed ())

              else
                Cmd.none
            )


viewItems : List BoundedContext.Model -> List (Html Msg)
viewItems items =
    items
        |> List.map BoundedContext.view
        |> List.map (Html.map BoundedContextMsg)


viewAppliedFilters : List QueryParameter -> Html Msg
viewAppliedFilters query =
    if not <| List.isEmpty query then
        Grid.simpleRow
            [ Grid.col [ Col.xs3 ]
                [ Html.h5 [] [ text "Active filters" ] ]
            , Grid.col []
                [ Html.ul []
                    (query |> List.map (\q -> Html.li [] [ text <| q.name ++ ": " ++ q.value ]))
                ]
            ]

    else
        Grid.simpleRow []


viewFilterDescription : LabelFilter -> Html Msg
viewFilterDescription filter =
    let
        { basedOn, filterOn, name, value } =
            filter
    in
    Form.row []
        [ Form.colLabel
            [ Col.attrs
                (filterOn.description
                    |> Maybe.map title
                    |> Maybe.map List.singleton
                    |> Maybe.withDefault []
                )
            ]
            [ Html.span [] [ text "Search in ", Html.b [] [ text filterOn.name ] ]
            ]
        , Form.col []
            [ Input.text
                [ Input.attrs [ Attributes.list <| "list-" ++ filterOn.name ]
                , Input.onInput (FilterLabelNameChanged filter)
                , Input.value name
                ]
            , Html.datalist
                [ id <| "list-" ++ filterOn.name ]
                (filterOn.labels
                    |> List.map (\l -> Html.option [] [ text l.name ])
                )
            ]
        , Form.col []
            [ Input.text
                [ Input.attrs [ Attributes.list <| "list-" ++ filterOn.name ++ "-values" ]
                , Input.value value
                , Input.onInput (FilterLabelValueChanged filter)
                ]
            , Html.datalist
                [ id <| "list-" ++ filterOn.name ++ "-values" ]
                (case basedOn of
                    Just basedOnLabel ->
                        basedOnLabel.values
                            |> List.map (\l -> Html.option [] [ text l ])

                    Nothing ->
                        []
                )
            ]
        ]


viewNamespaceFilter : Dict.Dict String LabelFilter -> NamespaceFilter -> Html Msg
viewNamespaceFilter activeFilters namespaces =
    Grid.simpleRow
        [ Grid.col [ Col.xs3 ]
            [ Html.h5 [] [ text "Search in Namespaces" ] ]
        , Grid.col []
            (List.append namespaces.withTemplate namespaces.withoutTemplate
                |> List.map
                    (\t ->
                        viewFilterDescription
                            (activeFilters
                                |> Dict.get (asFilterKey t Nothing)
                                |> Maybe.withDefault
                                    { name = ""
                                    , value = ""
                                    , basedOn = Nothing
                                    , filterOn = t
                                    }
                            )
                    )
            )
        ]


viewFilter : Filter -> Html Msg
viewFilter model =
    div []
        [ viewAppliedFilters model.query
        , model.namespaceFilter
            |> RemoteData.map (viewNamespaceFilter model.selectedFilters)
            |> RemoteData.withDefault (text "Loading namespaces")
        , Button.button [ Button.onClick ApplyFilters ] [ text "Apply Filters" ]
        ]


view : Model -> Html Msg
view model =
    case model.models of
        RemoteData.Success items ->
            Grid.container []
                (viewFilter model.filter
                    :: viewItems items
                )

        e ->
            text <| "Could not load data: " ++ Debug.toString e


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
        { url = Api.withoutQuery [ "search", "filter", "namespaces" ] |> Api.url config
        , expect = Http.expectJson NamespaceFiltersLoaded namespaceFilterDecoder
        }
