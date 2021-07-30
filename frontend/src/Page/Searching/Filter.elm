module Page.Searching.Filter exposing (FilterParameter, Model, Msg, OutMsg(..), init, update, view)

import Api as Api
import Bootstrap.Badge as Badge
import Bootstrap.Button as Button
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Grid.Row as Row
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Text as Text
import Bootstrap.Utilities.Spacing as Spacing
import Bounce exposing (Bounce)
import BoundedContext.Namespace as Namespace exposing (NamespaceTemplateId)
import Dict
import Html exposing (Html, div, text)
import Html.Attributes as Attributes exposing (..)
import Html.Events exposing (onClick)
import Http
import Json.Decode as Decode
import RemoteData
import Task


applyFiltersCommand =
    Task.perform (\_ -> ApplyFilters) (Task.succeed ())

type alias FilterParameter =
    { name : String
    , value : String
    }


type alias LabelFilterOption =
    { labelName : String
    , values : List String
    }


type alias LabelFilter =
    { id: String
    , labelName : String
    , labelValue : String
    , filterInNamespace : NamespaceFilterDescription
    , basedOnLabel : Maybe LabelFilterOption
    }


type alias NamespaceFilterDescription =
    { namespaceName : String
    , description : Maybe String
    , templateId : Maybe NamespaceTemplateId
    , labels : List LabelFilterOption
    }


type alias ActiveFilters =
    { byNamespace : Dict.Dict String LabelFilter
    , unknown : List FilterParameter
    }


type alias Filter =
    { namespaceFilter : RemoteData.WebData (List NamespaceFilterDescription)
    , currentParameters : List FilterParameter
    , activeFilters : ActiveFilters
    , bounce : Bounce
    }
    

initActiveFilters =
    { byNamespace = Dict.empty, unknown = [] }


init : Api.Configuration -> List FilterParameter -> ( Filter, Cmd Msg )
init config parameters =
    ( { currentParameters = parameters
      , namespaceFilter = RemoteData.Loading
      , activeFilters = initActiveFilters
      , bounce = Bounce.init
      }
    , Cmd.batch [ getNamespaceFilterDescriptions config ]
    )

initLabelFilter namespace existingFilters =
    { id = "filter_" ++ (existingFilters |> Dict.size |> String.fromInt)
    , labelName = ""
    , labelValue = ""
    , basedOnLabel = Nothing
    , filterInNamespace = namespace
    }


type Msg
    = NamespaceFilterDescriptionsLoaded (Api.ApiResponse (List NamespaceFilterDescription))
    | AddNewFilterLabel NamespaceFilterDescription
    | RemoveFilterLabel LabelFilter
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


asNamespaceFilterKey : LabelFilter -> String
asNamespaceFilterKey { filterInNamespace , id } =
    filterInNamespace.namespaceName ++ "---" ++ id


filterByLabelName : LabelFilter -> Maybe FilterParameter
filterByLabelName filter =
    if String.isEmpty filter.labelName then
        Nothing

    else
        Just { name = "Label.Name", value = filter.labelName }


filterByLabelValue : LabelFilter -> Maybe FilterParameter
filterByLabelValue filter =
    if String.isEmpty filter.labelValue then
        Nothing

    else
        Just { name = "Label.Value", value = filter.labelValue }


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
        |> Dict.filter (\_ { labelName, labelValue } -> not (String.isEmpty labelName && String.isEmpty labelValue))


findBasedOnValues text labels =
    labels
        |> List.filter (\l -> String.toLower l.labelName == String.toLower text)
        |> List.head


updateLabelNameFilter basis text model =
    { model
        | byNamespace =
            model.byNamespace
                |> Dict.insert
                    (asNamespaceFilterKey basis)
                    { basis
                        | labelName = text
                        , basedOnLabel = basis.filterInNamespace.labels |> findBasedOnValues text
                    }
    }


updateLabelValueFilter label value model =
    { model
        | byNamespace =
            model.byNamespace
                |> Dict.insert
                    (asNamespaceFilterKey label)
                    { label | labelValue = value }
    }


