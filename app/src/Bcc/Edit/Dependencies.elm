module Bcc.Edit.Dependencies exposing (Msg(..), DependenciesEdit, update, initDependencies, view)

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Select as Select
import Bootstrap.Button as Button

import Dict

import Bcc

type alias DependencyEdit =
  { system: Bcc.System
  , relationship: Maybe Bcc.Relationship }

type alias DependenciesEdit =
  { consumer: DependencyEdit
  , supplier: DependencyEdit
  }

type alias Model = (DependenciesEdit, Bcc.Dependencies)

initDependency = 
  { system = "", relationship = Nothing } 

initDependencies = 
  { consumer = initDependency
  , supplier = initDependency
  }

-- UPDATE

type FieldMsg
  = SetSystem Bcc.System
  | SetRelationship String

type ChangeTypeMsg
  = FieldEdit FieldMsg
  | DepdendencyChanged Bcc.DependencyAction

type Msg
  = Consumer ChangeTypeMsg
  | Supplier ChangeTypeMsg

updateAddingDependency : FieldMsg -> DependencyEdit -> DependencyEdit
updateAddingDependency msg model =
  case msg of
    SetSystem system ->
      { model | system = system }
    SetRelationship relationship ->
      { model | relationship = Bcc.relationshipParser relationship }

updateDependency : ChangeTypeMsg -> (DependencyEdit, Bcc.DependencyMap) -> (DependencyEdit, Bcc.DependencyMap) 
updateDependency msg (model,depdendencies) =
  case msg of
    DepdendencyChanged change ->
        (initDependency , Bcc.updateDependencyAction change depdendencies)
    FieldEdit depMsg ->
      ( updateAddingDependency depMsg model, depdendencies)

update : Msg -> Model -> Model
update msg (model, dependencies) =
  case msg of
    Consumer conMsg ->
      let
        (m, dependency) = updateDependency conMsg (model.consumer, dependencies.consumers)
      in
        ( { model | consumer = m }, { dependencies | consumers = dependency })
    Supplier supMsg ->
      let
        (m, dependency) = updateDependency supMsg (model.supplier, dependencies.suppliers)
      in
        ( { model | supplier = m }, { dependencies | suppliers = dependency })

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

viewAddedDepencency :Bcc.Dependency -> Html ChangeTypeMsg
viewAddedDepencency (system, relationship) =
  Grid.row []
    [ Grid.col [] [text system]
    , Grid.col [] [text (Maybe.withDefault "not specified" (relationship |> Maybe.map translateRelationship))]
    , Grid.col [ Col.xs2 ] 
      [ Button.button 
        [ Button.danger
        , Button.onClick (
            (system, relationship)
            |> Bcc.Remove |> DepdendencyChanged
          ) 
        ]
        [ text "x" ]
      ]
    ]

viewAddDependency : DependencyEdit -> Html ChangeTypeMsg
viewAddDependency model =
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
        |> Bcc.Add >> DepdendencyChanged
      ) 
    ] 
    [ Grid.row []
      [ Grid.col [] 
        [ Input.text
          [ Input.value model.system
          , Input.onInput (SetSystem >> FieldEdit)
          ]
        ] 
      , Grid.col [] 
        [ Select.select [ Select.onChange (SetRelationship >> FieldEdit) ]
            (List.append [ Select.item [ selected (model.relationship == Nothing), value "" ] [text "unknown"] ] items)
        ]
      , Grid.col [ Col.xs2 ]
        [ Button.submitButton [ Button.secondary ] [ text "+" ]
        ]
      ]
    ]

viewDependency : String -> DependencyEdit -> Bcc.DependencyMap -> Html ChangeTypeMsg
viewDependency title model addedDependencies =
  div [] 
    [ Html.h6 [ class "text-center" ] [ text title ]
    , Grid.row []
      [ Grid.col [] [ Html.h6 [] [ text "Name"] ]
      , Grid.col [] [ Html.h6 [] [ text "Relationship"] ]
      , Grid.col [Col.xs2] []
      ]
    , div [] 
      (addedDependencies
      |> Dict.toList
      |> List.map viewAddedDepencency)
    , viewAddDependency model
    ]

view : Model -> Html Msg
view (model, dependencies) =
  div []
    [ Html.h5 [ class "text-center" ] [ text "Dependencies and Relationships" ]
    , Grid.row []
      [ Grid.col []
        [ viewDependency "Message Suppliers" model.supplier dependencies.suppliers |> Html.map Supplier ]
      , Grid.col []
        [ viewDependency "Message Consumers" model.consumer dependencies.consumers |> Html.map Consumer ]
      ]
    ]