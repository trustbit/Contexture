module Page.Bcc.Edit.Name exposing (
    init, Model,
    update, Msg,
    view)

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

import Browser.Dom as Dom
import Task

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Flex as Flex

import Api

import BoundedContext as BoundedContext exposing(BoundedContext, Name)
import Html

type alias NameChange = 
  { name : String
  , potentialName : Result BoundedContext.Problem Name
  }


type alias Model =
  { changingName : Maybe NameChange
  , config : Api.Configuration
  , boundedContext : BoundedContext
  }


initNameChange boundedContext =
  { name = boundedContext |> BoundedContext.name
  , potentialName = boundedContext |> BoundedContext.name |> BoundedContext.isName
  }


init : Api.Configuration -> BoundedContext -> (Model, Cmd Msg)
init configuration model =
    ( { changingName = Nothing
      , config = configuration
      , boundedContext = model
      }
    , Cmd.none
    )

type Msg
  = Saved (Api.ApiResponse BoundedContext)
  | StartChanging
  | SetName String
  | ChangeName Name
  | CancelChanging
  | NoOp

noCommand model = (model, Cmd.none)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    StartChanging ->
      ( { model | changingName = model.boundedContext |> initNameChange |> Just }
      , Task.attempt (\_ -> NoOp) (Dom.focus "name")
      )

    CancelChanging ->
      noCommand { model | changingName = Nothing }

    ChangeName name ->
      ( model
      , BoundedContext.changeName model.config (model.boundedContext |> BoundedContext.id) name Saved
      )

    Saved (Ok context) ->
      noCommand
        { model
        | boundedContext = context
        , changingName = Nothing
        }

    Saved (Err error) ->
      Debug.todo "Error"
      -- noCommand <| Debug.log "Error on save " model

    SetName name ->
      { model 
      | changingName =
          model.changingName 
          |> Maybe.map (\m -> { m | name = name, potentialName = BoundedContext.isName name } ) 
      }
      |> noCommand
    
    NoOp ->
      noCommand model

view : Model -> Html Msg
view model =
  Form.group []
    ( case model.changingName of
        Just { name, potentialName } ->
          let
            (events, disabled, inputType) = 
              case potentialName of
                Ok boundedContext ->
                  ([ onSubmit (ChangeName boundedContext)],False, Input.success)
                Err _ ->
                  ([], True, Input.danger)
          in 
          [ Form.form events
            [ Grid.row []
              [ Grid.col []
                [ viewCaption
                  [ text "Name"
                  , ButtonGroup.buttonGroup []
                    [ ButtonGroup.button 
                      [ Button.primary
                      , Button.small
                      , Button.disabled disabled
                      ]
                      [ text "Change Name" ]
                    , ButtonGroup.button [ Button.secondary, Button.small, Button.onClick CancelChanging] [text "X"]
                    ]
                  ]
                ]
              ]
            , Grid.row [ Row.attrs [ Spacing.pt2, style "min-height" "80px"] ]
              [ Grid.col [] 
                [ Input.text 
                  [ Input.id "name"
                  , Input.value name
                  , Input.onInput SetName
                  , inputType
                  ]
                , Form.help [] [ text "Naming is hard. Writing down the name of your context and gaining agreement as a team will frame how you design the context."] 
                , Form.invalidFeedback [] [ text "A name for a Bounded Context is required!" ]
                ]
              ]
            ]
          ]

        Nothing ->
          [ Grid.row []
            [ Grid.col []
              [ viewCaption
                [ text "Name"
                , Button.button [ Button.outlinePrimary, Button.small, Button.onClick StartChanging] [text "Assign new name"]
                ]
              ]
            ]
          , Grid.row [ Row.attrs [ Spacing.pt2, style "min-height" "80px"] ]
            [ Grid.col [ ] [ Html.h5[] [ model.boundedContext |> BoundedContext.name |> text ] ]]
          ]
      )


viewCaption : List(Html msg) -> Html msg
viewCaption content =
  div
    [ Flex.justifyBetween
    , Flex.block
    , style "background-color" "lightGrey"
    , Spacing.p2
    ]
    content
