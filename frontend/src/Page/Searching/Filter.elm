module Page.Searching.Filter exposing (FilterParameter, Model, Msg, OutMsg(..), init, update, view)

import Api as Api
import Bootstrap.Badge as Badge
import Bootstrap.Button as Button
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Grid.Row as Row
import Bootstrap.Text as Text
import Bootstrap.Utilities.Spacing as Spacing
import Bounce exposing (Bounce)
import BoundedContext.Message exposing (Query)
import BoundedContext.Namespace as Namespace exposing (NamespaceTemplateId)
import Dict
import Html exposing (Html, div, text)
import Html.Attributes as Attributes exposing (..)
import Html.Events exposing (onClick)
import Http
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import RemoteData
import Task


applyFiltersCommand =
    Task.perform (\_ -> ApplyFilters) (Task.succeed ())


initSelectedFilters =
    { byNamespace = Dict.empty, unknown = [] }


init : Api.Configuration -> List FilterParameter -> ( Filter, Cmd Msg )
init config parameters =
    ( { initialParameters = parameters
      , namespaceFilter = RemoteData.Loading
      , selectedFilters = initSelectedFilters
      , bounce = Bounce.init
      }
    , Cmd.batch [ getNamespaceFilters config ]
    )


type alias FilterParameter =
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


type alias SelectedFilters =
    { byNamespace : Dict.Dict String LabelFilter
    , unknown : List FilterParameter
    }


type alias Filter =
    { namespaceFilter : RemoteData.WebData NamespaceFilter
    , initialParameters : List FilterParameter
    , selectedFilters : SelectedFilters
    , bounce : Bounce
    }


type Msg
    = NamespaceFiltersLoaded (Api.ApiResponse NamespaceFilter)
    | FilterLabelNameChanged LabelFilter String
    | RemoveFilterLabelName LabelFilter
    | FilterLabelValueChanged LabelFilter String
    | RemoveFilterLabelValue LabelFilter
    | RemoveUnknownFilter FilterParameter
    | RemoveAllFilters
    | ApplyFilters
    | BounceMsg


type OutMsg
    = NoOp
    | FilterApplied (List FilterParameter)


type alias Model =
    Filter


asNamespaceFilterKey : NamespaceFilterDescription -> Maybe String -> String
asNamespaceFilterKey namespace name =
    case name of
        Just n ->
            namespace.name ++ "---" ++ n

        Nothing ->
            namespace.name


filterByLabelName filter =
    if String.isEmpty filter.name then
        Nothing

    else
        Just { name = "Label.Name", value = filter.name }


filterByLabelValue filter =
    if String.isEmpty filter.value then
        Nothing

    else
        Just { name = "Label.Value", value = filter.value }


buildParameters selectedFilters =
    selectedFilters.byNamespace
        |> Dict.toList
        |> List.map Tuple.second
        |> List.concatMap
            (\t ->
                [ filterByLabelName t, filterByLabelValue t ] |> List.filterMap identity
            )
        |> List.append selectedFilters.unknown


removeUnusedNamespaceFilters byNamespace =
    byNamespace
        |> Dict.filter (\_ { name, value } -> not (String.isEmpty name && String.isEmpty value))


findBasedOnValues text labels =
    labels
        |> List.filter (\l -> String.toLower l.name == String.toLower text)
        |> List.head


updateLabelNameFilter basis text model =
    { model
        | byNamespace =
            model.byNamespace
                |> Dict.insert
                    (asNamespaceFilterKey basis.filterOn Nothing)
                    { basis
                        | name = text
                        , basedOn = basis.filterOn.labels |> findBasedOnValues text
                    }
                |> removeUnusedNamespaceFilters
    }


updateLabelValueFilter label value model =
    { model
        | byNamespace =
            model.byNamespace
                |> Dict.insert
                    (asNamespaceFilterKey label.filterOn Nothing)
                    { label | value = value }
                |> removeUnusedNamespaceFilters
    }


updateFilter action model =
    { model
        | selectedFilters =
            action model.selectedFilters
        , bounce = Bounce.push model.bounce
    }


