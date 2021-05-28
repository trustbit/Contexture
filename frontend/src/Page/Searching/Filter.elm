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
import BoundedContext as BoundedContext
import BoundedContext.BoundedContextId as BoundedContextId
import BoundedContext.Canvas
import BoundedContext.Message exposing (Query)
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
import Html.Events exposing (onClick)
import Http
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import RemoteData
import Task


applyFiltersCommand =
    Task.perform (\_ -> ApplyFilters) (Task.succeed ())


init : Api.Configuration -> List FilterParameter -> ( Filter, Cmd Msg )
init config parameters =
    ( { parameters = parameters
      , namespaceFilter = RemoteData.Loading
      , selectedFilters = Dict.empty
      , bounce = Bounce.init
      }
    , Cmd.batch [ getNamespaceFilters config, applyFiltersCommand ]
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


type alias Filter =
    { namespaceFilter : RemoteData.WebData NamespaceFilter
    , parameters : List FilterParameter
    , selectedFilters : Dict.Dict String LabelFilter
    , bounce : Bounce
    }


type Msg
    = NamespaceFiltersLoaded (Api.ApiResponse NamespaceFilter)
    | FilterLabelNameChanged LabelFilter String
    | RemoveFilterLabelName LabelFilter
    | FilterLabelValueChanged LabelFilter String
    | RemoveFilterLabelValue LabelFilter
    | RemoveAllFilters
    | ApplyFilters
    | BounceMsg


type OutMsg
    = NoOp
    | FilterApplied (List FilterParameter)


type alias Model =
    Filter


asFilterKey : NamespaceFilterDescription -> Maybe String -> String
asFilterKey namespace name =
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
    selectedFilters
        |> Dict.toList
        |> List.map Tuple.second
        |> List.concatMap
            (\t ->
                [ filterByLabelName t, filterByLabelValue t ] |> List.filterMap identity
            )


updateLabelNameFilter basis text model =
    { model
        | selectedFilters =
            model.selectedFilters
                |> Dict.insert
                    (asFilterKey basis.filterOn Nothing)
                    { basis
                        | name = text
                        , basedOn =
                            basis.filterOn.labels
                                |> List.filter (\l -> String.toLower l.name == String.toLower text)
                                |> List.head
                    }
        , bounce = Bounce.push model.bounce
    }


updateLabelValueFilter label value model =
    { model
        | selectedFilters =
            model.selectedFilters
                |> Dict.insert
                    (asFilterKey label.filterOn Nothing)
                    { label | value = value }
        , bounce = Bounce.push model.bounce
    }


update : Msg -> Model -> ( Model, Cmd Msg, OutMsg )
update msg model =
    case msg of
        NamespaceFiltersLoaded namespaces ->
            ( { model
                | namespaceFilter =
                    namespaces
                        |> RemoteData.fromResult
              }
            , Cmd.none
            , NoOp
            )

        FilterLabelNameChanged basis text ->
            ( model |> updateLabelNameFilter basis text
            , Bounce.delay 300 BounceMsg
            , NoOp
            )

        RemoveFilterLabelName basis ->
            ( model |> updateLabelNameFilter basis ""
            , applyFiltersCommand
            , NoOp
            )

        FilterLabelValueChanged label value ->
            ( model |> updateLabelValueFilter label value
            , Bounce.delay 300 BounceMsg
            , NoOp
            )

        RemoveFilterLabelValue label ->
            ( model |> updateLabelValueFilter label ""
            , applyFiltersCommand
            , NoOp
            )

        RemoveAllFilters ->
            ( { model | selectedFilters = Dict.empty }
            , applyFiltersCommand
            , NoOp
            )
            

        ApplyFilters ->
            let
                query =
                    buildParameters model.selectedFilters
            in
            ( { model
                | parameters = query
                , bounce = Bounce.init
                , selectedFilters = model.selectedFilters |> Dict.filter (\_ { name, value } -> not (String.isEmpty name && String.isEmpty value))
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


viewAppliedFilters : List LabelFilter -> Html Msg
viewAppliedFilters query =
    Grid.simpleRow
        [ Grid.col [ Col.xs3 ]
            [ Html.h5 [] [ text "Active filters" ] ]
        , Grid.col []
            (if not <| List.isEmpty query then
                [ Grid.simpleRow
                    [ Grid.col [ ]
                        (query
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
                        )
                    , Grid.col [ Col.mdAuto]
                        [ Button.button [ Button.secondary, Button.onClick RemoveAllFilters, Button.small, Button.roleLink] [ text "Remove all Filters"]]
                    ]
                ]
             else
                [ text "None" ]
            )
        ]


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
            [ let
                labelNameArguments =
                    [ Input.attrs [ Attributes.list <| "list-" ++ filterOn.name ]
                    , Input.onInput (FilterLabelNameChanged filter)
                    , Input.value name
                    ]
              in
              if String.isEmpty name then
                Input.text labelNameArguments

              else
                InputGroup.config
                    (InputGroup.text labelNameArguments)
                    |> InputGroup.successors
                        [ InputGroup.button [ Button.outlineSecondary, Button.onClick (RemoveFilterLabelName filter) ] [ text "x" ]
                        ]
                    |> InputGroup.view
            , Html.datalist
                [ id <| "list-" ++ filterOn.name ]
                (filterOn.labels
                    |> List.map (\l -> Html.option [] [ text l.name ])
                )
            ]
        , Form.col []
            [ let
                labelValueArguments =
                    [ Input.attrs [ Attributes.list <| "list-" ++ filterOn.name ++ "-values" ]
                    , Input.value value
                    , Input.onInput (FilterLabelValueChanged filter)
                    ]
              in
              if String.isEmpty value then
                Input.text labelValueArguments

              else
                InputGroup.config
                    (InputGroup.text labelValueArguments)
                    |> InputGroup.successors
                        [ InputGroup.button [ Button.outlineSecondary, Button.onClick (RemoveFilterLabelValue filter) ] [ text "x" ]
                        ]
                    |> InputGroup.view
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


view : Filter -> Html Msg
view model =
    div []
        [ viewAppliedFilters (model.selectedFilters |> Dict.values)
        , model.namespaceFilter
            |> RemoteData.map (viewNamespaceFilter model.selectedFilters)
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
