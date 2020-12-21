module Page.Bcc.Edit.Dependencies exposing (
  Msg(..), Model,
  update, init, view,
  asDependencies)

import Html exposing (Html, button, div, text)
import Html.Attributes exposing (..)
import Html.Events exposing (onClick)

import Bootstrap.Grid as Grid
import Bootstrap.Grid.Row as Row
import Bootstrap.Grid.Col as Col
import Bootstrap.Form as Form
import Bootstrap.Form.Input as Input
import Bootstrap.Form.Textarea as Textarea
import Bootstrap.Form.Radio as Radio
import Bootstrap.Form.Select as Select
import Bootstrap.Button as Button
import Bootstrap.Accordion as Accordion
import Bootstrap.Card.Block as Block
import Bootstrap.Form.Fieldset as Fieldset
import Bootstrap.Utilities.Spacing as Spacing
import Bootstrap.Utilities.Display as Display
import Bootstrap.Utilities.Border as Border

import Select as Autocomplete

import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP


import List
import Set
import Url
import Http

import Api

import BoundedContext.Canvas exposing (Dependencies)
import BoundedContext
import BoundedContext.BoundedContextId as BoundedContext exposing(BoundedContextId)
import BoundedContext.Dependency as Dependency
import Domain
import Domain.DomainId as Domain

import Connection exposing (..)
import Debug
import Debug
import Maybe
import Html.Attributes

type alias DomainDependency =
  { id : Domain.DomainId
  , name : String }

type alias BoundedContextDependency =
  { id : BoundedContext.BoundedContextId
  , name: String
  , domain: DomainDependency }

type CollaboratorReference
  = BoundedContext BoundedContextDependency
  | Domain DomainDependency
  | ExternalSystem String
  | Frontend String

type CollaboratorReferenceType
  = BoundedContextType (Maybe BoundedContextDependency) Autocomplete.State
  | DomainType (Maybe DomainDependency) Autocomplete.State
  | ExternalSystemType (Maybe String)
  | FrontendType (Maybe String)

type alias DependencyEdit =
  { selectedCollaborator : Maybe CollaboratorReference
  , dependencySelectState : Autocomplete.State
  , relationship : Maybe Dependency.RelationshipPattern
  , existingDependencies : Dependency.DependencyMap }

type CustomerSupplierType
  = IsSupplier
  | IsCustomer

type RelationshipEdit
  = SymmetricCollaboration (Maybe SymmetricRelationship)
  | CustomerSupplierCollaboration (Maybe CustomerSupplierType)
  | UpstreamCollaboration (Maybe UpstreamRelationship) (Maybe DownstreamRelationship)
  | DownstreamCollaboration (Maybe DownstreamRelationship) (Maybe UpstreamRelationship)
  | UnknownCollaboration

type alias CollaborationEdit =
  { selectedCollaborator : Maybe CollaboratorReferenceType
  , dependencySelectState : Autocomplete.State
  , description : String
  , relationship : Maybe RelationshipEdit
  , collaboration : Maybe CollaborationDefinition
  , existingCollaboration : List Connection.Collaboration }

type alias Model =
  { boundedContextId : BoundedContextId
  , config : Api.Configuration
  , consumer : DependencyEdit
  , supplier : DependencyEdit
  , inbound : CollaborationEdit
  , outbound : CollaborationEdit
  , availableDependencies : List CollaboratorReference
  }

initDependency : Dependency.DependencyMap -> String -> DependencyEdit
initDependency existing id =
  { selectedCollaborator = Nothing
  , dependencySelectState = Autocomplete.newState id
  , relationship = Nothing
  , existingDependencies = existing }

initCollaboration : List Connection.Collaboration -> String -> CollaborationEdit
initCollaboration existing id =
  { selectedCollaborator = Nothing
  , dependencySelectState = Autocomplete.newState id
  , relationship = Nothing
  , description = ""
  , collaboration = Nothing
  , existingCollaboration = existing }

initDependencies : Api.Configuration -> BoundedContextId -> Dependencies -> Model
initDependencies config contextId dependencies =
  { config = config
  , boundedContextId = contextId
  , consumer = initDependency dependencies.consumers "consumer-select"
  , supplier = initDependency dependencies.suppliers "supplier-select"
  , availableDependencies = []
  , inbound = initCollaboration [] "inbound-select"
  , outbound = initCollaboration [] "outbound-select"
  }

init : Api.Configuration -> BoundedContext.BoundedContext -> Dependencies -> (Model, Cmd Msg)
init config context dependencies =
  (
    initDependencies config (context |> BoundedContext.id) dependencies
  , Cmd.batch [ loadBoundedContexts config, loadDomains config, loadConnections config (context |> BoundedContext.id)])

asDependencies : Model -> Dependencies
asDependencies model =
  { suppliers = model.supplier.existingDependencies
  , consumers = model.consumer.existingDependencies }

-- UPDATE

type Action t
  = Add t
  | Remove t

type alias DependencyAction = Action Dependency.Dependency

type ChangeTypeMsg
  = SelectMsg (Autocomplete.Msg CollaboratorReference)
  | OnSelect (Maybe CollaboratorReference)
  | SetRelationship String
  | DepdendencyChanged DependencyAction
  -- | DependencyAdded (Api.ApiResponse Connection.Collaboration)