applyExistingFilters : List FilterParameter -> List NamespaceFilterDescription -> SelectedFilters
applyExistingFilters parameters namespaceFilter =
    let
        groupByLabelName ( label, namespace ) grouping =
            Dict.update
                label.name
                (\g ->
                    case g of
                        Just group ->
                            Just (namespace :: group)

                        Nothing ->
                            Just [ namespace ]
                )
                grouping

        namespaceLookup =
            namespaceFilter
                |> List.concatMap (\n -> n.labels |> List.map (\label -> ( label, n )))
                |> List.foldl groupByLabelName Dict.empty
    in
    parameters
        |> List.foldl
            (\parameter filters ->
                case String.toLower parameter.name of
                    "label.name" ->
                        case namespaceLookup |> Dict.get parameter.value |> Maybe.map List.reverse |> Maybe.andThen List.head of
                            Just filterDescription ->
                                { filters
                                    | byNamespace =
                                        filters.byNamespace
                                            |> Dict.insert
                                                filterDescription.name
                                                { name = parameter.value
                                                , value = ""
                                                , filterOn = filterDescription
                                                , basedOn = filterDescription.labels |> findBasedOnValues parameter.value
                                                }
                                }

                            Nothing ->
                                { filters | unknown = parameter :: filters.unknown }

                    "label.value" ->
                        case filters.byNamespace |> Dict.values |> List.filter (\l -> l.filterOn.labels |> List.concatMap (\label -> label.values) |> List.member parameter.value) |> List.head of
                            Just filter ->
                                { filters | byNamespace = filters.byNamespace |> Dict.insert filter.filterOn.name { filter | value = parameter.value } }

                            Nothing ->
                                { filters | unknown = parameter :: filters.unknown }

                    _ ->
                        { filters | unknown = parameter :: filters.unknown }
            )
            initSelectedFilters


update : Msg -> Model -> ( Model, Cmd Msg, OutMsg )
update msg model =
    case msg of
        NamespaceFiltersLoaded namespaces ->
            ( { model
                | namespaceFilter =
                    namespaces
                        |> RemoteData.fromResult
                , selectedFilters =
                    case namespaces of
                        Ok n ->
                            applyExistingFilters model.initialParameters (List.append n.withoutTemplate n.withTemplate)

                        _ ->
                            model.selectedFilters
              }
            , applyFiltersCommand
            , NoOp
            )

        FilterLabelNameChanged basis text ->
            ( model |> updateFilter (updateLabelNameFilter basis text)
            , Bounce.delay 300 BounceMsg
            , NoOp
            )

        RemoveFilterLabelName basis ->
            ( model |> updateFilter (updateLabelNameFilter basis "")
            , applyFiltersCommand
            , NoOp
            )

        FilterLabelValueChanged label value ->
            ( model |> updateFilter (updateLabelValueFilter label value)
            , Bounce.delay 300 BounceMsg
            , NoOp
            )

        RemoveFilterLabelValue label ->
            ( model |> updateFilter (updateLabelValueFilter label "")
            , applyFiltersCommand
            , NoOp
            )

        RemoveUnknownFilter filterToRemove ->
            ( model |> updateFilter (\selectedFilters -> { selectedFilters | unknown = selectedFilters.unknown |> List.filter (\unknownFilter -> unknownFilter /= filterToRemove) })
            , applyFiltersCommand
            , NoOp
            )

        RemoveAllFilters ->
            ( { model | selectedFilters = initSelectedFilters }
            , applyFiltersCommand
            , NoOp
            )

        ApplyFilters ->
            let
                query =
                    buildParameters model.selectedFilters
            in
            ( { model
                | initialParameters = query
                , bounce = Bounce.init
              }
            , Cmd.none
            , FilterApplied query
            )

        BounceMsg ->
            let
                newBounce =
                    Bounce.pop model.bounce
            in
            ( { model | bounce = newBounce }
            , if Bounce.steady newBounce then
                applyFiltersCommand

              else
                Cmd.none
            , NoOp
            )


