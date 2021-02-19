module Page.Bcc.Edit.Key exposing (
    init, Model,
    update, Msg,
    view)

import Html
import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

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
import Page.ChangeKey as ChangeKey
import BoundedContext as BoundedContext exposing(BoundedContext, Name)
import Key as Key


type alias Model =
  { changingName : Maybe ChangeKey.Model
  , config : Api.Configuration
  , boundedContext : BoundedContext
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
  | KeyMsg ChangeKey.Msg
  | ChangeKey (Maybe Key.Key)
  | CancelChanging


noCommand model = (model, Cmd.none)


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    StartChanging ->
      model.boundedContext |> BoundedContext.key |> ChangeKey.init model.config
      |> Tuple.mapFirst (\m -> { model | changingName = Just m })
      |> Tuple.mapSecond (Cmd.map KeyMsg)


    CancelChanging ->
      noCommand { model | changingName = Nothing }

    ChangeKey key ->
      ( model
      , BoundedContext.assignKey model.config (model.boundedContext |> BoundedContext.id) key Saved
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

    KeyMsg name ->
      case model.changingName of
        Just keyModel ->
          ChangeKey.update name keyModel
          |> Tuple.mapFirst (\m -> { model | changingName = Just m})
          |> Tuple.mapSecond(Cmd.map KeyMsg)
        Nothing ->
          (model, Cmd.none)


view : Model -> Html Msg
view model =
  Form.group []
    ( case model.changingName of
        Just m ->
          let
            (events, disabled) =
              case m.value of
                Ok key ->
                  ([ onSubmit (ChangeKey key)],False)
                Err _ ->
                  ([], True)
          in [
            Form.form events
            [ Grid.row []
              [ Grid.col []
                [ viewCaption
                  [ text "Key"
                  , ButtonGroup.buttonGroup []
                    [ ButtonGroup.button
                      [ Button.primary
                      , Button.small
                      , Button.disabled disabled
                      ]
                      [ text "Assign new key" ]
                    , ButtonGroup.button [ Button.secondary, Button.small, Button.onClick CancelChanging] [ text "X" ]
                    ]
                  ]
                ]
              ]
            , Grid.row [ Row.attrs [ Spacing.pt2, style "min-height" "80px" ] ]
              [ Grid.col []
                [ ChangeKey.view m
                  |> Html.map KeyMsg
                ]
              ]
            ]
          ]

        Nothing ->
          [ Grid.row []
            [ Grid.col []
              [ viewCaption
                [ text "Key"
                , Button.button [ Button.outlinePrimary, Button.small, Button.onClick StartChanging] [ text "Assign key" ]
                ]
              ]
            ]
          , Grid.row [ Row.attrs [ Spacing.pt2, style "min-height" "80px"] ]
            [ Grid.col [ ]
              [ model.boundedContext
                |> BoundedContext.key
                |> Maybe.map Key.toString
                |> Maybe.withDefault ""
                |> text
              ]
            ]
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
