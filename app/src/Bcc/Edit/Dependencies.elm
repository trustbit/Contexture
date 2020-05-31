module Bcc.Edit.Dependencies exposing (Msg(..), DependencyFieldMsg(..), Model, AddingDependency, update, init,initDependency)

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

type Msg
  = Consumer DependencyFieldMsg
  | Supplier DependencyFieldMsg

updateAddingDependency : DependencyFieldMsg -> AddingDependency -> AddingDependency
updateAddingDependency msg model =
  case msg of
    SetSystem system ->
      { model | system = system }
    SetRelationship relationship ->
      { model | relationship = Bcc.relationshipParser relationship }

update : Msg -> Model -> Model
update msg model =
  case msg of
    Consumer conMsg ->
      { model | consumer = updateAddingDependency conMsg model.consumer }
    Supplier supMsg ->
      { model | supplier = updateAddingDependency supMsg model.supplier }

-- VIEW

