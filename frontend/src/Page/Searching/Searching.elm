module Page.Searching.Searching exposing (..)

import Api as Api
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Bootstrap.Grid.Row as Row
import Bootstrap.Utilities.Border as Border
import Bootstrap.Utilities.Spacing as Spacing
import Bounce exposing (Bounce)
import BoundedContext as BoundedContext
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
import Json.Encode as Encode
import Json.Decode.Pipeline as JP
import Page.Searching.Filter as Filter
import Page.Searching.Ports as Ports exposing (SearchResultPresentation(..),SunburstPresentation(..))
import RemoteData
import Task
import Url
import Url.Builder
import Url.Parser
import Url.Parser.Query


initSearchResult : Api.Configuration -> BoundedContext.Presentation -> Collaboration.Collaborations -> List Domain -> List BoundedContextCard.Item -> List BoundedContext.Model
initSearchResult config presentation collaboration domains searchResults =
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
            searchResults
                |> List.foldl groupItemsByDomainId Dict.empty

        getContexts domain =
            boundedContextsPerDomain
                |> Dict.get (domain |> Domain.id |> DomainId.idToString)
                |> Maybe.withDefault []
    in
    domains
        |> List.map (\domain -> BoundedContext.init config presentation domain (getContexts domain) collaboration)
        |> List.filter (\i -> not <| List.isEmpty i.contextItems)


init apiBase presentation =
    let
        ( filterModel, filterCmd ) =
            Filter.init apiBase
    in
    ( { configuration = apiBase
      , textualModel = 
            { presentation =
                case presentation of
                    Just (Textual p) -> 
                        p
                    _ -> 
                        BoundedContext.Full
            , domains = RemoteData.Loading
            , collaboration = RemoteData.Loading
            , searchResults = RemoteData.NotAsked
            , searchResponse = RemoteData.Loading
            }
      , filter = filterModel
      , presentation = presentation |> Maybe.withDefault (Textual BoundedContext.Full)
      }
    , Cmd.batch
        [ filterCmd |> Cmd.map FilterMsg
        , getDomains apiBase
        , getCollaborations apiBase
        ]
    )


type alias TextualModel =
    { presentation : BoundedContext.Presentation
    , domains : RemoteData.WebData (List Domain)
    , searchResponse : RemoteData.WebData (List BoundedContextCard.Item)
    , collaboration : RemoteData.WebData Collaborations
    , searchResults : RemoteData.WebData (List BoundedContext.Model)
    }

type alias Model =
    { configuration : Api.Configuration
    , textualModel : TextualModel
    , filter : Filter.Model
    , presentation : SearchResultPresentation
    }


type Msg
    = DomainsLoaded (Api.ApiResponse (List Domain))
    | CollaborationsLoaded (Api.ApiResponse Collaborations)
    | BoundedContextsFound (Api.ApiResponse (List BoundedContextCard.Item))
    | BoundedContextMsg BoundedContext.Msg
    | FilterMsg Filter.Msg
    | SwitchPresentation SearchResultPresentation
    | QueryStringChanged String


updateSearchResults :(TextualModel -> TextualModel) -> Model -> Model
updateSearchResults updater model =
    let
        updated = updater model.textualModel
    in
        { model 
        | textualModel = 
            { updated
            | searchResults =
                RemoteData.map3
                      (initSearchResult model.configuration updated.presentation)
                      updated.collaboration
                      updated.domains
                      updated.searchResponse
            }
        }



update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        BoundedContextMsg m ->
            ( model, Cmd.none )
            
        QueryStringChanged q ->
            case q |> Decode.decodeString (Decode.list Filter.filterParameterDecoder) of
                Ok decoded ->
                    (model, findAll model.configuration decoded)
                Err e ->
                    (model, Cmd.none)
                

        BoundedContextsFound foundItems->
            ( updateSearchResults
                (\m ->{ m | searchResponse = foundItems |> RemoteData.fromResult })
                model
            , Cmd.none
            )

        DomainsLoaded domains ->
            ( updateSearchResults 
                (\m ->{ m | domains = domains |> RemoteData.fromResult })
                model
            , Cmd.none
            )

        CollaborationsLoaded collaborations ->
            ( updateSearchResults 
                (\m ->{ m | collaboration = collaborations |> RemoteData.fromResult })
                model
            , Cmd.none
            )

        FilterMsg msg_ ->
            Filter.update msg_ model.filter
            |> Tuple.mapFirst (\m -> { model | filter = m })
            |> Tuple.mapSecond (Cmd.map FilterMsg)


        SwitchPresentation newPresentation ->
            ( { model | presentation = newPresentation }
                |>
                case newPresentation of
                    Ports.Textual presentation ->
                        updateSearchResults (\m -> { m | presentation = presentation})
                    Ports.Sunburst _ ->
                        identity
            , Cmd.batch
                ( Ports.store newPresentation 
                    :: 
                     case newPresentation of
                        Ports.Textual _ ->
                            [ findAll model.configuration model.filter.currentParameters ]
                        _ ->
                            []
                )
            )


stickyTopAttributes =
    [ class "sticky-top", style "top" "5rem", style "height" "calc(100vh - 5rem)" ]


