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
import Route

type alias Domain = Domain.Domain

type alias Model =
  { navKey : Nav.Key
  , baseUrl : Url.Url
  , newDomainName: String
  }

type Msg
  = SetDomainName String
  | CreateDomain
  | DomainCreated (Result Http.Error Domain)

init: Url.Url -> Nav.Key -> (Model, Cmd Msg)
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
      (model, newDomain model.baseUrl model.newDomainName DomainCreated)
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
            , Button.disabled (model.newDomainName |> Domain.isNameValid |> not)
            ]
          [ text "Create new domain"]
        ]
      |> InputGroup.view
    ]