type CollaboratorReferenceMsg
  = SelectCollaborationType CollaboratorReferenceType
  | SelectBoundedContextMsg (Autocomplete.Msg BoundedContextDependency)
  | OnBoundedContextSelect (Maybe BoundedContextDependency)
  | SelectDomainMsg (Autocomplete.Msg DomainDependency)
  | OnDomainSelect (Maybe DomainDependency)
  | ExternalSystemCaption String
  | FrontendCaption String

type CollaborationTypeMsg
  = CollaboratorSelection CollaboratorReferenceMsg
  | SetRelationship2 (Maybe RelationshipEdit)
  | SetDescription String
  | AddInboundConnection CollaborationDefinition String
  | AddOutboundConnection CollaborationDefinition String
  | InboundConnectionAdded (Api.ApiResponse Collaboration)
  | OutboundConnectionAdded (Api.ApiResponse Collaboration)
  | DependencyAdded (Api.ApiResponse Connection.Collaboration)

type Msg
  = Consumer ChangeTypeMsg
  | Supplier ChangeTypeMsg
  | CollaborationMsg CollaborationTypeMsg
  | BoundedContextsLoaded (Result Http.Error (List BoundedContextDependency))
  | DomainsLoaded (Result Http.Error (List DomainDependency))
  | ConnectionsLoaded (Result Http.Error (List CollaborationType))

type alias ModifyDependency = Dependency.Dependency -> String -> Api.ApiResult Connection.Collaboration ChangeTypeMsg

updateDependencyAction : DependencyAction -> Dependency.DependencyMap -> Dependency.DependencyMap
updateDependencyAction action depenencies =
  case action of
    Add dependency ->
      Dependency.registerDependency dependency depenencies
    Remove dependency  ->
      Dependency.removeDependency dependency depenencies

updateDependency : ChangeTypeMsg -> DependencyEdit -> (DependencyEdit, Cmd ChangeTypeMsg)
updateDependency msg model =
  case msg of
    SetRelationship relationship ->
      ({ model | relationship = Dependency.relationshipParser relationship }, Cmd.none)
    SelectMsg selMsg ->
      let
        ( updated, cmd ) =
          Autocomplete.update selectConfig selMsg model.dependencySelectState
      in
        ( { model | dependencySelectState = updated }, cmd )
    OnSelect item ->
      ({ model | selectedCollaborator = item }, Cmd.none)
    DepdendencyChanged change ->
      let
        m = updateDependencyAction change model.existingDependencies
      in
      ( { model
        | selectedCollaborator = Nothing
        , relationship = Nothing
        , existingDependencies = m
        }
      , Cmd.none
      )

updateCollaboratorSelection : CollaboratorReferenceMsg -> Maybe CollaboratorReferenceType -> (Maybe CollaboratorReferenceType, Cmd CollaboratorReferenceMsg)
updateCollaboratorSelection msg model =
  case (msg, model) of
    (SelectCollaborationType t, _) ->
      (Just t, Cmd.none)
    (SelectBoundedContextMsg selMsg, Just (BoundedContextType sel state)) ->
      let
        ( updated, cmd ) =
          Autocomplete.update selectBoundedContextConfig selMsg state
      in
        ( Just <| BoundedContextType sel updated, cmd )
    (OnBoundedContextSelect context, Just (BoundedContextType _ state)) ->
      ( Just <| BoundedContextType context state, Cmd.none)
    (SelectDomainMsg selMsg, Just (DomainType sel state)) ->
      let
        ( updated, cmd ) =
          Autocomplete.update selectDomainConfig selMsg state
      in
        ( Just <| DomainType sel updated, cmd )
    (OnDomainSelect domain, Just (DomainType _ state)) ->
      ( Just <| DomainType domain state, Cmd.none)
    (ExternalSystemCaption caption, Just (ExternalSystemType _)) ->
      ( if caption |> String.isEmpty
        then Nothing
        else Just caption
        |> ExternalSystemType
        |> Just
      , Cmd.none
      )
    (FrontendCaption caption, Just (FrontendType _)) ->
      ( if caption |> String.isEmpty
        then Nothing
        else Just caption
        |> FrontendType
        |> Just
      , Cmd.none
      )
    _ ->
      (model, Cmd.none)


updateInboundCollaboration : CollaborationEdit ->  CollaborationEdit
updateInboundCollaboration model =
  case (model.selectedCollaborator, model.relationship) of
    (Just collaboratorType, Just relationshipType) ->
      let
        collaborator = 
          case collaboratorType of
            BoundedContextType (Just bc) _ ->
              Just <| Connection.BoundedContext bc.id
            DomainType (Just d) _ ->
              Just <| Connection.Domain d.id
            ExternalSystemType (Just e) ->
              Just <| Connection.ExternalSystem e
            FrontendType (Just f) ->
              Just <| Connection.Frontend f
            _ ->
              Nothing
        inboundCollaboration =
          case relationshipType of
            UnknownCollaboration ->
              Maybe.map Connection.UnknownCollaboration collaborator
            SymmetricCollaboration st ->
              Maybe.map2 Connection.SymmetricCollaboration st collaborator
            CustomerSupplierCollaboration (Just IsSupplier) ->
              Maybe.map Connection.ASupplierForCollaboration collaborator
            CustomerSupplierCollaboration (Just IsCustomer) ->
              Maybe.map Connection.ACustomerOfCollaboration collaborator
            UpstreamCollaboration upstreamRelation downstreamRelation ->
              Maybe.map2 Connection.UpstreamCollaboration 
                upstreamRelation 
                ( collaborator 
                  |> Maybe.andThen (\c -> 
                    downstreamRelation |> Maybe.map(\d -> (c, d))
                  )
                )
            DownstreamCollaboration downstreamRelation upstreamRelation ->
              Maybe.map2 Connection.DownstreamCollaboration 
                downstreamRelation 
                ( collaborator 
                  |> Maybe.andThen (\c -> 
                    upstreamRelation |> Maybe.map(\d -> (c, d))
                  )
                )
            _ -> 
              Nothing
      in
        { model | collaboration = inboundCollaboration }
    _ ->
      { model | collaboration = Nothing }


