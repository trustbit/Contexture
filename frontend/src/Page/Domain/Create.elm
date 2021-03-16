module Page.Domain.Create exposing (Msg, Model, update, view, init)

import Browser.Navigation as Nav

import Html exposing (Html, button, text)
import Html.Attributes
import Html.Events

import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Button as Button
import Url
import Http

import Domain exposing (newDomain)
import Api
import Route

type alias Domain = Domain.Domain

type alias Model =
  { navKey : Nav.Key
  , baseUrl : Api.Configuration
  , relation : Domain.DomainRelation
  , newDomainName: String
  }

type Msg
  = SetDomainName String
  | CreateDomain (Result Domain.Problem Domain.Name)
  | DomainCreated (Result Http.Error Domain)

init: Api.Configuration -> Nav.Key -> Domain.DomainRelation ->  (Model, Cmd Msg)
init baseUrl key relation =
  ( { navKey = key
    , baseUrl = baseUrl
    , relation = relation
    , newDomainName = ""
    }
  , Cmd.none
  )


update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    SetDomainName name ->
      ({ model | newDomainName = name}, Cmd.none)
    CreateDomain (Ok name) ->
      (model, newDomain model.baseUrl model.relation name DomainCreated)
    CreateDomain (Err name) ->
      (model, Cmd.none)
    DomainCreated (Ok item) ->
      (model, Route.pushUrl (item |> Domain.id |> Route.Domain) model.navKey)
    DomainCreated (Err e) ->
      Debug.log ("Error on creating domain: " ++ Debug.toString e ++ Debug.toString msg ++ " " ++ Debug.toString model)
      (model, Cmd.none)


view : Model -> Html Msg
view model =
  Form.form [Html.Events.onSubmit <| CreateDomain (Domain.asName model.newDomainName)]
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
            , Button.disabled (model.newDomainName |> Domain.isNameValid |> not)
            ]
          [ text "Create new domain"]
        ]
      |> InputGroup.view
    ]
