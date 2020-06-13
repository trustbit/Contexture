module Bcc.Index exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Json.Encode as Encode
import Json.Decode.Pipeline as JP
import Json.Decode as Decode exposing (Decoder)

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Form.Input as Input
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Button as Button
import Bootstrap.ListGroup as ListGroup
import Bootstrap.Card as Card
import Bootstrap.Card.Block as Block
import Bootstrap.Utilities.Spacing as Spacing

import Url
import Http
import RemoteData

import Bcc
import Route

-- MODEL

type alias BccItem =
  { id: Bcc.BoundedContextId
  , name: String
  , description: String }

type alias Model =
  { navKey : Nav.Key
  , bccName : String
  , baseUrl : Url.Url
  , bccs: RemoteData.WebData (List BccItem) }

init: Url.Url -> Nav.Key -> (Model, Cmd Msg)
init baseUrl key =
  ( { navKey = key
    , bccs = RemoteData.Loading
    , baseUrl = baseUrl
    , bccName = "" }
  , loadAll baseUrl )

-- UPDATE

type Msg
  = Loaded (Result Http.Error (List BccItem))
  | SetName String
  | CreateBcc
  | Created (Result Http.Error BccItem)

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Loaded (Ok items) ->
      ({ model | bccs = RemoteData.Success items }, Cmd.none)
    Loaded (Err e) ->
      ({ model | bccs = RemoteData.Failure e }, Cmd.none)
    SetName name ->
      ({ model | bccName = name}, Cmd.none)
    CreateBcc ->
      (model, createNewBcc model)
    Created (Ok item) ->
        (model, Route.pushUrl (Route.Bcc item.id) model.navKey)
    _ ->
        Debug.log ("Overview: " ++ Debug.toString msg ++ " " ++ Debug.toString model)
        (model, Cmd.none)

-- VIEW

createWithName : String -> Html Msg
createWithName name =
  Form.form [Html.Events.onSubmit CreateBcc]
    [ InputGroup.config (
        InputGroup.text
          [ Input.id name
          , Input.value name
          , Input.onInput SetName
          , Input.placeholder "Name of the new Bounded Context"
          ]
        )
      |> InputGroup.successors
        [ InputGroup.button
        [ Button.attrs
            [ Html.Attributes.type_ "submit"]
            , Button.primary
            , Button.disabled (name |> Bcc.ifNameValid (\_ -> True) (\_ -> False))
            ]
        [ text "Create new Bounded Context"]
        ]
      |> InputGroup.view
    ]

viewExisting : List BccItem  -> Html Msg
viewExisting items =
  if List.isEmpty items then
    Html.p
      [ class "lead" ]
      [ text "No exsisting Bounded Contexts found - do you want to create one?" ]
  else
    let
      renderCard item =
        Card.config []
          |> Card.block []
            ( List.concat
                [
                  [ Block.titleH4 [] [ text item.name ]]
                  , if String.length item.description > 0
                      then [ Block.text [] [ text item.description  ] ]
                      else []
                ]
            )
          |> Card.block []
            [ Block.link
                [ href (Route.routeToString (Route.Bcc item.id)), class "stretched-link" ]
                [text "View Bounded Context"]
            ]
    in
        Card.deck (items |> List.map renderCard)

view : Model -> List (Html Msg)
view model =
  case model.bccs of
    RemoteData.Success contexts ->
      [ Grid.row [ Row.attrs [ Spacing.pt3 ] ]
        [ Grid.col [] [viewExisting contexts] ]
        , Grid.row [ Row.attrs [Spacing.mt3]]
        [ Grid.col [] [ createWithName model.bccName ] ]
      ]
    _ -> [ text "Loading your contexts"]
          

-- helpers

loadAll: Url.Url -> Cmd Msg
loadAll baseUrl =
  Http.get
    { url = { baseUrl | path = baseUrl.path ++ "/bccs" } |> Url.toString
    , expect = Http.expectJson Loaded bccItemsDecoder
    }

createNewBcc : Model -> Cmd Msg
createNewBcc model =
  let
      body =
          Encode.object
          [ ("name", Encode.string model.bccName) ]
      baseUrl = model.baseUrl
  in
      Http.post
      { url = { baseUrl | path = baseUrl.path ++ "/bccs" } |> Url.toString
      , body = Http.jsonBody body
      , expect = Http.expectJson Created bccItemDecoder
      }

bccItemsDecoder: Decoder (List BccItem)
bccItemsDecoder =
  Decode.list bccItemDecoder


bccItemDecoder: Decoder BccItem
bccItemDecoder =
  Decode.succeed BccItem
    |> JP.required "id" Bcc.idDecoder
    |> JP.required "name" Decode.string
    |> JP.optional "description" Decode.string ""
