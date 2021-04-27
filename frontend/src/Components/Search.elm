module Components.Search exposing (main)

import Url
import Http
import RemoteData


import Api as Api
import Page.Bcc.BoundedContextCard as BoundedContextCard
import BoundedContext as BoundedContext
import BoundedContext.BoundedContextId as BoundedContextId
import Domain.DomainId as DomainId
import BoundedContext.Canvas
import BoundedContext.Namespace as Namespace
import Browser
import ContextMapping.Collaboration as Collaboration exposing (Collaborations)
import Domain exposing (Domain)
import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Json.Decode as Decode
import Json.Decode.Pipeline as JP
import Page.Bcc.BoundedContextsOfDomain as BoundedContext
import Url
import Url.Builder exposing (QueryParameter)
import Url.Parser
import Url.Parser.Query
import Dict


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


init : Decode.Value -> ( Model, Cmd Msg )
init flag =
    case flag |> Decode.decodeValue flagsDecoder of
        Ok decoded ->
            (
                { configuration = decoded.apiBase
                , domains = decoded.domains
                , collaboration = decoded.collaboration
                , items = RemoteData.Loading
                , models = RemoteData.Loading
                }
            , findAll decoded.apiBase []
            )

        Err e ->
            ( { configuration = Api.baseConfig "", domains = [], collaboration = [], items = RemoteData.Failure <| Http.BadBody (Debug.toString e), models = RemoteData.Failure <| Http.BadBody (Debug.toString e) }
            , Cmd.none
            )


type alias Flags =
    { collaboration : Collaborations
    , domains : List Domain
    , apiBase : Api.Configuration
    }

baseConfiguration =
    Decode.string
        |> Decode.andThen
            (\v ->
                case v |> Url.fromString of
                    Just url ->
                        url |> Api.config |> Decode.succeed
                    Nothing ->
                        if not <| String.isEmpty v
                        then v |> Api.baseConfig |> Decode.succeed
                        else Decode.fail <| "Could not decode url from " ++ v
            )


flagsDecoder =
    Decode.map3 Flags
        (Decode.field "collaboration" (Decode.list Collaboration.decoder))
        (Decode.field "domains" (Decode.list Domain.domainDecoder))
        (Decode.field "apiBase" baseConfiguration)


type alias Model =
    { configuration : Api.Configuration
    , domains : List Domain
    , collaboration : Collaborations
    , items : RemoteData.WebData (List BoundedContextCard.Item)
    , models : RemoteData.WebData (List BoundedContext.Model)
    }


type Msg
    = BoundedContextsFound (Api.ApiResponse (List BoundedContextCard.Item))
    | BoundedContextMsg BoundedContext.Msg


updateModels : Model -> Model
updateModels model =
    { model
    | models =
        model.items
        |> RemoteData.map (\items ->
            initModel model.configuration model.collaboration items model.domains
        )
    }


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        BoundedContextMsg m ->
            ( model, Cmd.none )

        BoundedContextsFound found ->
            ( updateModels { model | items = RemoteData.fromResult found }, Cmd.none)


viewItems : List BoundedContext.Model -> Html Msg
viewItems items =
    items
    |> List.map BoundedContext.view
    |> List.map (Html.map BoundedContextMsg)
    |> div [ class "container" ]


view : Model -> Html Msg
view model =
    case model.models of
        RemoteData.Success items ->
            viewItems items
        e ->
            text <| "Could not load data: " ++ (Debug.toString e)


subscriptions : Model -> Sub Msg
subscriptions model =
    Sub.none


findAll : Api.Configuration -> List QueryParameter -> Cmd Msg
findAll config query =
  Http.get
    { url = Api.allBoundedContexts [] |> Api.urlWithQueryParameters config query
    , expect = Http.expectJson BoundedContextsFound (Decode.list BoundedContextCard.decoder)
    }
