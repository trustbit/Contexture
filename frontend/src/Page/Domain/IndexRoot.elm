module Page.Domain.IndexRoot exposing (Model, Msg, initWithSubdomains, initWithoutSubdomains, subscriptions, update, view)

import Api exposing (ApiResponse, ApiResult)
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Modal as Modal
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import Browser.Navigation as Nav
import Components.BoundedContextCard as BoundedContextCard
import ContextMapping.Collaboration as Collaboration
import ContextMapping.Communication as Communication
import Domain
import Domain.DomainId exposing (DomainId)
import Html exposing (Html, button, div, text)
import Html.Attributes as Attr exposing (..)
import Html.Events exposing (onClick)
import Http
import Json.Decode as Decode exposing (Error)
import Page.Domain.Bubble as Bubble
import Page.Domain.Edit as Edit
import Page.Domain.Index as Index
import Page.Domain.Ports as Ports
import RemoteData
import Route
import Url


type Visualization
    = Bubble Bubble.Model
    | Grid Index.Model


type VisualizationPosition
    = GridOrBubble Domain.DomainRelation
    | GridOnly Index.Model


type alias Model =
    { navKey : Nav.Key
    , configuration : Api.Configuration
    , domainPosition : VisualizationPosition
    , visualization : Visualization
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
                    GridOrBubble domainPosition

                Domain.Subdomain _ ->
                    GridOnly indexModel
    in
    ( { navKey = key
      , configuration = config
      , domainPosition = position
      , visualization = Grid indexModel
      }
    , indexCmd |> Cmd.map DomainMsg
    )


type Msg
    = ChangeToBubble
    | ChangeToGrid
    | DomainMsg Index.Msg
    | BubbleMsg Bubble.Msg


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case ( model.visualization, msg ) of
        ( Grid gridModel, DomainMsg domainMsg ) ->
            Index.update domainMsg gridModel
                |> Tuple.mapFirst (\m -> { model | visualization = Grid m })
                |> Tuple.mapSecond (Cmd.map DomainMsg)

        ( _, ChangeToGrid ) ->
            case model.domainPosition of
                GridOrBubble position ->
                    Index.init model.configuration model.navKey position
                        |> Tuple.mapFirst (\m -> { model | visualization = Grid m })
                        |> Tuple.mapSecond (Cmd.map DomainMsg)

                _ ->
                    ( model, Cmd.none )

        ( _, ChangeToBubble ) ->
            Bubble.init model.navKey model.configuration
                |> Tuple.mapFirst (\m -> { model | visualization = Bubble m })
                |> Tuple.mapSecond (Cmd.map BubbleMsg)

        ( Bubble bubbleModel, BubbleMsg infoMsg ) ->
            Bubble.update infoMsg bubbleModel
                |> Tuple.mapFirst (\m -> { model | visualization = Bubble m })
                |> Tuple.mapSecond (Cmd.map BubbleMsg)

        other ->
            Debug.todo ("This should never happen " ++ Debug.toString other)


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
    case model.domainPosition of
        GridOnly m ->
            m
                |> Index.view
                |> Html.map DomainMsg

        GridOrBubble _ ->
            Grid.containerFluid []
                [ Grid.row []
                    [ Grid.col [ Col.xs1 ]
                        [ viewSwitch model.visualization ]
                    , Grid.col []
                        [ case model.visualization of
                            Grid m ->
                                m
                                    |> Index.view
                                    |> Html.map DomainMsg

                            Bubble m ->
                                m
                                    |> Bubble.view
                                    |> Html.map BubbleMsg
                        ]
                    ]
                ]


subscriptions : Model -> Sub Msg
subscriptions model =
    case model.visualization of
        Bubble m ->
            m
                |> Bubble.subscriptions
                |> Sub.map BubbleMsg

        Grid _ ->
            Sub.none