viewItems : RemoteData.WebData (List BoundedContext.Model) -> List (Html Msg)
viewItems searchResults =
    case searchResults of
        RemoteData.Success items ->
            [ Grid.row []
                [ Grid.col [ Col.xs3 ]
                    [ Html.h5 [] [ text "Search results" ] ]
                , if List.isEmpty items then
                    Grid.col [] [ text "No items found!" ]

                  else
                    Grid.col []
                        [ Html.b [] [ text (items |> List.length |> String.fromInt) ]
                        , text " Domain(s) with "
                        , Html.b [] [ text (items |> List.map (\b -> b.contextItems |> List.length) |> List.sum |> String.fromInt) ]
                        , text " Bounded Context(s)"
                        ]
                ]
            , Grid.row [ Row.attrs [ Spacing.mt2, Border.top ] ]
                [ Grid.col []
                    (
                            items
                            |> List.sortBy (\b -> b.domain |> Domain.name)
                            |> List.map BoundedContext.view
                            |> List.map (Html.map BoundedContextMsg)       
                    )
                ]
            ]

        e ->
            [ Grid.simpleRow [ Grid.col [] [ text <| "Could not execute search: " ++ Debug.toString e ] ]
            ]
            
viewSunburst configuration filterParameters mode =
     Html.node "visualization-sunburst"
        [ attribute "baseApi" (Api.withoutQuery [] |> Api.url configuration)
        , attribute "query" (filterParameters |> filterParametersAsQuery |> Url.Builder.toQuery)
        , attribute "mode" (
            case mode of
                Filtered -> "filtered"
                Highlighted -> "highlighted" 
            )
        ]   
        []


presentationOptionView presentation =
    Card.config []
        |> Card.block []
            [ Block.titleH5 [] [ text "Presentation mode" ]
            , Block.text []
                [ Html.p [] [text "Text based"]
                --, ButtonGroup.radioButtonGroupItem[] 
                , ButtonGroup.radioButtonGroup [ButtonGroup.attrs [ Spacing.ml2]]
                        [ ButtonGroup.radioButton
                            (presentation == Textual BoundedContext.Full)
                            [ Button.secondary, Button.onClick <| SwitchPresentation (Textual BoundedContext.Full) ]
                            [ text "Full" ]
                        , ButtonGroup.radioButton
                            (presentation == Textual BoundedContext.Condensed)
                            [ Button.secondary, Button.onClick <| SwitchPresentation (Textual BoundedContext.Condensed) ]
                            [ text "Condensed" ]
                        ]
                ]
            , Block.text []
                [ Html.p [] [text "Visualisation"] 
                --, ButtonGroup.radioButtonGroupItem []
                , ButtonGroup.radioButtonGroup [ButtonGroup.attrs [ Spacing.ml2]] 
                        [ ButtonGroup.radioButton
                            (presentation == Sunburst Filtered)
                            [ Button.secondary, Button.onClick <| SwitchPresentation (Sunburst Filtered)]
                            [ text "Filtered" ]
                        , ButtonGroup.radioButton
                             (presentation == Sunburst Highlighted)
                             [ Button.secondary, Button.onClick <| SwitchPresentation (Sunburst Highlighted)]
                             [ text "Highlighted" ]
                        ]
                    --]
                    
                ]
            ]
        |> Card.view


view : Model -> Html Msg
view model =
    Grid.containerFluid []
        [ Grid.simpleRow
            [ Grid.col [ Col.md4, Col.attrs stickyTopAttributes ]
                [ Grid.row [ Row.attrs [ Spacing.mb3 ] ]
                    [ Grid.col [] [ model.filter |> Filter.view |> Html.map FilterMsg ] ]
                , Grid.row [ Row.attrs [ Spacing.mb3 ] ]
                    [ Grid.col [] [ model.presentation |> presentationOptionView ] ]
                ]
            , Grid.col [ Col.attrs [ class "position-relative" ] ] (
                case model.presentation of
                    Textual _ ->
                        viewItems model.textualModel.searchResults
                    Sunburst mode ->
                        [ viewSunburst model.configuration model.filter.currentParameters mode ]
                )
            ]
        ]



filterParametersAsQuery query =
    query |> List.map (\q -> Url.Builder.string q.name q.value)

findAll : Api.Configuration -> List Filter.FilterParameter -> Cmd Msg
findAll config query =
    Http.get
        { url =
            Api.allBoundedContexts []
                |> Api.urlWithQueryParameters config (filterParametersAsQuery query)
        , expect = Http.expectJson BoundedContextsFound (Decode.list BoundedContextCard.decoder)
        }


getDomains : Api.Configuration -> Cmd Msg
getDomains config =
    Http.get
        { url = Api.domains [] |> Api.url config
        , expect = Http.expectJson DomainsLoaded (Decode.list Domain.domainDecoder)
        }


getCollaborations : Api.Configuration -> Cmd Msg
getCollaborations config =
    Http.get
        { url = Api.collaborations |> Api.url config
        , expect = Http.expectJson CollaborationsLoaded (Decode.list Collaboration.decoder)
        }

subscriptions : Model -> Sub Msg
subscriptions model =
  Sub.batch 
    [ Filter.subscriptions model.filter |> Sub.map FilterMsg
    , Ports.onQueryStringChanged QueryStringChanged
    ]