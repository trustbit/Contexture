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
import Bootstrap.Utilities.Border as Border

import Select as Autocomplete

import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Dict
import List
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

type CollaboratorReference
  = BoundedContext BoundedContextDependency
  | Domain DomainDependency

type alias DependencyEdit =
  { selectedCollaborator : Maybe CollaboratorReference
  , dependencySelectState : Autocomplete.State
  , relationship : Maybe Bcc.RelationshipPattern
  , existingDependencies : Bcc.DependencyMap }

type alias Model =
  { consumer: DependencyEdit
  , supplier: DependencyEdit
  , availableDependencies : List CollaboratorReference
  }

initDependency : Bcc.DependencyMap -> String -> DependencyEdit
initDependency existing id =
  { selectedCollaborator = Nothing
  , dependencySelectState = Autocomplete.newState id
  , relationship = Nothing
  , existingDependencies = existing }

initDependencies dependencies =
  { consumer = initDependency dependencies.consumers "consumer-select"
  , supplier = initDependency dependencies.suppliers "supplier-select"
  , availableDependencies = []
  }

init : Url.Url -> Bcc.Dependencies -> (Model, Cmd Msg)
init baseUrl dependencies =
  (
    initDependencies dependencies
  , Cmd.batch [ loadBoundedContexts baseUrl, loadDomains baseUrl])

-- UPDATE

type ChangeTypeMsg
  = SelectMsg (Autocomplete.Msg CollaboratorReference)
  | OnSelect (Maybe CollaboratorReference)
  | SetRelationship String
  | DepdendencyChanged Bcc.DependencyAction

type Msg
  = Consumer ChangeTypeMsg
  | Supplier ChangeTypeMsg
  | BoundedContextsLoaded (Result Http.Error (List BoundedContextDependency))
  | DomainsLoaded (Result Http.Error (List DomainDependency))


updateDependency : ChangeTypeMsg -> DependencyEdit -> (DependencyEdit, Cmd ChangeTypeMsg)
updateDependency msg model =
  case msg of
    SetRelationship relationship ->
      ({ model | relationship = Bcc.relationshipParser relationship }, Cmd.none)
    SelectMsg selMsg ->
      let
        ( updated, cmd ) =
          Autocomplete.update selectConfig selMsg model.dependencySelectState
      in
        ( { model | dependencySelectState = updated }, cmd )
    OnSelect item ->
      ({ model | selectedCollaborator = item }, Cmd.none)
    DepdendencyChanged change ->
      ( { model
        | selectedCollaborator = Nothing
        , relationship = Nothing
        , existingDependencies = Bcc.updateDependencyAction change model.existingDependencies
        }
      , Cmd.none
      )

update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
    Consumer conMsg ->
      let
        (consumerModel, dependencyCmd) = updateDependency conMsg model.consumer
      in
        ( { model | consumer = consumerModel }
        , dependencyCmd |> Cmd.map Consumer
        )
    Supplier supMsg ->
      let
        (supplierModel,dependencyCmd) = updateDependency supMsg model.supplier
      in
        ( { model | supplier = supplierModel }
        , dependencyCmd |> Cmd.map Supplier
        )
    BoundedContextsLoaded (Ok contexts) ->
      ( { model | availableDependencies = List.append model.availableDependencies (contexts |> List.map BoundedContext) }
      , Cmd.none
      )
    DomainsLoaded (Ok domains) ->
      ( { model | availableDependencies = List.append model.availableDependencies (domains |> List.map Domain) }
      , Cmd.none
      )
    _ ->
      let
        _ = Debug.log ("Dependencies: " ++ (Debug.toString msg) ++ (Debug.toString model))
      in
        (model, Cmd.none)

-- VIEW

toCollaborator : CollaboratorReference -> Bcc.Collaborator
toCollaborator dependency =
  case dependency of
    BoundedContext bc ->
      Bcc.BoundedContext bc.id
    Domain d ->
      Bcc.Domain d.id

filter : Int -> String -> List CollaboratorReference -> Maybe (List CollaboratorReference)
filter minChars query items =
  if String.length query < minChars then
      Nothing
  else
    let
      lowerQuery = query |> String.toLower
      containsLowerString text =
        text
        |> String.toLower
        |> String.contains lowerQuery
      searchable i =
        case i of
          BoundedContext bc ->
            containsLowerString bc.name || containsLowerString bc.domain.name
          Domain d ->
            containsLowerString d.name
      in
        items
        |> List.filter searchable
        |> Just

noOpLabel : CollaboratorReference -> String
noOpLabel item =
  case item of
    BoundedContext bc ->
      bc.domain.name ++ " - " ++ bc.name
    Domain d ->
      d.name

renderItem : CollaboratorReference -> Html msg
renderItem item =
  let
    content =
      case item of
      BoundedContext bc ->
        [ Html.h6 [ class "text-muted" ] [ text bc.domain.name ]
        , Html.span [] [ text bc.name ] ]
      Domain d ->
        [ Html.span [] [ text d.name ] ]
  in
    Html.span [] content

