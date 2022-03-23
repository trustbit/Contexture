module Page.Domain.Bubble exposing (..)

import Api exposing (ApiResponse, ApiResult)
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Card as Card
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Modal as Modal
import BoundedContext
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import Browser.Navigation as Nav
import Components.BoundedContextCard as BCC
import ContextMapping.Collaboration as Collaboration
import ContextMapping.Communication as Communication
import ContextMapping.Collaborator as Collaborator
import Domain
import Domain.DomainId exposing (DomainId)
import Html exposing (Html, button, div, text)
import Html.Attributes as Attr exposing (..)
import Html.Events exposing (onClick)
import Http
import Json.Decode as Decode exposing (Error)
import Page.Domain.Edit as Edit
import Page.Domain.Index as Index
import Page.Domain.Ports as Ports
import RemoteData
import Route
import Url


type alias Model =
    { navKey : Nav.Key
    , configuration : Api.Configuration
    , selectedElement : Ports.MoreInfoParameters
    , moreInfo : Maybe MoreInfoDetails
    , modalVisibility : Modal.Visibility
    }


type alias BoundedContextModel =
    { contextItems : RemoteData.WebData BCC.Item
    , communication : RemoteData.WebData Communication.Communication
    , model : RemoteData.WebData BCC.Model
    }


type MoreInfoDetails
    = Domain Index.Model
    | SubDomain Edit.Model
    | BoundedContext BoundedContextModel


init : Nav.Key -> Api.Configuration -> ( Model, Cmd Msg )
init key config =
    ( { navKey = key
      , configuration = config
      , selectedElement = Ports.None
      , moreInfo = Nothing
      , modalVisibility = Modal.hidden
     }
    , Cmd.none
    )


type Msg
    = MoreInfoChanged (Result Decode.Error Ports.MoreInfoParameters)
    | ShowMoreInfo Ports.MoreInfoParameters
    | Loaded (ApiResponse BCC.Item)
    | CommunicationLoaded (ApiResponse Collaboration.Collaborations)
    | DomainIndexMsg Index.Msg
    | DomainEditMsg Edit.Msg
    | CloseModal


updateBoundedContext bcModel =
    { bcModel
    | model =
        RemoteData.map2
            (\item communication ->
            BCC.init (Communication.communicationFor (item.context |> BoundedContext.id |> Collaborator.BoundedContext) communication) item
            )
            bcModel.contextItems
            bcModel.communication
    }

update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        MoreInfoChanged (Ok result) ->
            ( { model | selectedElement = result }
            , Cmd.none
            )

        MoreInfoChanged (Err error) ->
            Debug.todo <| "Decoding failed: " ++ Debug.toString error

        ShowMoreInfo id ->
            let
                ( m, cmds ) =
                    case id of
                        Ports.None ->
                            ( Nothing, Cmd.none )

                        Ports.Domain domainId ->
                            Index.init model.configuration model.navKey (Domain.Subdomain domainId)
                                |> Tuple.mapFirst (Domain >> Just)
                                |> Tuple.mapSecond (Cmd.map DomainIndexMsg)

                        Ports.SubDomain _ subdomainId ->
                            Edit.init model.navKey model.configuration subdomainId
                                |> Tuple.mapFirst (SubDomain >> Just)
                                |> Tuple.mapSecond (Cmd.map DomainEditMsg)

                        Ports.BoundedContext _ subdomainId contextId ->
                            ( { communication = RemoteData.Loading
                              , contextItems = RemoteData.Loading
                              , model = RemoteData.NotAsked
                              }
                                |> BoundedContext
                                |> Just
                            , Cmd.batch [ loadAll model.configuration subdomainId contextId, loadAllConnections model.configuration ]
                            )
            in
            ( { model
                | moreInfo = m
                , modalVisibility = Modal.shown
              }
            , cmds
            )

        DomainIndexMsg indexMsg ->
            case model.moreInfo of
                Just (Domain d) ->
                    Index.update indexMsg d
                        |> Tuple.mapFirst
                            (\m ->
                                { model | moreInfo = Just (Domain m) }
                            )
                        |> Tuple.mapSecond (Cmd.map DomainIndexMsg)

                _ ->
                    ( model, Cmd.none )

        DomainEditMsg editMsg ->
            case model.moreInfo of
                Just (SubDomain d) ->
                    Edit.update editMsg d
                        |> Tuple.mapFirst
                            (\m ->
                                { model | moreInfo = Just (SubDomain m) }
                            )
                        |> Tuple.mapSecond (Cmd.map DomainEditMsg)

                _ ->
                    ( model, Cmd.none )

        Loaded items ->
            case model.moreInfo of
                Just (BoundedContext bc) ->
                    ( { model
                      | moreInfo =
                         { bc
                         | contextItems = items |> RemoteData.fromResult
                         }
                         |> updateBoundedContext
                         |> BoundedContext
                         |> Just
                      }
                    , Cmd.none
                    )
                _ ->
                    ( model, Cmd.none )

        CommunicationLoaded collaborations ->
            case model.moreInfo of
                Just (BoundedContext bc) ->
                    ( { model
                    | moreInfo =
                        { bc    
                        | communication =
                            collaborations
                                |> RemoteData.fromResult
                                |> RemoteData.map Communication.asCommunication
                        }
                         |> updateBoundedContext
                         |> BoundedContext
                         |> Just
                    }
                    , Cmd.none
                    )
                _ ->
                    ( model, Cmd.none )

        CloseModal ->
            ( { model | modalVisibility = Modal.hidden }
            , Cmd.none
            )