updateCollaboration : Api.Configuration -> BoundedContextId -> CollaborationTypeMsg -> CollaborationEdit -> (CollaborationEdit, Cmd CollaborationTypeMsg)
updateCollaboration config bcId msg model =
  case msg of
    SetRelationship2 relationship ->
      (updateInboundCollaboration { model | relationship = relationship }, Cmd.none)
    
    CollaboratorSelection bcMsg ->
      let
        (updated, cmd) =
          updateCollaboratorSelection bcMsg model.selectedCollaborator
        in
          ( updateInboundCollaboration { model | selectedCollaborator = updated}
          , cmd |> Cmd.map CollaboratorSelection
          )
    AddInboundConnection coll desc ->
      ( model, Connection.defineInboundCollaboration config bcId coll desc InboundConnectionAdded )
    AddOutboundConnection coll desc ->
      ( model, Connection.defineOutboundCollaboration config bcId coll desc OutboundConnectionAdded )
    SetDescription d ->
      ( { model | description = d}, Cmd.none)
    InboundConnectionAdded (Ok result) ->
      ( { model | existingCollaboration = result :: model.existingCollaboration }, Cmd.none)
    OutboundConnectionAdded (Ok result) ->
      ( { model | existingCollaboration = result :: model.existingCollaboration }, Cmd.none)
    _ ->
      Debug.todo "Error handling"
      
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
    ConnectionsLoaded (Ok connections) ->
      let
        isInbound c =
          case c of
            Inbound _ -> True
            Outbound _ -> False

        (inbound, outbound) = 
          connections
          |> List.partition isInbound

        extractCollaboration colType =
          case colType of
            Inbound col ->
              col
            Outbound col ->
              col

        updateCollaborationModel colEdit items =
          { colEdit | existingCollaboration = items}

      in 
        ( { model
          | inbound = inbound |> List.map extractCollaboration |> updateCollaborationModel model.inbound
          , outbound = outbound |> List.map extractCollaboration |> updateCollaborationModel model.outbound
          }
        , Cmd.none
        )
    CollaborationMsg col ->
      let
        (m, cmd) = updateCollaboration model.config model.boundedContextId col model.inbound
      in
        ( { model | inbound = m}, cmd |> Cmd.map CollaborationMsg)
    _ ->
      let
        _ = Debug.log "Dependencies msg" msg
        _ = Debug.log "Dependencies model" model
      in
        (model, Cmd.none)

-- VIEW

toCollaborator : CollaboratorReference -> Dependency.Collaborator
toCollaborator dependency =
  case dependency of
    BoundedContext bc ->
      Dependency.BoundedContext bc.id
    Domain d ->
      Dependency.Domain d.id
    _ ->
      Debug.todo "handle later"

toCollaborator2 : CollaboratorReference -> Connection.Collaborator
toCollaborator2 dependency =
  case dependency of
    BoundedContext bc ->
      Connection.BoundedContext bc.id
    Domain d ->
      Connection.Domain d.id
    ExternalSystem s ->
      Connection.ExternalSystem s
    Frontend f ->
      Connection.Frontend f


filterLowerCase : Int -> (item -> List String) -> String -> List item -> Maybe (List item)
filterLowerCase minChars stringsToCompare query items =
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
        i
        |> stringsToCompare
        |> List.any containsLowerString
      in
        items
        |> List.filter searchable
        |> Just

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
          ExternalSystem s ->
            containsLowerString s
          Frontend f ->
            containsLowerString f
      in
        items
        |> List.filter searchable
        |> Just

oneLineLabel : CollaboratorReference -> String
oneLineLabel item =
  case item of
    BoundedContext bc ->
      bc.domain.name ++ " - " ++ bc.name
    Domain d ->
      d.name
    ExternalSystem s ->
      s
    Frontend f ->
      f

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
      ExternalSystem s ->
        [ Html.span [] [ text s ] ]
      Frontend f ->
        [ Html.span [] [ text f ] ]
  in
    Html.span [] content

