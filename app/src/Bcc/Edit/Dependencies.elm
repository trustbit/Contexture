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
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Display as Display

import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Dict
import Url
import Http

import Bcc
import Domain

type alias DomainDependency =
  { id : Domain.DomainId
  , name : String }

type alias BoundedContextDependency =
  { id : Bcc.BoundedContextId
  , name: String
  , domain: DomainDependency }

type Dependency_
  = BoundedContext BoundedContextDependency
  | Domain DomainDependency

type alias DependencyEdit =
  { system: Bcc.System
  , relationship: Maybe Bcc.Relationship }

type alias DependenciesEdit =
  { consumer: DependencyEdit
  , supplier: DependencyEdit
  }

type alias EditModel =
  { edit: DependenciesEdit
  , dependencies: Bcc.Dependencies
  }

type alias Model =
  { edit: EditModel
  , availableDependencies : List Dependency_ }

initDependency =
  { system = "", relationship = Nothing }

initDependencies =
  { consumer = initDependency
  , supplier = initDependency
  }

init : Url.Url -> Bcc.Dependencies -> (Model, Cmd Msg)
init baseUrl dependencies =
  (
    { edit =
      { edit = initDependencies
      , dependencies = dependencies
      }
    , availableDependencies = []
    }
  , Cmd.batch [ loadBoundedContexts baseUrl, loadDomains baseUrl])

-- UPDATE

type FieldMsg
  = SetSystem Bcc.System
  | SetRelationship String

type ChangeTypeMsg
  = FieldEdit FieldMsg
  | DepdendencyChanged Bcc.DependencyAction

type EditMsg
  = Consumer ChangeTypeMsg
  | Supplier ChangeTypeMsg

type Msg
  = Edit EditMsg
  | BoundedContextsLoaded (Result Http.Error (List BoundedContextDependency))
  | DomainsLoaded (Result Http.Error (List DomainDependency))

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

updateEdit : EditMsg -> EditModel -> EditModel
updateEdit msg { edit, dependencies } =
  case msg of
    Consumer conMsg ->
      let
        (m, dependency) = updateDependency conMsg (edit.consumer, dependencies.consumers)
      in
        { edit = { edit | consumer = m }, dependencies = { dependencies | consumers = dependency } }
    Supplier supMsg ->
      let
        (m, dependency) = updateDependency supMsg (edit.supplier, dependencies.suppliers)
      in
        { edit = { edit | supplier = m }, dependencies = { dependencies | suppliers = dependency } }

update : Msg -> Model -> Model
update msg model =
  case msg of
    Edit edit ->
      let
        editModel = updateEdit edit model.edit
      in
        { model | edit = editModel }
    BoundedContextsLoaded (Ok contexts) ->
      { model | availableDependencies = List.append model.availableDependencies (contexts |> List.map BoundedContext) }
    DomainsLoaded (Ok domains) ->
      { model | availableDependencies = List.append model.availableDependencies (domains |> List.map Domain) }
    _ ->
      let
        _ = Debug.log ("Dependencies: " ++ (Debug.toString msg) ++ (Debug.toString model))
      in
        model



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
        [ Button.submitButton
          [ Button.secondary
          , Button.disabled (String.length model.system <= 0)
          ]
          [ text "+" ]
        ]
      ]
    ]

viewDependency : String -> DependencyEdit -> Bcc.DependencyMap -> Html ChangeTypeMsg
viewDependency title model addedDependencies =
  div []
    [ Html.h6
      [ class "text-center", Spacing.p2 ]
      [ Html.strong [] [ text title ] ]
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

viewEdit : EditModel -> Html EditMsg
viewEdit { edit, dependencies } =
  div []
    [ Html.span
      [ class "text-center"
      , Display.block
      , style "background-color" "lightGrey"
      , Spacing.p2
      ]
      [ text "Dependencies and Relationships" ]
    , Form.help [] [ text "To create loosely coupled systems it's essential to be wary of dependencies. In this section you should write the name of each dependency and a short explanation of why the dependency exists." ]
    , Grid.row []
      [ Grid.col []
        [ viewDependency "Message Suppliers" edit.supplier dependencies.suppliers |> Html.map Supplier ]
      , Grid.col []
        [ viewDependency "Message Consumers" edit.consumer dependencies.consumers |> Html.map Consumer ]
      ]
    ]

view : Model -> Html Msg
view model =
  viewEdit model.edit |> Html.map Edit

domainDecoder : Decoder DomainDependency
domainDecoder =
  Decode.succeed DomainDependency
    |> JP.custom Domain.idFieldDecoder
    |> JP.custom Domain.nameFieldDecoder

boundedContextDecoder : Decoder BoundedContextDependency
boundedContextDecoder =
  Decode.succeed BoundedContextDependency
    |> JP.custom Bcc.idFieldDecoder
    |> JP.custom Bcc.nameFieldDecoder
    |> JP.required "domain" domainDecoder

loadBoundedContexts: Url.Url -> Cmd Msg
loadBoundedContexts url =
  Http.get
  -- todo this is wrong
    { url = Url.toString { url | path = "/api/bccs?_expand=domain"}
    , expect = Http.expectJson BoundedContextsLoaded (Decode.list boundedContextDecoder)
    }

loadDomains: Url.Url -> Cmd Msg
loadDomains url =
  Http.get
  -- todo this is wrong
    { url = Url.toString { url | path = "/api/domains"}
    , expect = Http.expectJson DomainsLoaded (Decode.list domainDecoder)
    }