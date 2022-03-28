module Page.Domain.IndexRoot exposing (Model, Msg, initWithSubdomains, initWithoutSubdomains, subscriptions, update, view)

import Api
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import Browser.Navigation as Nav
import Domain
import Domain.DomainId exposing (DomainId)
import Html exposing (Html, button, div, text)
import Html.Attributes as Attr exposing (..)
import Html.Events exposing (onClick)
import Json.Decode as Decode exposing (Error)
import Page.Bubble.Bubble as Bubble
import Page.Domain.Index as Index


type Visualization
    = Bubble Bubble.Model
    | Grid Index.Model


type VisualizationOption
    = GridOrBubble Visualization Domain.DomainRelation
    | GridOnly Index.Model


type alias Model =
    { navKey : Nav.Key
    , configuration : Api.Configuration
    , options : VisualizationOption
    }


initWithSubdomains : Api.Configuration -> Nav.Key -> DomainId -> ( Model, Cmd Msg )
initWithSubdomains baseUrl key parentDomain =
    init baseUrl key (Domain.Subdomain parentDomain)


initWithoutSubdomains : Api.Configuration -> Nav.Key -> ( Model, Cmd Msg )
initWithoutSubdomains baseUrl key =
    init baseUrl key Domain.Root


init : Api.Configuration -> Nav.Key -> Domain.DomainRelation -> ( Model, Cmd Msg )
init config key domainPosition =
    let
        ( indexModel, indexCmd ) =
            Index.init config key domainPosition

        position =
            case domainPosition of
                Domain.Root ->
                    GridOrBubble (Grid indexModel) domainPosition

                Domain.Subdomain _ ->
                    GridOnly indexModel
    in
    ( { navKey = key
      , configuration = config
      , options = position
      }
    , indexCmd |> Cmd.map GridDomainMsg
    )


type Msg
    = ChangeToBubble
    | ChangeToGrid
    | GridDomainMsg Index.Msg
    | BubbleMsg Bubble.Msg
    | GridOnlyDomainMsg Index.Msg


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case model.options of
        GridOrBubble visualisation domainPosition ->
            case ( visualisation, msg ) of
                ( Grid gridModel, GridDomainMsg domainMsg ) ->
                    Index.update domainMsg gridModel
                        |> Tuple.mapFirst (\m -> { model | options = GridOrBubble (Grid m) domainPosition })
                        |> Tuple.mapSecond (Cmd.map GridDomainMsg)

                ( _, ChangeToGrid ) ->
                    Index.init model.configuration model.navKey domainPosition
                        |> Tuple.mapFirst (\m -> { model | options = GridOrBubble (Grid m) domainPosition })
                        |> Tuple.mapSecond (Cmd.map GridDomainMsg)

                ( _, ChangeToBubble ) ->
                    Bubble.init model.navKey model.configuration
                        |> Tuple.mapFirst (\m -> { model | options = GridOrBubble (Bubble m) domainPosition })
                        |> Tuple.mapSecond (Cmd.map BubbleMsg)

                ( Bubble bubbleModel, BubbleMsg infoMsg ) ->
                    Bubble.update infoMsg bubbleModel
                        |> Tuple.mapFirst (\m -> { model | options = GridOrBubble (Bubble m) domainPosition })
                        |> Tuple.mapSecond (Cmd.map BubbleMsg)

                other ->
                    Debug.todo ("This should never happen " ++ Debug.toString other)

        GridOnly gridModel ->
            case msg of
                GridOnlyDomainMsg domainMsg ->
                    Index.update domainMsg gridModel
                        |> Tuple.mapFirst (\m -> { model | options = GridOnly m })
                        |> Tuple.mapSecond (Cmd.map GridOnlyDomainMsg)

                other ->
                    Debug.todo ("This should never happen either " ++ Debug.toString other)


isGrid visualization =
    case visualization of
        Grid _ ->
            True

        _ ->
            False


viewSwitch current =
    ButtonGroup.radioButtonGroup []
        [ ButtonGroup.radioButton (isGrid current)
            [ Button.primary
            , Button.onClick <| ChangeToGrid
            ]
            [ Html.text "Grid" ]
        , ButtonGroup.radioButton (not (isGrid current))
            [ Button.primary
            , Button.onClick <| ChangeToBubble
            ]
            [ Html.text "Bubble" ]
        ]


view : Model -> Html Msg
view model =
    case model.options of
        GridOnly m ->
            m
                |> Index.view
                |> Html.map GridDomainMsg

        GridOrBubble visualisation _ ->
            Grid.containerFluid []
                (case visualisation of
                    Grid m ->
                        [ Grid.row []
                            [ Grid.col [ Col.xs1 ]
                                [ viewSwitch visualisation ]
                            , Grid.col []
                                [ m
                                    |> Index.view
                                    |> Html.map GridDomainMsg

                                ]
                            ]
                        ]
                    Bubble m ->
                        [ Grid.row []
                            [ Grid.col [ Col.xs1 ]
                                [ viewSwitch visualisation
                                ]
                            ]
                        , m
                            |> Bubble.view
                            |> Html.map BubbleMsg
                        ]
                )
                


subscriptions : Model -> Sub Msg
subscriptions model =
    case model.options of
        GridOrBubble (Bubble m) _ ->
            m
                |> Bubble.subscriptions
                |> Sub.map BubbleMsg

        _ ->
            Sub.none