selectConfig : Autocomplete.Config ChangeTypeMsg CollaboratorReference
selectConfig =
    Autocomplete.newConfig
        { onSelect = OnSelect
        , toLabel = oneLineLabel
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

selectBoundedContextConfig : Autocomplete.Config CollaboratorReferenceMsg BoundedContextDependency
selectBoundedContextConfig =
    Autocomplete.newConfig
        { onSelect = OnBoundedContextSelect
        , toLabel = (\bc -> bc.domain.name ++ " - " ++ bc.name)
        , filter = filterLowerCase 2 (\bc -> [ bc.name, bc.domain.name ])
        }
        |> Autocomplete.withCutoff 12
        |> Autocomplete.withInputClass "text-control border rounded form-control-lg"
        |> Autocomplete.withInputWrapperClass ""
        |> Autocomplete.withItemClass " border p-2 "
        |> Autocomplete.withMenuClass "bg-light"
        |> Autocomplete.withNotFound "No matches"
        |> Autocomplete.withNotFoundClass "text-danger"
        |> Autocomplete.withHighlightedItemClass "bg-white"
        |> Autocomplete.withPrompt "Search for a Bounded Context"
        |> Autocomplete.withItemHtml (\bc ->  
            Html.span [] 
              [ Html.h6 [ class "text-muted" ] [ text bc.domain.name ]
              , Html.span [] [ text bc.name ] 
              ]
        )

selectDomainConfig : Autocomplete.Config CollaboratorReferenceMsg DomainDependency
selectDomainConfig =
    Autocomplete.newConfig
        { onSelect = OnDomainSelect
        , toLabel = (\domain -> domain.name )
        , filter = filterLowerCase 2 (\domain -> [ domain.name ])
        }
        |> Autocomplete.withCutoff 12
        |> Autocomplete.withInputClass "text-control border rounded form-control-lg"
        |> Autocomplete.withInputWrapperClass ""
        |> Autocomplete.withItemClass " border p-2 "
        |> Autocomplete.withMenuClass "bg-light"
        |> Autocomplete.withNotFound "No matches"
        |> Autocomplete.withNotFoundClass "text-danger"
        |> Autocomplete.withHighlightedItemClass "bg-white"
        |> Autocomplete.withPrompt "Search for a Domain"
        |> Autocomplete.withItemHtml (\domain ->  
          Html.span [] [ text domain.name ] 
        )

translateRelationship : Dependency.RelationshipPattern -> String
translateRelationship relationship =
  case relationship of
    Dependency.AntiCorruptionLayer -> "Anti Corruption Layer"
    Dependency.OpenHostService -> "Open Host Service"
    Dependency.PublishedLanguage -> "Published Language"
    Dependency.SharedKernel ->"Shared Kernel"
    Dependency.UpstreamDownstream -> "Upstream/Downstream"
    Dependency.Conformist -> "Conformist"
    Dependency.Octopus -> "Octopus"
    Dependency.Partnership -> "Partnership"
    Dependency.CustomerSupplier -> "Customer/Supplier"

viewAddedDepencency : List CollaboratorReference -> Dependency.Dependency -> Html ChangeTypeMsg
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
            |> Remove |> DepdendencyChanged
          )
        ]
        [ text "x" ]
      ]
    ]

onSubmitMaybe : Maybe msg -> Html.Attribute msg
onSubmitMaybe maybeMsg =
  -- https://thoughtbot.com/blog/advanced-dom-event-handlers-in-elm
  let
    preventDefault m =
      ( m, True )
  in case maybeMsg of
    Just msg ->
      Html.Events.preventDefaultOn "submit" (Decode.map preventDefault (Decode.succeed msg))
    Nothing ->
      Html.Events.preventDefaultOn "submit" (Decode.map preventDefault (Decode.fail "No message to submit"))


viewAddDependency : List CollaboratorReference -> DependencyEdit -> Html ChangeTypeMsg
viewAddDependency dependencies model =
  let
    items =
      [ Dependency.AntiCorruptionLayer
      , Dependency.OpenHostService
      , Dependency.PublishedLanguage
      , Dependency.SharedKernel
      , Dependency.UpstreamDownstream
      , Dependency.Conformist
      , Dependency.Octopus
      , Dependency.Partnership
      , Dependency.CustomerSupplier
      ]
        |> List.map (\r -> (r, translateRelationship r))
        |> List.map (\(v,t) -> Select.item [value (Dependency.relationshipToString v)] [ text t])

    selectedItem =
      case model.selectedCollaborator of
        Just s -> [ s ]
        Nothing -> []

    -- TODO: this is probably very unefficient
    existingDependencies =
      model.existingDependencies
      |> Dependency.dependencyList
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
        |> Maybe.map (Add >> DepdendencyChanged)
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
      |> Dependency.dependencyList
      |> List.map (viewAddedDepencency items))
    , viewAddDependency items model
    ]

translateSymmetricRelationship relationship =
  case relationship of
    SharedKernel -> "SK"
    Partnership -> "PS"
    SeparateWays -> "SW"
    BigBallOfMud -> "BBoM"


translateUpstreamRelationship relationship =
  case relationship of
    Upstream -> "US"
    PublishedLanguage -> "PL"
    OpenHost -> "OHS"

translateDownstreamRelationship relationship =
  case relationship of
    Downstream -> "DS"
    AntiCorruptionLayer -> "ACL"
    Conformist -> "CF"

translateUpstreamDownstreamRelationship relationship =
  case relationship of
    CustomerSupplierRole _ -> "CUS/SUP"
    UpstreamDownstreamRole (_,ut) (_,dt) -> 
      ( translateDownstreamRelationship dt ) ++ "/" ++ ( translateUpstreamRelationship ut)

