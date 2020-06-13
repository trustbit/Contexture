module Domain.Edit exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.Checkbox as Checkbox
import Bootstrap.Button as Button
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Text as Text
import Bootstrap.Utilities.Spacing as Spacing

import Json.Encode as Encode
import Json.Decode.Pipeline as JP
import Json.Decode as Decode

import RemoteData

import Url
import Http

import Route

import Domain
import Bcc.Index

-- MODEL

type alias EditableDomain = Domain.Domain

type alias Model =
  { key: Nav.Key
  , self: Url.Url
  , edit: RemoteData.WebData EditableDomain
  , contexts : Bcc.Index.Model
  }

init : Nav.Key -> Url.Url -> (Model, Cmd Msg)
init key url =
  let
    (contexts, contextCmd) = Bcc.Index.init url key
    model =
      { key = key
      , self = url
      , edit = RemoteData.Loading
      , contexts = contexts
      }
  in
    (
      model
    , Cmd.batch [loadDomain model, contextCmd |> Cmd.map BccMsg ]
    )

-- UPDATE

type EditingMsg
  = Field Domain.Msg

type Msg
  = Loaded (Result Http.Error Domain.Domain)
  | Editing EditingMsg
  | Save
  | Saved (Result Http.Error ())
  | Delete
  | Deleted (Result Http.Error ())
  | BccMsg Bcc.Index.Msg

updateEdit : EditingMsg -> RemoteData.WebData EditableDomain -> RemoteData.WebData EditableDomain
updateEdit msg model =
  case (msg, model) of
    (Field fieldMsg, RemoteData.Success domain) ->
      RemoteData.Success <| Domain.update fieldMsg domain
    _ -> model

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Editing editing ->
      ({ model | edit = updateEdit editing model.edit}, Cmd.none)
    Save ->
      case model.edit of
        RemoteData.Success domain ->
          (model, saveBCC model.self domain)
        _ ->
          Debug.log ("Cannot save unloaded model: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
          (model, Cmd.none)
    Saved (Ok _) ->
      (model, Cmd.none)
    Delete ->
      (model, deleteBCC model)
    Deleted (Ok _) ->
      (model, Route.pushUrl Route.Home model.key)
    Loaded (Ok m) ->
      ({ model | edit = RemoteData.Success m } , Cmd.none)
    Loaded (Err e) ->
      ({ model | edit = RemoteData.Failure e } , Cmd.none)
    BccMsg m ->
      let
        (bccModel, bccCmd) = Bcc.Index.update m model.contexts
      in
        ({ model | contexts = bccModel}, bccCmd |> Cmd.map BccMsg)
    _ ->
      Debug.log ("BCC: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)

-- VIEW

ifValid : (model -> Bool) -> (model -> result) -> (model -> result) -> model -> result
ifValid predicate trueRenderer falseRenderer model =
  if predicate model then
    trueRenderer model
  else
    falseRenderer model

ifNameValid =
  ifValid (\name -> String.length name <= 0)

viewLabel : String -> String -> Html msg
viewLabel labelId caption =
  Form.label [ for labelId] [ Html.h6 [] [ text caption ] ]

view : Model -> Html Msg
view model =
  let
    detail =
      case model.edit of
        RemoteData.Success domain ->
          ( List.concat
            [ [ Grid.row []
                [ Grid.col []
                    [ viewDomainCard domain ]
                ]
              ]
            , viewBccCard model.contexts
            ]
          )
        _ ->
          [ Grid.row [] 
            [ Grid.col []
              [ Html.p [] [ text "Loading details..." ] ]
            ]
          ]
  in
    Grid.container [] detail


viewDomainCard : EditableDomain -> Html Msg
viewDomainCard model =
  Card.config []
  |> Card.header []
    [ Html.h5 [] [ text "Manage your domain"] ]
  |> Card.block []
    [ Block.custom <| (viewDomain model |> Html.map Editing) ]
  |> Card.footer []
    [ Grid.row []
      [ Grid.col []
        [ Button.linkButton
          [ Button.attrs [ href (Route.routeToString Route.Home) ], Button.roleLink ]
          [ text "Back" ] ]
      , Grid.col [ Col.textAlign Text.alignLgRight ]
        [ Button.button
          [ Button.secondary
          , Button.onClick Delete
          , Button.attrs
            [ title ("Delete " ++ model.name)
            , Spacing.mr3
            ]
          ]
          [ text "Delete" ]
        , Button.submitButton
          [ Button.primary
          , Button.onClick Save
          , Button.disabled (model.name |> ifNameValid (\_ -> True) (\_ -> False))
          ]
          [ text "Save"]
        ]
      ]
    ]
  |> Card.view

viewBccCard : Bcc.Index.Model -> List(Html Msg)
viewBccCard model =
  Bcc.Index.view model
  |> List.map (Html.map BccMsg)


viewDomain : EditableDomain -> Html EditingMsg
viewDomain model =
  div []
    [ Form.group []
      [ viewLabel "name" "Name"
      , Input.text (
        List.concat
        [ [ Input.id "name", Input.value model.name, Input.onInput Domain.SetName ]
        , model.name |> ifNameValid (\_ -> [ Input.danger ]) (\_ -> [])
        ]
      )
      , Form.invalidFeedback [] [ text "A name for the Domain is required!" ]
      ]
    , Html.hr [] []
    , Form.group []
        [ viewLabel "vision" "Vision Statement"
        , Textarea.textarea
          [ Textarea.id "vision"
          , Textarea.value model.vision
          , Textarea.onInput Domain.SetVision
          , Textarea.rows 5
          ]
        , Form.help [] [ text "Summary of purpose"] ]
    ]
    |> Html.map Field

-- HTTP

loadDomain: Model -> Cmd Msg
loadDomain model =
  Http.get
    { url = Url.toString model.self
    , expect = Http.expectJson Loaded modelDecoder
    }

saveBCC: Url.Url -> EditableDomain -> Cmd Msg
saveBCC url model =
    Http.request
      { method = "PUT"
      , headers = []
      , url = Url.toString url
      , body = Http.jsonBody <| modelEncoder model
      , expect = Http.expectWhatever Saved
      , timeout = Nothing
      , tracker = Nothing
      }

deleteBCC: Model -> Cmd Msg
deleteBCC model =
    Http.request
      { method = "DELETE"
      , headers = []
      , url = Url.toString model.self
      , body = Http.emptyBody
      , expect = Http.expectWhatever Deleted
      , timeout = Nothing
      , tracker = Nothing
      }

modelDecoder : Decode.Decoder Domain.Domain
modelDecoder =
  Decode.succeed Domain.Domain
    |> JP.required "name" Decode.string
    |> JP.optional "vision" Decode.string ""

modelEncoder : Domain.Domain -> Encode.Value
modelEncoder model =
    Encode.object
        [ ("name", Encode.string model.name)
        , ("vision", Encode.string model.vision)
        ]