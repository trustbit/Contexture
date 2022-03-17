module Page.Domain.IndexRoot exposing (Model, initWithSubdomains, initWithoutSubdomains, Msg, update,view)

import Api
import Bootstrap.Grid as Grid
import Bootstrap.Grid.Col as Col
import Browser.Navigation as Nav
import Domain
import Domain.DomainId exposing (DomainId)
import Html exposing (Html)
import Html.Attributes exposing (..)
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup

import Page.Domain.Index as Index

type Visualization 
    = Bubble
    | Grid

type alias Model =
  { navKey : Nav.Key
  , configuration : Api.Configuration
  , domainPosition : Domain.DomainRelation
  , index : Index.Model
  , visualization : Visualization
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
    }
  , indexCmd |> Cmd.map DomainMsg 
  )

type Msg
  = DomainMsg Index.Msg
  | ChangeVisualization Visualization
  
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

 
viewBubble configuration =
    Html.node "bubble-visualization"
        [ attribute "baseApi" (Api.withoutQuery [] |> Api.url configuration)
        ]
        []
        
viewGridSwitch current =
    ButtonGroup.radioButtonGroup []
        [ ButtonGroup.radioButton (current == Grid) [ Button.primary, Button.onClick <| ChangeVisualization Grid ] [ Html.text "Grid" ]
        , ButtonGroup.radioButton (current == Bubble) [ Button.secondary, Button.onClick <| ChangeVisualization Bubble ] [ Html.text "Bubble" ]
        ]

viewBubbleSwitch current =
    ButtonGroup.radioButtonGroup []
        [ ButtonGroup.radioButton (current == Grid) [ Button.secondary, Button.onClick <| ChangeVisualization Grid ] [ Html.text "Grid" ]
        , ButtonGroup.radioButton (current == Bubble) [ Button.primary, Button.onClick <| ChangeVisualization Bubble ] [ Html.text "Bubble" ]
        ]

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
                        [ case model.visualization of
                            Grid ->
                                viewGridSwitch model.visualization
                            Bubble ->
                                viewBubbleSwitch model.visualization
                        ]
                    , Grid.col []
                        [ case model.visualization of
                            Grid ->
                                model.index
                                |> Index.view 
                                |> Html.map DomainMsg
                            Bubble ->
                                viewBubble model.configuration
                        ]
                    ]                       
                ]
    