viewCollaboration : List CollaboratorReference -> Collaboration -> Html CollaborationTypeMsg
viewCollaboration items collaboration =
  let
    relationship = Connection.relationship collaboration
    description = Connection.description collaboration

    collaboratorReferenceName collaborator =
      items
      |> List.filter (\r -> toCollaborator r == collaborator )
      |> List.head
      |> Maybe.map renderItem
      |> Maybe.withDefault (text "Unknown name")

    collaboratorCaption collaborator =
      case collaborator of
        Connection.BoundedContext bc ->
          collaboratorReferenceName <| Dependency.BoundedContext bc
        Connection.Domain d ->
          collaboratorReferenceName <| Dependency.Domain d
        Connection.ExternalSystem s ->
          Html.span
             []
             [ Html.h6 [ class "text-muted" ] [ text "External System" ]
             , Html.span [] [ text s] 
             ]
        Connection.Frontend s ->
          Html.span
             []
             [ Html.h6 [ class "text-muted" ] [ text "Frontend" ]
             , Html.span [] [ text s] 
             ]
        UserInteraction s ->
          Html.span
             []
             [ Html.h6 [ class "text-muted" ] [ text "User Interaction" ]
             , Html.span [] [ text s] 
             ]
      

    cols =
      case relationship of
        Symmetric t p1 _ ->
          [ Grid.col [] [ collaboratorCaption p1]
          , Grid.col [] 
            [ Html.h6 [] [ text <| translateSymmetricRelationship t ]
            , Html.span [] [ text <| Maybe.withDefault "" description ]
            ]
          ]
        UpstreamDownstream (CustomerSupplierRole { customer, supplier}) ->
          [ Grid.col [] [ collaboratorCaption customer ]
          , Grid.col [] 
            [ Html.h6[] [ text "CUS" ] ]
          , Grid.col [] 
            [ Html.span [] [ text <| Maybe.withDefault "" description ] ]
          , Grid.col [] 
            [ Html.h6[] [ text "SUP" ] ]
          ]
            
        UpstreamDownstream (UpstreamDownstreamRole (collaborator,upstreamType) (_,downstreamType)) ->
          [ Grid.col [] [ collaboratorCaption collaborator ]
          , Grid.col [] 
            [ Html.h6[] [ text <| translateUpstreamRelationship upstreamType ] ]
          , Grid.col [] 
            [ Html.span [] [ text <| Maybe.withDefault "" description ] ]
          , Grid.col [] 
            [ Html.h6[] [ text <| translateDownstreamRelationship downstreamType ] ]
          ]
        Octopus _ _ ->
          []
        Unknown p1 _ ->
          [ Grid.col [] [ collaboratorCaption p1 ]
          , Grid.col [] 
            [ Html.h6[] [ text "Unknown" ]
            , Html.span [] [ text <| Maybe.withDefault "" description ]
            ]
          ]
    
  in
    Grid.row 
      [ Row.attrs [ Border.top, Spacing.mb2, Spacing.pt1 ] ]
      (  List.append 
          cols
          [ Grid.col [ Col.xs2 ]
            [ Button.button
              [ Button.secondary
              -- , Button.onClick (
              --     (collaborator, relationship)
              --     |> Remove |> DepdendencyChanged
              --   )
              ]
              [ text "x" ]
            ]
          ]
      )

