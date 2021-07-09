module Components.BoundedContextsOfDomain exposing (..)

import Api exposing (ApiResponse, ApiResult)
import Bootstrap.Badge as Badge
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Form as Form
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Form.Input as Input
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Grid.Row as Row
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Modal as Modal
import Bootstrap.Text as Text
import Bootstrap.Utilities.Border as Border
import Bootstrap.Utilities.Spacing as Spacing
import BoundedContext as BoundedContext exposing (BoundedContext)
import BoundedContext.Canvas exposing (BoundedContextCanvas)
import BoundedContext.Namespace as Namespace exposing (Namespace)
import BoundedContext.StrategicClassification as StrategicClassification
import Components.BoundedContextCard as BoundedContextCard
import ContextMapping.Collaboration as Collaboration
import ContextMapping.Collaborator as Collaborator
import ContextMapping.Communication as Communication
import Dict as Dict exposing (Dict)
import Domain exposing (Domain)
import Domain.DomainId exposing (DomainId)
import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)
import Http
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import Key
import List
import List.Split exposing (chunksOfLeft)
import RemoteData
import Route
import Select as Autocomplete
import Set
import Url


type alias Model =
    { config : Api.Configuration
    , domain : Domain
    , contextItems : List BoundedContextCard.Model
    , presentation : Presentation
    }


type Presentation
    = Full
    | Condensed


init : Api.Configuration -> Presentation -> Domain -> List BoundedContextCard.Item -> Collaboration.Collaborations -> Model
init config presentation domain items collaborations =
    let
        communication =
            Communication.asCommunication collaborations

        communicationFor { context } =
            communication
                |> Communication.communicationFor
                    (context
                        |> BoundedContext.id
                        |> Collaborator.BoundedContext
                    )
    in
    { contextItems =
        items
            |> List.map
                (\i ->
                    BoundedContextCard.init (communicationFor i) i
                )
    , config = config
    , domain = domain
    , presentation = presentation
    }


type Msg
    = NoOp


condendsedWithActions : BoundedContextCard.Model -> Html Never
condendsedWithActions model =
    let
        context =
            model.contextItem.context
    in
    Grid.row [ Row.attrs [ Spacing.mb2, class "bounded-context" ] ]
        [ Grid.col [ Col.md3 ]
            [ Html.h5 [ class "card-title", Spacing.mb1 ]
                [ text (context |> BoundedContext.name) ]
            , Html.small [ class "text-muted" ]
                [ text (context |> BoundedContext.key |> Maybe.map Key.toString |> Maybe.withDefault "") ]
            ]
        , Grid.col [ Col.md7 ]
            [ ListGroup.ul (BoundedContextCard.namespaceItems model.contextItem.namespaces) ]
        , Grid.col [ Col.md2 ]
            [ ButtonGroup.linkButtonGroup [ ButtonGroup.vertical, ButtonGroup.attrs [ class "text-left" ] ]
                [ ButtonGroup.linkButton
                    [ Button.roleLink
                    , Button.attrs
                        [ href
                            (model.contextItem.context
                                |> BoundedContext.id
                                |> Route.BoundedContextCanvas
                                |> Route.routeToString
                            )
                        , class "text-nowrap"
                        ]
                    ]
                    [ text "Canvas" ]
                , ButtonGroup.linkButton
                    [ Button.roleLink
                    , Button.attrs
                        [ href
                            (model.contextItem.context
                                |> BoundedContext.id
                                |> Route.Namespaces
                                |> Route.routeToString
                            )
                        , class "text-nowrap"
                        ]
                    ]
                    [ text "Namespaces" ]
                ]
            ]
        ]


viewWithActions : BoundedContextCard.Model -> Card.Config Never
viewWithActions model =
    model
        |> BoundedContextCard.view
        |> Card.footer []
            [ Grid.simpleRow
                [ Grid.col [ Col.md7 ]
                    [ ButtonGroup.linkButtonGroup []
                        [ ButtonGroup.linkButton
                            [ Button.roleLink
                            , Button.attrs
                                [ href
                                    (model.contextItem.context
                                        |> BoundedContext.id
                                        |> Route.BoundedContextCanvas
                                        |> Route.routeToString
                                    )
                                ]
                            ]
                            [ text "Canvas" ]
                        , ButtonGroup.linkButton
                            [ Button.roleLink
                            , Button.attrs
                                [ href
                                    (model.contextItem.context
                                        |> BoundedContext.id
                                        |> Route.Namespaces
                                        |> Route.routeToString
                                    )
                                ]
                            ]
                            [ text "Namespaces" ]
                        ]
                    ]
                ]
            ]


view : Model -> Html Msg
view { contextItems, domain, presentation } =
    let
        contextCount =
            contextItems |> List.length

        contextCountText =
            if contextCount == 1 then
                "1 Bounded Context"

            else
                (contextCount |> String.fromInt) ++ " Bounded Contexts"

        title =
            Html.h5 []
                [ text <|
                    (domain |> Domain.name)
                        ++ ": "
                        ++ contextCountText
                ]
    in
    case presentation of
        Full ->
            div [ Spacing.mt3 ]
                [ title
                , div [ Spacing.mt2 ]
                    (contextItems
                        |> List.sortBy (\{ contextItem } -> contextItem.context |> BoundedContext.name)
                        |> List.map viewWithActions
                        |> chunksOfLeft 2
                        |> List.map Card.deck
                    )
                ]
                |> Html.map (\_ -> NoOp)

        Condensed ->
            div [ Spacing.mt3, Border.rounded, Border.all, Spacing.p2, class "shadow", class "condensed" ]
                [ title
                , Html.div [ Spacing.mt3 ]
                    (contextItems
                        |> List.sortBy (\{ contextItem } -> contextItem.context |> BoundedContext.name)
                        |> List.map condendsedWithActions
                    )
                ]
                |> Html.map (\_ -> NoOp)