updateFilter action model =
    { model
        | activeFilters =
            action model.activeFilters
        , bounce = Bounce.push model.bounce
    }


applyExistingFilters : List FilterParameter -> List NamespaceFilterDescription -> ActiveFilters
applyExistingFilters parameters namespaceFilter =
    let
        groupByLabelName ( label, namespace ) grouping =
            Dict.update
                label.labelName
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
                                                filterDescription.namespaceName
                                                ( initLabelFilter filterDescription filters.byNamespace
                                                    |> \newLabel ->
                                                        { newLabel 
                                                        | labelName = parameter.value
                                                        , basedOnLabel = filterDescription.labels |> findBasedOnValues parameter.value
                                                        }
                                                )
                                }

                            Nothing ->
                                { filters | unknown = parameter :: filters.unknown }

                    "label.value" ->
                        case filters.byNamespace |> Dict.values |> List.filter (\l -> l.filterInNamespace.labels |> List.concatMap (\label -> label.values) |> List.member parameter.value) |> List.head of
                            Just filter ->
                                { filters | byNamespace = filters.byNamespace |> Dict.insert filter.filterInNamespace.namespaceName { filter | labelValue = parameter.value } }

                            Nothing ->
                                { filters | unknown = parameter :: filters.unknown }

                    _ ->
                        { filters | unknown = parameter :: filters.unknown }
            )
            initActiveFilters


update : Msg -> Model -> ( Model, Cmd Msg, OutMsg )
update msg model =
    case msg of
        NamespaceFilterDescriptionsLoaded namespaces ->
            ( { model
                | namespaceFilter =
                    namespaces
                        |> RemoteData.fromResult
                , activeFilters =
                    namespaces
                        |> Result.map (applyExistingFilters model.currentParameters)
                        |> Result.withDefault model.activeFilters
              }
            , applyFiltersCommand
            , NoOp
            )
            
        AddNewFilterLabel namespace ->
            ( model |> updateFilter (updateLabelNameFilter (initLabelFilter namespace model.activeFilters.byNamespace) "")
            , Bounce.delay 300 BounceMsg
            , NoOp
            )
        RemoveFilterLabel filter ->
            ( model |> updateFilter (\m -> { m | byNamespace = m.byNamespace |> Dict.remove (asNamespaceFilterKey filter) } )
            , Bounce.delay 300 BounceMsg
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
            ( { model | activeFilters = initActiveFilters }
            , applyFiltersCommand
            , NoOp
            )

        ApplyFilters ->
            let
                parameters =
                    buildParameters model.activeFilters
            in
            ( { model
                | currentParameters = parameters
                , bounce = Bounce.init
              }
            , Cmd.none
            , FilterApplied parameters
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
                            Html.a 
                                [ class "badge badge-secondary"
                                , Spacing.ml1
                                , Attributes.href "#"
                                , title "Remove filter"
                                , onClick removeAction 
                                ] 
                                [ text <| filter.name ++ ": " ++ filter.value ]
                        )
                    )
            )


viewAppliedUnknownFilters : List FilterParameter -> List (Html Msg)
viewAppliedUnknownFilters query =
    query
        |> List.map (\filter -> 
            Html.a 
                [ class "badge badge-warning"
                , Spacing.ml1
                , Attributes.href "#"
                , title "Remove unknown filter"
                , onClick (RemoveUnknownFilter filter) 
                ] 
                [ text <| filter.name ++ ": " ++ filter.value ]
            )


viewAppliedFilters : ActiveFilters -> List (Block.Item Msg)
viewAppliedFilters { byNamespace, unknown } =
    let
        activeFilters =
            List.concat
                [ viewAppliedNamespaceFilters (byNamespace |> Dict.values)
                , viewAppliedUnknownFilters unknown
                ]
    in
    if not <| List.isEmpty activeFilters then
        [ Block.titleH5 []
            [ Grid.simpleRow
                [ Grid.col []
                    [ text "Active filters" ]
                , Grid.col [ Col.mdAuto ]
                    [ Button.button [ Button.secondary, Button.onClick RemoveAllFilters, Button.small, Button.roleLink ] [ text "Remove all Filters" ] ]
                ]
            ]
        , Block.text [] activeFilters
        ]

    else
        [ Block.titleH5 [] [ text "Active filters" ]
        , Block.text [] [ text "None" ]
        ]