viewAddConnection : List CollaboratorReference -> CollaborationEdit -> Html CollaborationTypeMsg
viewAddConnection dependencies model =
  let
    captionAndDescription caption description =
      Html.span [] 
        [ text <| caption
        , Form.help [] [ text description ]
        ]
    labelAndDescription caption abbreviation description =
      Radio.label [] [ captionAndDescription (caption ++ " (" ++ abbreviation ++ ")") description ]
    
    -- selectedItem =
    --   case model.selectedCollaborator of
    --     Just (Just (BoundedContextType s)) -> [ s ]
    --     Nothing -> []

    -- TODO: this is probably very unefficient
    -- existingDependencies =
    --   model.existingCollaboration
    --   |> List.map Tuple.first

    -- relevantDependencies =
    --   dependencies
    --   |> List.filter (\d -> existingDependencies |> List.any (\existing -> (d |> toCollaborator)  == existing ) |> not )

    radioList = 
      Radio.radioList "collaboratorSelection" 
        [ Radio.create
          [ Radio.id "boundedContextOption"
          , Radio.onClick (SelectCollaborationType (BoundedContextType Nothing (Autocomplete.newState "boundedContext")))
          , Radio.checked (
              case model.selectedCollaborator of 
                Just (BoundedContextType _ _) -> True
                _ -> False
            )
          ]
          "Bounded Context"
        , Radio.create
          [ Radio.id "domainOption"
          , Radio.onClick (SelectCollaborationType (DomainType Nothing (Autocomplete.newState "domain")))
          , Radio.checked (
              case model.selectedCollaborator of 
                Just (DomainType _ _) -> True
                _ -> False
            )
          ]
          "Domain"
        , Radio.create
          [ Radio.id "externalSystemOption"
          , Radio.onClick (SelectCollaborationType (ExternalSystemType Nothing))
          , Radio.checked (
              case model.selectedCollaborator of 
                Just (ExternalSystemType _) -> True
                _ -> False
            )
          ]
          "External System"
         , Radio.create
          [ Radio.id "frontendOption"
          , Radio.onClick (SelectCollaborationType (FrontendType Nothing))
          , Radio.checked (
              case model.selectedCollaborator of 
                Just (FrontendType _) -> True
                _ -> False
            )
          ]
          "Frontend"
        ]

    asList maybe =
      case maybe of
        Just value -> [ value ]
        Nothing -> []

    collaboratorSelection =
      Form.group
       [] 
       [ Html.div [] radioList
       , (
        case model.selectedCollaborator of
          Just selectedType ->
            case selectedType of
              BoundedContextType selected state ->
                 Autocomplete.view
                  selectBoundedContextConfig
                  state
                  ( dependencies
                    |> List.filterMap (\item ->
                        case item of
                          BoundedContext bc ->
                            Just bc
                          _ -> 
                            Nothing
                      )
                  )
                  (asList selected)
                |> Html.map SelectBoundedContextMsg
              DomainType selected state ->
                Autocomplete.view
                  selectDomainConfig
                  state
                  ( dependencies
                    |> List.filterMap (\item ->
                        case item of
                          Domain bc ->
                            Just bc
                          _ -> 
                            Nothing
                      )
                  )
                  (asList selected)
                |> Html.map SelectDomainMsg
              ExternalSystemType value ->
                Input.text
                  [ Input.value (value |> Maybe.withDefault "")
                  , Input.placeholder "External System name"
                  , Input.onInput ExternalSystemCaption
                  ]
              FrontendType value ->
                Input.text
                  [ Input.value (value |> Maybe.withDefault "")
                  , Input.placeholder "Frontend name"
                  , Input.onInput FrontendCaption
                  ]
          Nothing ->
            Input.text
              [ Input.disabled True
              , Input.placeholder "Select an option"]
       )
       ]
      |> Html.map CollaboratorSelection
        
    symmetricConfiguration =
      let
        renderOption idValue label abbreviation descriptionText value =
          Radio.createAdvanced
            [ Radio.id idValue
            , Radio.onClick (SetRelationship2 (Just <| SymmetricCollaboration <| Just value))
            , Radio.checked (
                case model.relationship of
                  Just (SymmetricCollaboration (Just x)) ->
                    x == value
                  _ ->
                    False
                )
            ]
            ( labelAndDescription label abbreviation descriptionText )
        options =
          Radio.label [] 
            ( ( captionAndDescription "Symmetric" "The relationship between the collaborators is equal or symmetric." )
              :: [ Html.div []
                (  Radio.radioList "symmetricOptions"
                  [ renderOption "sharedKernel" "Shared Kernel" "SK" "Technical artefacts are shared between the collaborators" SharedKernel
                  , renderOption "PartnershipOption" "Partnership" "PS ""The collaborators work together to reach a common goal" Partnership
                  , renderOption "separateWaysOption" "Separate Ways" "SW" "The collaborators decided to NOT use information, but rather work in seperate ways" SeparateWays
                  , renderOption "bigBallOfMudOption" "Big Ball of Mud" "BBoM" "It's complicated..."  BigBallOfMud
                  ]
                )
              ]  
          )      
      in
        Radio.createAdvanced
          [ Radio.id "symmetricType"
          , Radio.onClick (SetRelationship2 (Just <| SymmetricCollaboration Nothing))
          , Radio.checked (
              case model.relationship of
                Just (SymmetricCollaboration _) ->
                  True
                _ ->
                  False
              )
          ]
          options

    customerSupplierConfiguration =
      Radio.createAdvanced
        [ Radio.id "customerType"
        , Radio.onClick (SetRelationship2 (Just <| CustomerSupplierCollaboration Nothing))
        , Radio.checked (
            case model.relationship of
              Just (CustomerSupplierCollaboration _) ->
                True
              _ ->
                False
          )
        ]
        ( Radio.label [] 
          [ captionAndDescription "Customer/Supplier" "There is a cooperation with the collaborator that can be described as a customer/supplier relationship."
          , Html.div [] 
            ( Radio.radioList "customerSupplierOptions"
              [ Radio.createAdvanced
                [ Radio.id "isCustomerOption"
                , Radio.checked (model.relationship == (Just <| CustomerSupplierCollaboration <| Just IsCustomer))
                , Radio.onClick (SetRelationship2 (Just <| CustomerSupplierCollaboration <| Just IsCustomer))
                , Radio.inline
                ]
                ( labelAndDescription "Customer" "CUS" "The collaborator is in the customer role" )
              , Radio.createAdvanced
                [ Radio.id "isSupplierOption"
                , Radio.checked (model.relationship == (Just <| CustomerSupplierCollaboration <| Just IsSupplier))
                , Radio.onClick (SetRelationship2 (Just <| CustomerSupplierCollaboration <| Just IsSupplier))
                , Radio.inline
                ]
                ( labelAndDescription "Supplier" "SUP" "The collaborator is in the supplier role" )
              ]
            )
          ]
        )

    upstreamConfiguration =
      let
        isUpstream upstreamConfig =
          case model.relationship of
            (Just (UpstreamCollaboration (Just up) _)) ->
              up == upstreamConfig
            _ ->
              False
        setUpstream upstreamConfig =
          case model.relationship of
            (Just (UpstreamCollaboration _ down)) ->
              SetRelationship2 (Just <| UpstreamCollaboration (Just upstreamConfig) down)
            _ ->
              SetRelationship2 (Just <| UpstreamCollaboration (Just upstreamConfig) Nothing)

        isDownstream downstreamConfig =
          case model.relationship of
            (Just (UpstreamCollaboration _ (Just down))) ->
              down == downstreamConfig
            _ ->
              False
        setDownstream downstreamConfig =
          case model.relationship of
            (Just (UpstreamCollaboration up _)) ->
              SetRelationship2 (Just <| UpstreamCollaboration up (Just downstreamConfig))
            _ ->
              SetRelationship2 (Just <| UpstreamCollaboration Nothing (Just downstreamConfig))

        options = 
          Radio.label [] 
            [ captionAndDescription "Upstream" "The collaborator is upstream and I depend on changes."
            , Html.div [ class "row" ]
              [ Form.group [ Form.attrs [ class "col"]  ]
                ( Html.span[] [ text "Describe the collaborator:" ]
                :: Radio.radioList "upstreamOptions"
                  [ Radio.createAdvanced
                    [ Radio.id "upstreamOption"
                    , Radio.checked (isUpstream Upstream)
                    , Radio.onClick (setUpstream Upstream)
                    ]
                    ( labelAndDescription "Upstream" "US" "The collaborator is just Upstream" )
                  , Radio.createAdvanced
                    [ Radio.id "publishedLanguageOption"
                    , Radio.checked (isUpstream PublishedLanguage)
                    , Radio.onClick (setUpstream PublishedLanguage)
                    ]
                    ( labelAndDescription "Published Language" "PL" "The collaborator is using a Published Language" )
                  , Radio.createAdvanced
                    [ Radio.id "openHostOption"
                    , Radio.checked (isUpstream OpenHost)
                    , Radio.onClick (setUpstream OpenHost)
                    ]
                    ( labelAndDescription "Open Host Service" "OHS" "The collaborator is providing an Open Host Service" )
                  ]
                )
              , Form.group [ Form.attrs [ class "col"]  ]
                ( Html.span[] [ text "Describe your relationship with the collaborator:" ]
                :: Radio.radioList "downstreamOptions"
                  [ Radio.createAdvanced
                    [ Radio.id "downstreamOption"
                    , Radio.checked (isDownstream Downstream)
                    , Radio.onClick (setDownstream Downstream)
                    ]
                    ( labelAndDescription "Downstream" "DS" "I'm just Downstream" )
                  , Radio.createAdvanced
                    [ Radio.id "aclOption"
                    , Radio.checked (isDownstream AntiCorruptionLayer)
                    , Radio.onClick (setDownstream AntiCorruptionLayer)
                    ]
                    ( labelAndDescription "Anti-Corruption-Layer" "ACL" "I'm using an Anti-Corruption-Layer to shield me from changes" )
                  , Radio.createAdvanced
                    [ Radio.id "cfOption"
                    , Radio.checked (isDownstream Conformist)
                    , Radio.onClick (setDownstream Conformist)
                    ]
                    ( labelAndDescription "Conformist" "CF" "I'm Conformist to upstream changes" )
                  ]
                )
              ]
            ]
        in
        Radio.createAdvanced
          [ Radio.id "upstreamType"
          , Radio.onClick (SetRelationship2 (Just <| UpstreamCollaboration Nothing Nothing))
          , Radio.checked (
              case model.relationship of
                Just (UpstreamCollaboration _ _) ->
                  True
                _ ->
                  False
            )
          ]
          options

    downstreamConfiguration =
      let
        isUpstream upstreamConfig =
          case model.relationship of
            (Just (DownstreamCollaboration _ (Just up))) ->
              up == upstreamConfig
            _ ->
              False
        setUpstream upstreamConfig =
          case model.relationship of
            (Just (DownstreamCollaboration down _ )) ->
              SetRelationship2 (Just <| DownstreamCollaboration down (Just upstreamConfig))
            _ ->
              SetRelationship2 (Just <| DownstreamCollaboration Nothing (Just upstreamConfig) )

        isDownstream downstreamConfig =
          case model.relationship of
            (Just (DownstreamCollaboration (Just down) _ )) ->
              down == downstreamConfig
            _ ->
              False
        setDownstream downstreamConfig =
          case model.relationship of
            (Just (DownstreamCollaboration _ up)) ->
              SetRelationship2 (Just <| DownstreamCollaboration (Just downstreamConfig) up)
            _ ->
              SetRelationship2 (Just <| DownstreamCollaboration (Just downstreamConfig) Nothing)

        options = 
          Radio.label [] 
            [ captionAndDescription "Downstream" "The collaborator is downstream and they depend on my changes."
            , Html.div [ class "row" ]
              [ Form.group [ Form.attrs [ class "col"] ]
                ( Html.span[] [ text "Describe the collaborator:" ]
                :: Radio.radioList "downstreamOptions"
                  [ Radio.createAdvanced
                    [ Radio.id "downstreamOption"
                    , Radio.checked (isDownstream Downstream)
                    , Radio.onClick (setDownstream Downstream)
                    ]
                    ( labelAndDescription "Downstream" "DS" "The collaborator is just Downstream" )
                  , Radio.createAdvanced
                    [ Radio.id "aclOption"
                    , Radio.checked (isDownstream AntiCorruptionLayer)
                    , Radio.onClick (setDownstream AntiCorruptionLayer)
                    ]
                    ( labelAndDescription "Anti-Corruption-Layer" "ACL" "The collaborator is using an Anti-Corruption-Layer to shield from my changes" )
                  , Radio.createAdvanced
                    [ Radio.id "cfOption"
                    , Radio.checked (isDownstream Conformist)
                    , Radio.onClick (setDownstream Conformist)
                    ]
                    ( labelAndDescription "Conformist" "CF" "The collaborator is Conformist to my upstream changes" )
                  ]
                )
              , Form.group [ Form.attrs [ class "col"] ]
                ( Html.span[] [ text "Describe your relationship with the collaborator:" ]
                :: Radio.radioList "upstreamOptions"
                  [ Radio.createAdvanced
                    [ Radio.id "upstreamOption"
                    , Radio.checked (isUpstream Upstream)
                    , Radio.onClick (setUpstream Upstream)
                    ]
                    ( labelAndDescription "Upstream" "US" "I'm just Upstream" )
                  , Radio.createAdvanced
                    [ Radio.id "publishedLanguageOption"
                    , Radio.checked (isUpstream PublishedLanguage)
                    , Radio.onClick (setUpstream PublishedLanguage)
                    ]
                    ( labelAndDescription "Published Language" "PL" "I'm using a Published Language" )
                  , Radio.createAdvanced
                    [ Radio.id "openHostOption"
                    , Radio.checked (isUpstream OpenHost)
                    , Radio.onClick (setUpstream OpenHost)
                    ]
                    ( labelAndDescription "Open Host Service" "OHS" "I'm providing an Open Host Service" )
                  ]
                )
                
              ]
            ]
        in
        Radio.createAdvanced
          [ Radio.id "upstreamType"
          , Radio.onClick (SetRelationship2 (Just <| UpstreamCollaboration Nothing Nothing))
          , Radio.checked (
              case model.relationship of
                Just (UpstreamCollaboration _ _) ->
                  True
                _ ->
                  False
            )
          ]
          options

    unknownConfiguration =
      Radio.createAdvanced
        [ Radio.id "uknownType"
        , Radio.onClick (SetRelationship2 (Just UnknownCollaboration))
        , Radio.checked (model.relationship == Just UnknownCollaboration)
        ]
        ( labelAndDescription  "Unkown" "?" "Is the exact description of the relationship unknown or you are not sure how to describe it."
        )
  in
  Form.form []
    [ Fieldset.config
      |> Fieldset.legend [] [ text "Collaborator"]
      |> Fieldset.children
        [ text "Please select the collaborator of the connection"
        , collaboratorSelection
        ]
      |> Fieldset.view
    , Fieldset.config
      |> Fieldset.legend [] [ text "Collaboration Type"]
      |> Fieldset.children
        [ Html.div []
            ( Radio.radioList "collaborationTypeSelection"
              [ unknownConfiguration
              , symmetricConfiguration
              , customerSupplierConfiguration
              , upstreamConfiguration
              , downstreamConfiguration
              ]
            )
        ]
      |> Fieldset.view
    , Fieldset.config
      |> Fieldset.legend [] [ text "Description" ]
      |> Fieldset.children
        [ text "An optional description of the relationship and collaboration."
        , Textarea.textarea [ Textarea.id "description", Textarea.rows 3, Textarea.value model.description]
        ]
      |> Fieldset.view
    
    , Button.submitButton
      [ Button.primary
      , Button.disabled (model.collaboration == Nothing)
      , model.collaboration 
        |> Maybe.map (\c -> AddInboundConnection c model.description)
        |> Maybe.map Button.onClick
        |> Maybe.withDefault (Button.attrs [])
      ]
      [ text "Add inbound connection" ]
    , Button.submitButton
      [ Button.primary
      , Button.disabled (model.collaboration == Nothing)
      , model.collaboration 
        |> Maybe.map (\c -> AddOutboundConnection c model.description)
        |> Maybe.map Button.onClick
        |> Maybe.withDefault (Button.attrs [])
      ]
      [ text "Add outbound connection" ]
    ]