selectConfig : Autocomplete.Config ChangeTypeMsg CollaboratorReference
selectConfig =
    Autocomplete.newConfig
        { onSelect = OnSelect
        , toLabel = noOpLabel
        , filter = filter 2
        }
        |> Autocomplete.withCutoff 12
        |> Autocomplete.withInputClass "text-control border rounded form-control-lg"
        |> Autocomplete.withInputWrapperClass ""
        |> Autocomplete.withItemClass " border p-2 "
        |> Autocomplete.withMenuClass "bg-light"
        |> Autocomplete.withNotFound "No matches"
        |> Autocomplete.withNotFoundClass "text-danger"
        |> Autocomplete.withHighlightedItemClass "bg-white"
        |> Autocomplete.withPrompt "Search for a Dependency"
        |> Autocomplete.withItemHtml renderItem

translateRelationship : Bcc.RelationshipPattern -> String
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

viewAddedDepencency : List CollaboratorReference -> Bcc.Dependency -> Html ChangeTypeMsg
viewAddedDepencency items (collaborator, relationship) =
  let
    collaboratorCaption =
      items
      |> List.filter (\r -> toCollaborator r == collaborator )
      |> List.head
      |> Maybe.map renderItem
      |> Maybe.withDefault (text "Unknown name")
  in
  Grid.row [Row.attrs [ Border.top, Spacing.mb2, Spacing.pt1 ] ]
    [ Grid.col [] [ collaboratorCaption ]
    , Grid.col [] [text (Maybe.withDefault "not specified" (relationship |> Maybe.map translateRelationship))]
    , Grid.col [ Col.xs2 ]
      [ Button.button
        [ Button.secondary
        , Button.onClick (
            (collaborator, relationship)
            |> Bcc.Remove |> DepdendencyChanged
          )
        ]
        [ text "x" ]
      ]
    ]

onSubmitMaybe : Maybe msg -> Html.Attribute msg
onSubmitMaybe maybeMsg =
  -- https://thoughtbot.com/blog/advanced-dom-event-handlers-in-elm
  case maybeMsg of
    Just msg ->
      Html.Events.preventDefaultOn "submit" (Decode.map (\m -> ( m, True )) (Decode.succeed msg))
    Nothing ->
      Html.Events.preventDefaultOn "submit" (Decode.map (\m -> ( m, True )) (Decode.fail "No message to submit"))


viewAddDependency : List CollaboratorReference -> DependencyEdit -> Html ChangeTypeMsg
viewAddDependency dependencies model =
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

    selectedItem =
      case model.selectedCollaborator of
        Just s -> [ s ]
        Nothing -> []

    -- TODO: this is probably very unefficient
    existingDependencies =
      model.existingDependencies
      |> Bcc.dependencyList
      |> List.map Tuple.first

    relevantDependencies =
      dependencies
      |> List.filter (\d -> existingDependencies |> List.any (\existing -> (d |> toCollaborator)  == existing ) |> not )

    autocompleteSelect =
      Autocomplete.view
        selectConfig
        model.dependencySelectState
        relevantDependencies
        selectedItem
  in
  Form.form
    [ onSubmitMaybe
      ( model.selectedCollaborator
        |> Maybe.map toCollaborator
        |> Maybe.map (\s -> (s, model.relationship))
        |> Maybe.map (Bcc.Add >> DepdendencyChanged)
      )
    ]
    [ Grid.row []
      [ Grid.col []
        [ autocompleteSelect |> Html.map SelectMsg ]
      , Grid.col []
        [ Select.select [ Select.onChange SetRelationship ]
            (List.append [ Select.item [ selected (model.relationship == Nothing), value "" ] [text "unknown"] ] items)
        ]
      , Grid.col [ Col.xs2 ]
        [ Button.submitButton
          [ Button.secondary
          , Button.disabled
            ( model.selectedCollaborator
              |> Maybe.map (\_ -> False)
              |> Maybe.withDefault True
            )
          ]
          [ text "+" ]
        ]
      ]
    ]

viewDependency : List CollaboratorReference -> String -> DependencyEdit -> Html ChangeTypeMsg
viewDependency items title model =
  div []
    [ Html.h6
      [ class "text-center", Spacing.p2 ]
      [ Html.strong [] [ text title ] ]
    , Grid.row []
      [ Grid.col [] [ Html.h6 [] [ text "Name"] ]
      , Grid.col [] [ Html.h6 [] [ text "Relationship Pattern"] ]
      , Grid.col [Col.xs2] []
      ]
    , div []
      (model.existingDependencies
      |> Bcc.dependencyList
      |> List.map (viewAddedDepencency items))
    , viewAddDependency items model
    ]

view : Model -> Html Msg
view model =
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
        [ viewDependency model.availableDependencies "Message Suppliers" model.supplier |> Html.map Supplier ]
      , Grid.col []
        [ viewDependency model.availableDependencies "Message Consumers" model.consumer |> Html.map Consumer ]
      ]
    ]

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