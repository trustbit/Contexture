module Page.Domain.Bubble exposing (..)

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
    , contextItems : RemoteData.WebData (List BoundedContextCard.Item)
    , communication : RemoteData.WebData Communication.Communication
    , contextModels : RemoteData.WebData (List BoundedContextCard.Model)
    }


type MoreInfoDetails
    = Domain Index.Model
    | SubDomain Edit.Model



-- | BoundedContext BoundedContextId


init : Nav.Key -> Api.Configuration -> ( Model, Cmd Msg )
init key config =
    ( { navKey = key
      , configuration = config
      , selectedElement = Ports.None
      , moreInfo = Nothing
      , modalVisibility = Modal.hidden
      , communication = RemoteData.NotAsked
      , contextItems = RemoteData.NotAsked
      , contextModels = RemoteData.NotAsked
      }
    , Cmd.none
    )


type Msg
    = MoreInfoChanged (Result Decode.Error Ports.MoreInfoParameters)
    | ShowMoreInfo Ports.MoreInfoParameters
    | Loaded (ApiResponse (List BoundedContextCard.Item))
    | CommunicationLoaded (ApiResponse Collaboration.Collaborations)
    | DomainIndexMsg Index.Msg
    | DomainEditMsg Edit.Msg
    | CloseModal


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

                        Ports.BoundedContext _ subdomainId _ ->
                            Index.init model.configuration model.navKey (Domain.Subdomain subdomainId)
                                |> Tuple.mapFirst (Domain >> Just)
                                |> Tuple.mapSecond (Cmd.map DomainIndexMsg)
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
            ( { model
                | contextItems = items |> RemoteData.fromResult
              }
            , Cmd.none
            )

        CommunicationLoaded collaborations ->
            ( { model
                | communication =
                    collaborations
                        |> RemoteData.fromResult
                        |> RemoteData.map Communication.asCommunication
              }
            , Cmd.none
            )

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


loadAll : Api.Configuration -> DomainId -> Cmd Msg
loadAll config domain =
    Http.get
        { url = Api.boundedContexts domain |> Api.url config
        , expect = Http.expectJson Loaded (Decode.list BoundedContextCard.decoder)
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