viewConnection : List CollaboratorReference -> String -> CollaborationEdit -> Html CollaborationTypeMsg
viewConnection items title model =
  div []
    [ Html.h6
      [ class "text-center", Spacing.p2 ]
      [ Html.strong [] [ text title ] ]
    , Grid.row []
      [ Grid.col [] [ Html.h6 [] [ text "Name"] ]
      , Grid.col [] [ Html.h6 [] [ text "Initiator-Type"] ]
      , Grid.col [] [ Html.h6 [] [ text "Description"] ]
      , Grid.col [] [ Html.h6 [] [ text "Recipient-Type"] ]
      , Grid.col [Col.xs2] []
      ]
    , div []
      (model.existingCollaboration
      |> List.map (viewCollaboration items))
     , viewAddConnection items model
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
     , Grid.row []
      [ Grid.col []
        [ viewConnection model.availableDependencies "Inbound Connection" model.inbound |> Html.map CollaborationMsg ]
      , Grid.col []
        [ viewConnection model.availableDependencies "Outbound Connection" model.outbound |> Html.map CollaborationMsg ]
      ]
    -- , Grid.simpleRow
    --   [ Grid.col []
    --       [ viewAddConnection model.availableDependencies model]

    --   ]
    ]

domainDecoder : Decoder DomainDependency
domainDecoder =
  Domain.domainDecoder
  |> Decode.map (\d -> { id = d |> Domain.id, name = d |> Domain.name})