viewAppliedNamespaceFilters : List LabelFilter -> List (Html Msg)
viewAppliedNamespaceFilters query =
    query
        |> List.concatMap
            (\q ->
                [ q
                    |> filterByLabelName
                    |> Maybe.map (Tuple.pair (RemoveFilterLabelName q))
                , q
                    |> filterByLabelValue
                    |> Maybe.map (Tuple.pair (RemoveFilterLabelValue q))
                ]
                    |> List.filterMap
                        (Maybe.map
                            (\( removeAction, filter ) ->
                                Html.a [ class "badge badge-secondary", Spacing.ml1, Attributes.href "#", title "Remove filter", onClick removeAction ] [ text <| filter.name ++ ": " ++ filter.value ]
                            )
                        )
            )


viewAppliedUnkownFilters : List FilterParameter -> List (Html Msg)
viewAppliedUnkownFilters query =
    query
        |> List.map (\filter -> Html.a [ class "badge badge-warning", Spacing.ml1, Attributes.href "#", title "Remove unkown filter", onClick (RemoveUnknownFilter filter) ] [ text <| filter.name ++ ": " ++ filter.value ])


viewAppliedFilters : SelectedFilters -> Html Msg
viewAppliedFilters { byNamespace, unknown } =
    let
        activeFilters =
            List.concat
                [ viewAppliedNamespaceFilters (byNamespace |> Dict.values)
                , viewAppliedUnkownFilters unknown
                ]
    in
    Grid.simpleRow
        [ Grid.col [ Col.xs3 ]
            [ Html.h5 [] [ text "Active filters" ] ]
        , Grid.col []
            (if not <| List.isEmpty activeFilters then
                [ Grid.simpleRow
                    [ Grid.col []
                        activeFilters
                    , Grid.col [ Col.mdAuto ]
                        [ Button.button [ Button.secondary, Button.onClick RemoveAllFilters, Button.small, Button.roleLink ] [ text "Remove all Filters" ] ]
                    ]
                ]

             else
                [ text "None" ]
            )
        ]


viewFilterInput : String -> List String -> (String -> Msg) -> Msg -> String -> List (Html Msg)
viewFilterInput name options inputAction removeAction value =
    let
        arguments =
            [ Input.attrs [ Attributes.list <| "list-" ++ name ]
            , Input.onInput inputAction
            , Input.value value
            ]
    in
    [ if String.isEmpty value then
        Input.text arguments

      else
        InputGroup.config
            (InputGroup.text arguments)
            |> InputGroup.successors
                [ InputGroup.button [ Button.outlineSecondary, Button.onClick removeAction ] [ text "x" ]
                ]
            |> InputGroup.view
    , Html.datalist
        [ id <| "list-" ++ name ]
        (options
            |> List.map (\l -> Html.option [] [ text l ])
        )
    ]


viewFilterDescription : LabelFilter -> Html Msg
viewFilterDescription filter =
    let
        { basedOn, filterOn } =
            filter
    in
    Form.row []
        [ Form.colLabel
            [ Col.attrs
                [ title (filterOn.description |> Maybe.withDefault ("Filter in " ++ filterOn.name)) ]
            ]
            [ Html.span [] [ text "Search in ", Html.b [] [ text filterOn.name ] ]
            ]
        , Form.col []
            (viewFilterInput
                filterOn.name
                (filterOn.labels |> List.map .name)
                (FilterLabelNameChanged filter)
                (RemoveFilterLabelName filter)
                filter.name
            )
        , Form.col []
            (viewFilterInput
                (filterOn.name ++ "-values")
                (basedOn
                    |> Maybe.map .values
                    |> Maybe.withDefault []
                )
                (FilterLabelValueChanged filter)
                (RemoveFilterLabelValue filter)
                filter.value
            )
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
                                |> Dict.get (asNamespaceFilterKey t Nothing)
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


view : Filter -> Html Msg
view model =
    div []
        [ viewAppliedFilters model.selectedFilters
        , model.namespaceFilter
            |> RemoteData.map (viewNamespaceFilter model.selectedFilters.byNamespace)
            |> RemoteData.withDefault (text "Loading namespaces")
        ]


getNamespaceFilters : Api.Configuration -> Cmd Msg
getNamespaceFilters config =
    Http.get
        { url = Api.withoutQuery [ "search", "filter", "namespaces" ] |> Api.url config
        , expect = Http.expectJson NamespaceFiltersLoaded namespaceFilterDecoder
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