viewFilterInput : String -> List String -> (String -> Msg) -> Msg -> String -> List (Html Msg)
viewFilterInput name options inputAction removeAction value =
    [ InputGroup.config
        (InputGroup.text
            [ Input.attrs [ Attributes.list <| "list-" ++ name ]
            , Input.onInput inputAction
            , Input.value value
            ]
        )
        |> (\inputConfig ->
                if String.isEmpty value then
                    inputConfig

                else
                    inputConfig
                        |> InputGroup.successors
                            [ InputGroup.button [ Button.outlineSecondary, Button.onClick removeAction ] [ text "x" ] ]
           )
        |> InputGroup.view
    , Html.datalist
        [ id <| "list-" ++ name ]
        (options
            |> List.map (\l -> Html.option [] [ text l ])
        )
    ]


viewFilter : LabelFilter -> Html Msg 
viewFilter filter =
    Form.row []
        [ Form.col []
            (viewFilterInput
                filter.id
                (filter.filterInNamespace.labels |> List.map .labelName)
                (FilterLabelNameChanged filter)
                (RemoveFilterLabelName filter)
                filter.labelName
            )
        , Form.col []
            (viewFilterInput
                (filter.id ++ "-values")
                (filter.basedOnLabel
                    |> Maybe.map .values
                    |> Maybe.withDefault []
                )
                (FilterLabelValueChanged filter)
                (RemoveFilterLabelValue filter)
                filter.labelValue
            )
        , Form.col [ Col.smAuto, Col.attrs [Spacing.p0]]
            [ Button.button [ Button.roleLink, Button.onClick (RemoveFilterLabel filter)] [ text "x"]
            ]
        ]

viewFilterDescription : NamespaceFilterDescription -> List LabelFilter -> List (Block.Item Msg)
viewFilterDescription namespace filters =
    [ Block.text
        [ title (namespace.description |> Maybe.withDefault ("Filter in " ++ namespace.namespaceName)) ]
        [ Button.button 
            [ Button.outlinePrimary
            , Button.onClick (AddNewFilterLabel namespace) ] 
            [ text "+ Search in ", Html.b [] [ text namespace.namespaceName ] ] 
        ]
    , Block.custom <|
        Html.div []
           (filters |> List.map viewFilter)
    ]


viewNamespaceFilter : Dict.Dict String LabelFilter -> List NamespaceFilterDescription -> List (Block.Item Msg)
viewNamespaceFilter activeFilters namespaces =
    Block.titleH5 [] [ text "Search in Namespaces" ]
        :: (namespaces
                |> List.sortBy (\n -> n.namespaceName)
                |> List.concatMap
                    (\t ->
                        viewFilterDescription
                            t
                            (activeFilters
                                |> Dict.filter (\key _ -> key |> String.startsWith t.namespaceName)
                                |> Dict.toList
                                |> List.map Tuple.second
                            )
                    )
           )


view : Filter -> Html Msg
view model =
    Card.config []
        |> Card.block [] (viewAppliedFilters model.activeFilters)
        |> Card.block []
            (model.namespaceFilter
                |> RemoteData.map (viewNamespaceFilter model.activeFilters.byNamespace)
                |> RemoteData.withDefault [ Block.text [] [ text "Loading namespaces" ] ]
            )
        |> Card.view


getNamespaceFilterDescriptions : Api.Configuration -> Cmd Msg
getNamespaceFilterDescriptions config =
    Http.get
        { url = Api.withoutQuery [ "search", "filter", "namespaces" ] |> Api.url config
        , expect = Http.expectJson NamespaceFilterDescriptionsLoaded (Decode.list namespaceFilterDescriptionDecoder)
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
