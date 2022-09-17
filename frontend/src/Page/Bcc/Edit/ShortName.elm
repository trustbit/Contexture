module Page.Bcc.Edit.ShortName exposing (
    init, Model,
    update, Msg,
    view)

import Html
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
import Page.ChangeShortName as ChangeShortName
import BoundedContext as BoundedContext exposing(BoundedContext, Name)
import ShortName as ShortName


type alias Model =
  { changingName : Maybe ChangeShortName.Model
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
  | ShortNameMsg ChangeShortName.Msg
  | ChangeShortName (Maybe ShortName.ShortName)
  | CancelChanging
  | NoOp


noCommand model = (model, Cmd.none)


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    StartChanging ->
      model.boundedContext |> BoundedContext.shortName |> ChangeShortName.init model.config
      |> Tuple.mapFirst (\m -> { model | changingName = Just m })
      |> Tuple.mapSecond (Cmd.map ShortNameMsg)
      |> Tuple.mapSecond(\c -> Cmd.batch [ c, Task.attempt (\_ -> NoOp) (Dom.focus "shortName")])


    CancelChanging ->
      noCommand { model | changingName = Nothing }

    ChangeShortName shortName ->
      ( model
      , BoundedContext.assignShortName model.config (model.boundedContext |> BoundedContext.id) shortName Saved
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

    ShortNameMsg name ->
      case model.changingName of
        Just shortNameModel ->
          ChangeShortName.update name shortNameModel
          |> Tuple.mapFirst (\m -> { model | changingName = Just m})
          |> Tuple.mapSecond(Cmd.map ShortNameMsg)
        Nothing ->
          (model, Cmd.none)

    NoOp ->
      noCommand model



view : Model -> Html Msg
view model =
  Form.group []
    ( case model.changingName of
        Just m ->
          let
            (events, disabled) =
              case m.value of
                Ok shortName ->
                  ([ onSubmit (ChangeShortName shortName)],False)
                Err _ ->
                  ([], True)
          in [
            Form.form events
            [ Grid.row []
              [ Grid.col []
                [ viewCaption
                  [ text "Short name"
                  , ButtonGroup.buttonGroup []
                    [ ButtonGroup.button
                      [ Button.primary
                      , Button.small
                      , Button.disabled disabled
                      ]
                      [ text "Assign new short name" ]
                    , ButtonGroup.button [ Button.secondary, Button.small, Button.onClick CancelChanging] [ text "X" ]
                    ]
                  ]
                ]
              ]
            , Grid.row [ Row.attrs [ Spacing.pt2, style "min-height" "80px" ] ]
              [ Grid.col []
                [ ChangeShortName.view m
                  |> Html.map ShortNameMsg
                ]
              ]
            ]
          ]

        Nothing ->
          [ Grid.row []
            [ Grid.col []
              [ viewCaption
                [ text "Short name"
                , Button.button [ Button.outlinePrimary, Button.small, Button.onClick StartChanging] [ text "Assign short name" ]
                ]
              ]
            ]
          , Grid.row [ Row.attrs [ Spacing.pt2, style "min-height" "80px"] ]
            [ Grid.col [ ]
              [ model.boundedContext
                |> BoundedContext.shortName
                |> Maybe.map ShortName.toString
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