boundedContextDecoder : Decoder BoundedContextDependency
boundedContextDecoder =
  -- TODO: can we reuse BoundedContext.modelDecoder?
  Decode.succeed BoundedContextDependency
    |> JP.custom BoundedContext.idFieldDecoder
    |> JP.custom BoundedContext.nameFieldDecoder
    |> JP.required "domain" domainDecoder

loadBoundedContexts: Api.Configuration -> Cmd Msg
loadBoundedContexts config =
  Http.get
    { url = Api.allBoundedContexts [ Api.Domain ] |> Api.url config |> Url.toString
    , expect = Http.expectJson BoundedContextsLoaded (Decode.list boundedContextDecoder)
    }

loadDomains: Api.Configuration -> Cmd Msg
loadDomains config =
  Http.get
    { url = Api.domains [] |> Api.url config |> Url.toString
    , expect = Http.expectJson DomainsLoaded (Decode.list domainDecoder)
    }

loadConnections : Api.Configuration -> BoundedContextId -> Cmd Msg
loadConnections config context =
  let
    filterConnections connections =
      connections
      |> List.filterMap (Connection.isCollaborator (Connection.BoundedContext context))
  in Http.get
    { url = Api.communication |> Api.url config |> Url.toString
    , expect = Http.expectJson ConnectionsLoaded (Decode.map filterConnections (Decode.list Connection.modelDecoder))
    }