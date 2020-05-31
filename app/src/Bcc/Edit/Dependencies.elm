module Bcc.Edit.Dependencies exposing (Msg(..), DependenciesFieldMsg(..), DependencyFieldMsg(..), Model, AddingDependency, update, init,initDependency, viewDepencency, viewAddDependency)

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Select as Select
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.InputGroup as InputGroup
import Bootstrap.Button as Button
import Bootstrap.ListGroup as ListGroup


import Bcc

type alias AddingDependency =
  { system: Bcc.System
  , relationship: Maybe Bcc.Relationship }

type alias Model =
  { consumer: AddingDependency
  , supplier: AddingDependency
  }

initDependency = 
  { system = "", relationship = Nothing } 

init = 
  { consumer = initDependency
  , supplier = initDependency
  }

-- UPDATE

type DependencyFieldMsg
  = SetSystem Bcc.System
  | SetRelationship String

type DependenciesFieldMsg
  = Consumer DependencyFieldMsg
  | Supplier DependencyFieldMsg

type Msg
  = FieldEdit DependenciesFieldMsg
  | DepdendencyChanged Bcc.DependenciesMsg

updateAddingDependency : DependencyFieldMsg -> AddingDependency -> AddingDependency
updateAddingDependency msg model =
  case msg of
    SetSystem system ->
      { model | system = system }
    SetRelationship relationship ->
      { model | relationship = Bcc.relationshipParser relationship }

update : DependenciesFieldMsg -> Model -> Model
update msg model =
  case msg of
    Consumer conMsg ->
      { model | consumer = updateAddingDependency conMsg model.consumer }
    Supplier supMsg ->
      { model | supplier = updateAddingDependency supMsg model.supplier }

-- VIEW

translateRelationship : Bcc.Relationship -> String
translateRelationship relationship =
  case relationship of
    Bcc.AntiCorruptionLayer -> "Anti Corruption Layer"
    Bcc.OpenHostService -> "Open Host Service"
    Bcc.PublishedLanguage -> "Published Language"
    Bcc.SharedKernel ->"Shared Kernel"
    Bcc.UpstreamDownstream -> "Upstream/Downstream"
    Bcc.Conformist -> "Conformist"
    Bcc.Octopus -> "Octopus"
    Bcc.Partnership -> "Partnership"
    Bcc.CustomerSupplier -> "Customer/Supplier"

viewDepencency : (Bcc.Action Bcc.Dependency -> Bcc.DependenciesMsg) -> Bcc.Dependency -> Html Bcc.Msg
viewDepencency removeCmd (system, relationship) =
  Grid.row []
    [ Grid.col [] [text system]
    , Grid.col [] [text (Maybe.withDefault "not specified" (relationship |> Maybe.map translateRelationship))]
    , Grid.col [ Col.xs2 ] 
      [ Button.button 
        [ Button.danger
        , Button.onClick (
            (system, relationship)
            |> Bcc.Remove |> removeCmd |> Bcc.ChangeDependencies
          ) 
        ]
        [ text "x" ]
      ]
    ]

viewAddDependency : (DependencyFieldMsg -> DependenciesFieldMsg) -> (Bcc.Action Bcc.Dependency -> Bcc.DependenciesMsg) -> AddingDependency -> Html Msg
viewAddDependency editCmd addCmd model =
  let
    items =
      [ Bcc.AntiCorruptionLayer
      , Bcc.OpenHostService
      , Bcc.PublishedLanguage
      , Bcc.SharedKernel
      , Bcc.UpstreamDownstream
      , Bcc.Conformist
      , Bcc.Octopus
      , Bcc.Partnership
      , Bcc.CustomerSupplier
      ]
        |> List.map (\r -> (r, translateRelationship r))
        |> List.map (\(v,t) -> Select.item [value (Bcc.relationshipToString v)] [ text t])
  in
  Form.form 
    [ Html.Events.onSubmit
      (
        (model.system, model.relationship)
        |> Bcc.Add >> addCmd >> DepdendencyChanged
      ) 
    ] 
    [ Grid.row []
      [ Grid.col [] 
        [ Input.text
          [ Input.value model.system
          , Input.onInput (SetSystem >> editCmd >> FieldEdit)
          ]
        ] 
      , Grid.col [] 
        [ Select.select [ Select.onChange (SetRelationship >> editCmd >> FieldEdit) ]
            (List.append [ Select.item [ selected (model.relationship == Nothing), value "" ] [text "unknown"] ] items)
        ]
      , Grid.col [ Col.xs2 ]
        [ Button.submitButton [ Button.secondary ] [ text "+" ]
        ]
      ]
    ]