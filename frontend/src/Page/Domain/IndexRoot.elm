module Page.Domain.IndexRoot exposing (Model, initWithSubdomains, initWithoutSubdomains, Msg, update,view, subscriptions)

import Api
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Browser.Navigation as Nav
import Domain
import Domain.DomainId exposing (DomainId)
import Html exposing (Html)
import Html exposing (Html, button, div, text)
import Html.Events exposing (onClick)
import Html.Attributes exposing (..)
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup

import Page.Domain.Index as Index
import Page.Domain.Ports as Ports

import Json.Decode as Decode exposing (Error)

type Visualization 
    = Bubble
    | Grid

type alias Model =
  { navKey : Nav.Key
  , configuration : Api.Configuration
  , domainPosition : Domain.DomainRelation
  , index : Index.Model
  , visualization : Visualization
  , selectedElement : Maybe Ports.MoreInfoParameters
  , moreInfo : Maybe Ports.MoreInfoParameters
  }
   

initWithSubdomains : Api.Configuration -> Nav.Key -> DomainId -> (Model, Cmd Msg)
initWithSubdomains baseUrl key parentDomain =
  init baseUrl key (Domain.Subdomain parentDomain)

initWithoutSubdomains : Api.Configuration -> Nav.Key -> (Model, Cmd Msg)
initWithoutSubdomains baseUrl key =
  init baseUrl key Domain.Root


   
init : Api.Configuration -> Nav.Key -> Domain.DomainRelation -> (Model, Cmd Msg)
init config key domainPosition =
  let
    (indexModel, indexCmd) = Index.init config key domainPosition
  in
  ( { navKey = key
    , configuration = config
    , domainPosition = domainPosition
    , index = indexModel
    , visualization = Grid
    , selectedElement = Nothing
    , moreInfo = Nothing
    }
  , indexCmd |> Cmd.map DomainMsg 
  )

type Msg
  = DomainMsg Index.Msg
  | ChangeVisualization Visualization
  | ShowMoreInfo Ports.MoreInfoParameters
  | MoreInfoChanged (Result Decode.Error Ports.MoreInfoParameters)
  
update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
    case msg of
        DomainMsg domainMsg ->
            Index.update domainMsg model.index
            |> Tuple.mapFirst (\m -> { model | index = m})
            |> Tuple.mapSecond (Cmd.map DomainMsg)
            
        ChangeVisualization new ->
            ( { model | visualization = new }
            , Cmd.none 
            )

        ShowMoreInfo id ->
            ( { model | moreInfo = Just id }
                        , Cmd.none
                        )

        MoreInfoChanged (Ok result) ->
            ( { model | selectedElement = Just result}
            , Cmd.none)
        
        MoreInfoChanged (Err error) ->
            Debug.todo <| "Decoding failed: " ++ (Debug.toString error)



 
viewBubble configuration =
            Html.node "bubble-visualization"
           [ attribute "baseApi" (Api.withoutQuery [] |> Api.url configuration), attribute "moreinfo" ""
           ]
           []

        
viewSwitch current =
    ButtonGroup.radioButtonGroup []
        [ ButtonGroup.radioButton (current == Grid) [ (if current == Grid then Button.primary else Button.secondary), Button.onClick <| ChangeVisualization Grid ] [ Html.text "Grid" ]
        , ButtonGroup.radioButton (current == Bubble) [(if current == Grid then Button.secondary else Button.primary), Button.onClick <| ChangeVisualization Bubble ] [ Html.text "Bubble" ]
        ]

viewMore { selectedElement, moreInfo} =
    case selectedElement of
        Just element ->
            Html.button[ class "btn btn-primary" , onClick <| ShowMoreInfo element ][Html.text "More"]
        Nothing ->
            Html.button[ class "btn btn-primary btn-disabled" ] [Html.text "More"]

view : Model -> Html Msg
view model =
    case model.domainPosition of
        Domain.Subdomain _ -> 
            model.index
            |> Index.view 
            |> Html.map DomainMsg
        Domain.Root -> 
            Grid.containerFluid [] 
                [ Grid.row []
                    [ Grid.col [Col.xs1]   
                        [ viewSwitch model.visualization ]
                    , Grid.col []
                        [ case model.visualization of
                            Grid ->
                                model.index
                                |> Index.view 
                                |> Html.map DomainMsg
                            Bubble ->
                                viewBubble model.configuration
                        ]
                     , Grid.col [Col.xs1]
                               [ if model.visualization == Bubble then
                                       viewMore model
                               else
                                    Html.p[][]
                               ]
                    ]                       
                ]
    

subscriptions : Model -> Sub Msg
subscriptions _ =
    Ports.moreInfoChanged MoreInfoChanged