viewBubble configuration =
    Html.node "bubble-visualization"
        [ attribute "baseApi" (Api.withoutQuery [] |> Api.url configuration)
        , attribute "moreinfo" ""
        ]
        []


viewMoreDetails item =
    case item of
        Domain d ->
            d
                |> Index.view
                |> Html.map DomainIndexMsg

        SubDomain d ->
            d
                |> Edit.view
                |> Html.map DomainEditMsg

        BoundedContext bc ->
            case bc.model of
                RemoteData.Success m ->
                    m
                    |> BCC.view
                    |> Card.view
                _ ->
                    text "Loading..."


view model =
    Grid.row []
        [ Grid.col []
            [ model.configuration
                |> viewBubble
            ]
        , Grid.col []
            (case model.selectedElement of
                Ports.None ->
                    [ Button.button [ Button.primary, Button.disabled True ] [ Html.text "More" ] ]

                other ->
                    [ Grid.row []
                        [ Grid.col []
                            [ Button.button [ Button.primary, Button.onClick <| ShowMoreInfo other ] [ Html.text "More" ] ]
                        ]
                    , Grid.row []
                        [ Grid.col []
                            [ case model.moreInfo of
                                Just more ->
                                    Modal.config CloseModal
                                        |> Modal.attrs [ Attr.class "modal-xl" ]
                                        |> Modal.scrollableBody True
                                        |> Modal.hideOnBackdropClick True
                                        |> Modal.body [] [ viewMoreDetails more ]
                                        |> Modal.view model.modalVisibility

                                Nothing ->
                                    text ""
                            ]
                        ]
                    ]
            )
        ]


subscriptions : Model -> Sub Msg
subscriptions _ =
    Ports.moreInfoChanged MoreInfoChanged


loadAll : Api.Configuration -> DomainId -> BoundedContextId -> Cmd Msg
loadAll config domain context =
    let
        filter bccs =
            bccs
                |> List.filter (\bc -> (bc.context |> BoundedContext.id) == context)
                |> List.head

        decoder =
            Decode.list BCC.decoder
                |> Decode.andThen
                    (\bccs ->
                        case filter bccs of
                            Just bc ->
                                Decode.succeed bc

                            Nothing ->
                                Decode.fail "Could not find single context"
                    )
    in
    Http.get
        { url = Api.boundedContexts domain |> Api.url config
        , expect = Http.expectJson Loaded decoder
        }


loadAllConnections : Api.Configuration -> Cmd Msg
loadAllConnections config =
    Http.get
        { url = Api.collaborations |> Api.url config
        , expect = Http.expectJson CommunicationLoaded (Decode.list Collaboration.decoder)
        }


findAllDomains : Api.Configuration -> ApiResult (List Domain.Domain) msg
findAllDomains base =
    let
        request toMsg =
            Http.get
                { url = Api.domains [] |> Api.url base
                , expect = Http.expectJson toMsg Domain.domainsDecoder
                }
    in
    request
