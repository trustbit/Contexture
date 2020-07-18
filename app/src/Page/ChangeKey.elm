module Page.ChangeKey exposing (
  Model, KeyError(..), init,
  Msg, update,
  view
  )

import Json.Decode as Decode

import Html exposing (Html, div, text)
import Html.Attributes exposing (..)

import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input

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
              |> Maybe.map (\key -> KeyFromDomain key (Domain.name domain))
            )
      in
        ( { model | existingKeys = List.append model.existingKeys domainKeys}
        , Cmd.none
        )

    BoundedContextKeysLoaded (Ok contexts) ->
      let
        contextKeys =
          contexts
          |> List.filterMap
            ( \context ->
              context
              |> BoundedContext.key
              |> Maybe.map (\key -> KeyFromBoundedContext key (BoundedContext.name context))
            )
      in
        ( { model | existingKeys = List.append model.existingKeys contextKeys}
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
                      KeyFromBoundedContext contextKey _ ->
                        (contextKey |> Key.toString |> String.toLower) == String.toLower newKey
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
    _ -> 
      (Debug.log "ChangeKey" model, Cmd.none)

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
        [ text "To help you identify this entity a readable key can be assigned, which must be unique among all domains and bounded contexts!" ]
      , Form.invalidFeedback
        []
        [ text <| "The key '" ++ model.enteredKey ++ "' is invalid because "  ++
          ( case model.value of
              Err (Problem Key.StartsWithNumber) -> "a key must not start with a number"
              Err (Problem Key.ContainsWhitespace) -> "a key should not contain any whitespaces"
              Err (Problem (Key.ContainsSpecialChars chars)) ->
                "a key should not contain the following charachters: " ++ String.join " " (chars |> Set.toList |> List.map String.fromChar)
              Err (NotUnique other) ->
                "the key is already in use by " ++
                ( case other of
                    KeyFromDomain key name -> "domain '" ++ name ++ " - " ++ (key |> Key.toString) ++ "'"
                    KeyFromBoundedContext key name -> "bounded context '" ++ name ++ " - " ++ (key |> Key.toString) ++ "'"
                )
              _ -> ""
          )
        ]
      ]

loadBoundedContexts: Api.Configuration -> Cmd Msg
loadBoundedContexts config =
  Http.get
    { url = Api.allBoundedContexts [] |> Api.url config |> Url.toString
    , expect = Http.expectJson BoundedContextKeysLoaded (Decode.list BoundedContext.modelDecoder)
    }

loadDomains: Api.Configuration -> Cmd Msg
loadDomains configuration =
  Http.get
    { url = Api.domains [] |> Api.url configuration |> Url.toString
    , expect = Http.expectJson DomainKeysLoaded (Decode.list Domain.domainDecoder)
    }