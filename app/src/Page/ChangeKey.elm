module Page.ChangeKey exposing (..)

import Json.Decode as Decode

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick, onSubmit)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Button as Button
import Bootstrap.Utilities.Spacing as Spacing

import Http
import Url

import Set


import Api

import Domain exposing (Domain)
import BoundedContext exposing (BoundedContext)
import Key

type ExistingKey
  = KeyFromDomain Key.Key String
  | KeyFromBoundedContext Key.Key String

type KeyError
  = Problem Key.Problem
  | NotUnique ExistingKey

type alias Model =
  { existingKeys : List ExistingKey
  , enteredKey : String
  , value : Result KeyError (Maybe Key.Key)
  }

init : Api.Configuration -> Maybe Key.Key -> (Model, Cmd Msg)
init config maybeAKey =
  let
    model =
      maybeAKey
      |> Maybe.map (\key -> { existingKeys = [], enteredKey = key |> Key.toString, value = Ok (Just key) } )
      |> Maybe.withDefault { existingKeys = [], enteredKey = "", value = Ok Nothing }
  in
    ( model
    , Cmd.batch
      [ loadDomains config
      , loadBoundedContexts config
      ] 
    )

type Msg
  = DomainKeysLoaded (Api.ApiResponse (List Domain))
  | BoundedContextKeysLoaded (Api.ApiResponse (List BoundedContext))
  | UpdateKey String


update : Msg -> Model ->  ( Model, Cmd Msg )
update msg model =
  case msg of
    DomainKeysLoaded (Ok domains) ->
      let
        domainKeys =
          domains
          |> List.filterMap
            ( \domain ->
              domain
              |> Domain.key
              |> Maybe.map (\key ->KeyFromDomain key (Domain.name domain))
            )
      in
        ( { model | existingKeys = List.append model.existingKeys domainKeys}
        , Cmd.none
        )

    UpdateKey newKey ->
      let
        value =
          case Key.fromString newKey of
            Ok k ->
              let
                existingKey =
                  model.existingKeys
                  |> List.filter (\key ->
                    case key of
                      KeyFromDomain domainKey _ ->
                        (domainKey |> Key.toString |> String.toLower) == String.toLower newKey
                      _ -> False
                  )
                  |> List.head
              in
                case existingKey of
                  Just existing -> Err (NotUnique existing)
                  Nothing -> Ok (Just k)
            Err (Key.Empty) -> Ok Nothing
            Err e -> Err (Problem e)

      in
        ( { model | enteredKey = newKey, value = value }
        , Cmd.none
        )
    _ -> (model,Cmd.none)

view : Model -> Html Msg
view model =
  let
    keyIsValid =
      case model.value of
      Ok _ -> True
      Err _ -> False
  in
    div [] 
      [ Input.text
        [ Input.id "key"
        , Input.value model.enteredKey
        , Input.onInput UpdateKey
        , Input.placeholder "Choose a key"
        , if keyIsValid
          then Input.success
          else Input.danger
        ]
      , Form.help []
        [ text "You can hoose a unique, readable key among all domains and bounded contexts, to help you identify this domain!" ]
      , Form.invalidFeedback
        []
        [ text <| "The key '" ++ model.enteredKey ++ "' is invalid: "  ++
          ( case model.value of
              Err (Problem Key.StartsWithNumber) -> "it must not start with a number"
              Err (Problem Key.ContainsWhitespace) -> "it should not contain any whitespaces"
              Err (Problem (Key.ContainsSpecialChars chars)) ->
                "it should not contain the following charachters: " ++ String.join " " (chars |> Set.toList |> List.map String.fromChar)
              Err (NotUnique other) ->
                "it is already in use by " ++
                ( case other of
                    KeyFromDomain key name -> "domain '" ++ name ++ "' - " ++ (key |> Key.toString)
                    _ -> ""
                )
              _ -> ""
          )
        ]
      ]
  



loadBoundedContexts: Api.Configuration -> Cmd Msg
loadBoundedContexts configuration =
  Http.get
    { url = Api.allBoundedContexts |> Api.url configuration |> Url.toString
    , expect = Http.expectJson BoundedContextKeysLoaded (Decode.list BoundedContext.modelDecoder)
    }

loadDomains: Api.Configuration -> Cmd Msg
loadDomains configuration =
  Http.get
    { url = Api.domains [] |> Api.url configuration |> Url.toString
    , expect = Http.expectJson DomainKeysLoaded (Decode.list Domain.domainDecoder)
    }