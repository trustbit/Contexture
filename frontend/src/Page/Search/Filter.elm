module Page.Search.Filter exposing (Model,FilterParameter,init,update,Msg,OutMsg(..),view)

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
import Http
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import RemoteData
import Task

init : Api.Configuration -> List FilterParameter -> ( Filter, Cmd Msg )
init config parameters =
    ( { parameters = parameters
      , namespaceFilter = RemoteData.Loading
      , selectedFilters = Dict.empty
      , bounce = Bounce.init
      }
    , Cmd.batch [ getNamespaceFilters config, Task.perform (\_ -> ApplyFilters) (Task.succeed ()) ]
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
    | FilterLabelValueChanged LabelFilter String
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


buildParameters selectedFilters =
    selectedFilters
        |> Dict.toList
        |> List.map Tuple.second
        |> List.concatMap
            (\t ->
                [ { name = "Label.Name", value = t.name }, { name = "Label.Value", value = t.value } ]
                    |> List.filter (\f -> not <| String.isEmpty f.value)
            )


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
            ( { model
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
            , Bounce.delay 300 BounceMsg
            , NoOp
            )

        FilterLabelValueChanged label value ->
            ( { model
                | selectedFilters =
                    model.selectedFilters
                        |> Dict.insert
                            (asFilterKey label.filterOn Nothing)
                            { label | value = value }
                , bounce = Bounce.push model.bounce
              }
            , Bounce.delay 300 BounceMsg
            , NoOp
            )

        ApplyFilters ->
            let
                query =
                    buildParameters model.selectedFilters
            in
            ( { model | parameters = query }
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
                Task.perform (\_ -> ApplyFilters) (Task.succeed ())

              else
                Cmd.none
            , NoOp
            )


viewAppliedFilters : List FilterParameter -> Html Msg
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


view : Filter -> Html Msg
view model =
    div []
        [ viewAppliedFilters model.parameters
        , model.namespaceFilter
            |> RemoteData.map (viewNamespaceFilter model.selectedFilters)
            |> RemoteData.withDefault (text "Loading namespaces")
        , Button.button [ Button.onClick ApplyFilters ] [ text "Apply Filters" ]
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
