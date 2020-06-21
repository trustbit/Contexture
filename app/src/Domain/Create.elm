module Domain.Create exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Json.Encode as Encode

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events

import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Button as Button
import Url
import Http

import Domain
import Route

type alias Domain =
  { id: Domain.DomainId
  , name: String
  , vision: String
  }

type alias Model =
  { navKey : Nav.Key
  , baseUrl : String
  , newDomainName: String
  }

type Msg
  = SetDomainName String
  | CreateDomain
  | DomainCreated (Result Http.Error Domain)

init: String -> Nav.Key -> (Model, Cmd Msg)
init baseUrl key =
  ( { navKey = key
    , baseUrl = baseUrl
    , newDomainName = ""
    }
  , Cmd.none
  )


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    SetDomainName name ->
      ({ model | newDomainName = name}, Cmd.none)
    CreateDomain ->
      (model, createNewDomain model)
    DomainCreated (Ok item) ->
      (model, Route.pushUrl (Route.Domain item.id) model.navKey)
    DomainCreated (Err e) ->
      Debug.log ("Error on creating domain: " ++ Debug.toString e ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)


view : Model -> Html Msg
view model =
  Form.form [Html.Events.onSubmit CreateDomain]
    [ InputGroup.config
      (
        InputGroup.text
          [ Input.id "domainName"
          , Input.value model.newDomainName
          , Input.onInput SetDomainName
          , Input.placeholder "Name of the domain"
          ]
        )
      |> InputGroup.successors
        [ InputGroup.button
          [ Button.attrs
            [ Html.Attributes.type_ "submit"]
            , Button.primary
            , Button.disabled (model.newDomainName |> Domain.ifNameValid (\_ -> True) (\_ -> False))
            ]
          [ text "Create new domain"]
        ]
      |> InputGroup.view
    ]

createNewDomain : Model -> Cmd Msg
createNewDomain model =
  let
    body =
      Encode.object
      [ ("name", Encode.string model.newDomainName) ]
  in
    Http.post
      { url = model.baseUrl ++ "/domains"
      , body = Http.jsonBody body
      , expect = Http.expectJson DomainCreated Domain.domainDecoder
      }
