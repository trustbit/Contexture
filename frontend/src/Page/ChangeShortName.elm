module Page.ChangeShortName exposing (
  Model, ShortNameError(..), init,
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
import ShortName

type ExistingShortName
  = ShortNameFromDomain ShortName.ShortName String
  | ShortNameFromBoundedContext ShortName.ShortName String

type ShortNameError
  = Problem ShortName.Problem
  | NotUnique ExistingShortName

type alias Model =
  { existingShortNames : List ExistingShortName
  , enteredShortName : String
  , initialShortName : Maybe ShortName.ShortName
  , value : Result ShortNameError (Maybe ShortName.ShortName)
  }

init : Api.Configuration -> Maybe ShortName.ShortName -> (Model, Cmd Msg)
init config maybeAShortName =
  let
    model =
      maybeAShortName
      |> Maybe.map (\shortName ->
        { existingShortNames = []
        , enteredShortName = shortName |> ShortName.toString
        , value = Ok (Just shortName)
        , initialShortName = Just shortName
        }
      )
      |> Maybe.withDefault
        { existingShortNames = []
        , enteredShortName = ""
        , value = Ok Nothing
        , initialShortName = Nothing
        }
  in
    ( model
    , Cmd.batch
      [ loadDomains config
      , loadBoundedContexts config
      ]
    )

type Msg
  = DomainShortNamesLoaded (Api.ApiResponse (List Domain))
  | BoundedContextShortNamesLoaded (Api.ApiResponse (List BoundedContext))
  | UpdateShortName String


shortNamesEqual existing shortName =
  case existing of
    ShortNameFromDomain k _ ->
      k == shortName
    ShortNameFromBoundedContext k _ ->
      k == shortName

update : Msg -> Model ->  ( Model, Cmd Msg )
update msg model =
  case msg of
    DomainShortNamesLoaded (Ok domains) ->
      let
        domainShortNames =
          domains
          |> List.filterMap
            ( \domain ->
              domain
              |> Domain.shortName
              |> Maybe.map (\shortName -> ShortNameFromDomain shortName (Domain.name domain))
            )
      in
        ( { model | existingShortNames = List.append model.existingShortNames domainShortNames}
        , Cmd.none
        )

    BoundedContextShortNamesLoaded (Ok contexts) ->
      let
        contextShortNames =
          contexts
          |> List.filterMap
            ( \context ->
              context
              |> BoundedContext.shortName
              |> Maybe.map (\shortName -> ShortNameFromBoundedContext shortName (BoundedContext.name context))
            )
      in
        ( { model | existingShortNames = List.append model.existingShortNames contextShortNames}
        , Cmd.none
        )

    UpdateShortName newShortName ->
      let
        value =
          case ShortName.fromString newShortName of
            Ok k ->
              let
                existingShortName =
                  model.existingShortNames
                  |> List.filter (\shortName ->
                    case shortName of
                      ShortNameFromDomain domainShortName _ ->
                        (domainShortName |> ShortName.toString |> String.toLower) == String.toLower newShortName
                      ShortNameFromBoundedContext contextShortName _ ->
                        (contextShortName |> ShortName.toString |> String.toLower) == String.toLower newShortName
                  )
                  |> List.head
              in
                case (existingShortName, model.initialShortName) of
                  (Just existing, Just initial) ->
                    if shortNamesEqual existing initial
                    then Ok (Just k)
                    else Err (NotUnique existing)
                  (Just existing, Nothing) ->
                    Err (NotUnique existing)
                  _ ->
                    Ok (Just k)
            Err (ShortName.Empty) -> Ok Nothing
            Err e -> Err (Problem e)

      in
        ( { model | enteredShortName = newShortName, value = value }
        , Cmd.none
        )
    _ ->
      (Debug.log "ChangeShortName" model, Cmd.none)

view : Model -> Html Msg
view model =
  let
    shortNameIsValid =
      case model.value of
      Ok _ -> True
      Err _ -> False
  in
    div []
      [ Input.text
        [ Input.id "shortName"
        , Input.value model.enteredShortName
        , Input.onInput UpdateShortName
        , Input.placeholder "Choose a short name"
        , if shortNameIsValid
          then Input.success
          else Input.danger
        ]
      , Form.help []
        [ text "To help you identify this entity a readable short name can be assigned, which must be unique among all domains and bounded contexts!" ]
      , Form.invalidFeedback
        []
        [ text <| "The short name '" ++ model.enteredShortName ++ "' is invalid because "  ++
          ( case model.value of
              Err (Problem ShortName.StartsWithNumber) -> "a short name must not start with a number"
              Err (Problem ShortName.ContainsWhitespace) -> "a short name should not contain any whitespaces"
              Err (Problem (ShortName.ContainsSpecialChars chars)) ->
                "a short name should not contain the following charachters: " ++ String.join " " (chars |> Set.toList |> List.map String.fromChar)
              Err (NotUnique other) ->
                "the short name is already in use by " ++
                ( case other of
                    ShortNameFromDomain shortName name -> "domain '" ++ name ++ " - " ++ (shortName |> ShortName.toString) ++ "'"
                    ShortNameFromBoundedContext shortName name -> "bounded context '" ++ name ++ " - " ++ (shortName |> ShortName.toString) ++ "'"
                )
              _ -> ""
          )
        ]
      ]

loadBoundedContexts: Api.Configuration -> Cmd Msg
loadBoundedContexts config =
  Http.get
    { url = Api.allBoundedContexts [] |> Api.url config 
    , expect = Http.expectJson BoundedContextShortNamesLoaded (Decode.list BoundedContext.modelDecoder)
    }

loadDomains: Api.Configuration -> Cmd Msg
loadDomains configuration =
  Http.get
    { url = Api.domains [] |> Api.url configuration 
    , expect = Http.expectJson DomainShortNamesLoaded (Decode.list Domain.domainDecoder)
    }