module Bcc.Edit.Dependencies exposing (Msg(..), Model, update, init, view)

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

type alias DependencyType = DependencyFieldMsg -> DependenciesFieldMsg

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

updateField : DependenciesFieldMsg -> Model -> Model
updateField msg model =
  case msg of
    Consumer conMsg ->
      { model | consumer = updateAddingDependency conMsg model.consumer }
    Supplier supMsg ->
      { model | supplier = updateAddingDependency supMsg model.supplier }

update : Msg -> (Model, Bcc.BoundedContextCanvas) -> (Model, Bcc.BoundedContextCanvas)
update msg (model, canvas) =
  case msg of
    DepdendencyChanged change ->
      let 
        addingDependencies =
          case change of
            Bcc.Supplier _ ->
              { model | supplier = initDependency }
            Bcc.Consumer _ ->
              { model | consumer = initDependency }
      in
        (addingDependencies, Bcc.update (Bcc.ChangeDependencies change) canvas)
    FieldEdit depMsg ->
      ( updateField depMsg model, canvas)


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

viewAddedDepencency : (Bcc.Action Bcc.Dependency -> Bcc.DependenciesMsg) -> Bcc.Dependency -> Html Msg
viewAddedDepencency removeCmd (system, relationship) =
  Grid.row []
    [ Grid.col [] [text system]
    , Grid.col [] [text (Maybe.withDefault "not specified" (relationship |> Maybe.map translateRelationship))]
    , Grid.col [ Col.xs2 ] 
      [ Button.button 
        [ Button.danger
        , Button.onClick (
            (system, relationship)
            |> Bcc.Remove |> removeCmd |> DepdendencyChanged
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

viewDependency : String -> AddingDependency -> DependencyType -> Bcc.DependencyMap -> Bcc.DependencyType -> List (Html Msg)
viewDependency title model updatedField addedDependencies updatedDependency =
    [ Html.h6 [ class "text-center" ] [ text title ]
    , Grid.row []
      [ Grid.col [] [ Html.h6 [] [ text "Name"] ]
      , Grid.col [] [ Html.h6 [] [ text "Relationship"] ]
      , Grid.col [Col.xs2] []
      ]
    , div [] 
      (addedDependencies
      |> Dict.toList
      |> List.map (viewAddedDepencency updatedDependency))
    , viewAddDependency updatedField updatedDependency model
    ]

view : Model -> Bcc.Dependencies -> Html Msg
view model dependencies =
  div []
    [ Html.h5 [ class "text-center" ] [ text "Dependencies and Relationships" ]
    , Grid.row []
      [ Grid.col []
        (viewDependency "Message Suppliers" model.supplier Supplier dependencies.suppliers Bcc.Supplier)
      , Grid.col []
        (viewDependency "Message Consumers" model.consumer Consumer dependencies.consumers Bcc.Consumer)
      ]
    ]