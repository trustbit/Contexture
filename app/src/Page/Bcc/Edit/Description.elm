module Page.Bcc.Edit.Description exposing (
    init, Model,
    update, Msg,
    view)

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Button as Button
import Bootstrap.ButtonGroup as ButtonGroup
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Flex as Flex

import Api

import BoundedContext.BoundedContextId exposing(BoundedContextId)
import BoundedContext.Description as Description exposing(Description)
import Html
import RemoteData

type alias Model =
  { description : RemoteData.WebData Description
  , changingDescription : Maybe Description
  , config : Api.Configuration
  , boundedContextId : BoundedContextId
  }


init : Api.Configuration -> BoundedContextId -> Description -> (Model, Cmd Msg)
init configuration id model =
    ( { description = RemoteData.succeed model
      , changingDescription = Nothing
      , config = configuration
      , boundedContextId = id
      }
    , Cmd.none
    )

type Msg
  = Saved (Api.ApiResponse Description)
  | StartChanging
  | SetDescription String
  | SaveDescription Description
  | CancelChanging


noCommand model = (model, Cmd.none)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    StartChanging ->
      noCommand { model | changingDescription = model.description |> RemoteData.toMaybe }

    CancelChanging ->
      noCommand { model | changingDescription = Nothing }

    SaveDescription description ->
      ( model
      , Description.update model.config model.boundedContextId description Saved
      )

    Saved (Ok description) ->
      noCommand
        { model
        | description = RemoteData.succeed description
        , changingDescription = Nothing
        }

    Saved (Err error) ->
      Debug.todo "Error"
      noCommand <| Debug.log "Error on save " model

    SetDescription description ->
      { model | changingDescription = Just description }
      |> noCommand

view : Model -> Html Msg
view model =
  Form.group []
    ( case model.changingDescription of
        Just description ->
          [ Grid.row []
            [ Grid.col []
              [ viewCaption
                [ text "Description"
                , ButtonGroup.buttonGroup []
                  [ ButtonGroup.button [ Button.primary, Button.small, Button.onClick (SaveDescription description)] [text "Update"]
                  , ButtonGroup.button [ Button.secondary, Button.small, Button.onClick CancelChanging] [text "X"]
                  ]
                ]
              ]
            ]
          , Grid.row [ Row.attrs [ Spacing.pt2, style "min-height" "100px"] ]
            [ Grid.col [] 
              [ Textarea.textarea
                  [ Textarea.id "description"
                  , Textarea.value description
                  , Textarea.onInput SetDescription
                  ]
              , Form.help [] [ text "A few sentences describing the why and what of the context in business language. No technical details here."] 
              ]
            ]
          ]

        Nothing ->
          [ Grid.row []
            [ Grid.col []
              [ viewCaption
                [ text "Description"
                , Button.button [ Button.outlinePrimary, Button.small, Button.onClick StartChanging] [text "Change Description"]
                ]
              ]
            ]
          , Grid.row [ Row.attrs [ Spacing.pt2, style "min-height" "100px"] ]
            [ case model.description of
                RemoteData.Success description ->
                  if String.isEmpty description
                  then Grid.col [Col.attrs [ class "text-muted", class "text-center"] ] [ Html.i [] [ text "No description :-(" ]]
                  else Grid.col [ Col.attrs [ class "text-muted" ] ] [ text description ]
                _ ->
                  Grid.col [] [ text "Loading"]
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
