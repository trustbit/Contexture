module Page.Bcc.Edit.Dependencies exposing (
  Msg(..), Model,
  update, init, view)

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
import ContextMapping.CollaborationId as ContextMapping exposing(CollaborationId)
import ContextMapping.Collaborator as Collaborator exposing(Collaborator)
import ContextMapping.RelationshipType exposing(..)
import BoundedContext.BoundedContextId as BoundedContext exposing(BoundedContextId)
import BoundedContext as BoundedContext
import BoundedContext.Dependency as Dependency
import Domain
import Domain.DomainId as Domain

import Connection exposing (..)
import Debug
import Debug
import Maybe
import Html.Attributes
import Bootstrap.Navbar exposing (items)

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
  , collaborator : Maybe Collaborator
  }

type alias Model =
  { boundedContextId : BoundedContextId
  , config : Api.Configuration
  , newCollaborations : CollaborationEdit
  , availableDependencies : List CollaboratorReference
  , inboundCollaboration : List Connection.Collaboration
  , outboundCollaboration : List Connection.Collaboration
  , inboundCollaboration2 : List Connection.Collaboration2
  , outboundCollaboration2 : List Connection.Collaboration2
  }

initCollaboration : String -> CollaborationEdit
initCollaboration id =
  { selectedCollaborator = Nothing
  , dependencySelectState = Autocomplete.newState id
  , relationship = Nothing
  , description = ""
  , collaboration = Nothing
  , collaborator = Nothing
  }

initDependencies : Api.Configuration -> BoundedContextId -> Model
initDependencies config contextId =
  { config = config
  , boundedContextId = contextId
  , availableDependencies = []
  , inboundCollaboration = []
  , outboundCollaboration = []
  , inboundCollaboration2 = []
  , outboundCollaboration2 = []
  , newCollaborations = initCollaboration "collaboration-select"
  }

init : Api.Configuration -> BoundedContext.BoundedContext -> Dependencies -> (Model, Cmd Msg)
init config context dependencies =
  (
    initDependencies config (context |> BoundedContext.id)
  , Cmd.batch [ loadBoundedContexts config, loadDomains config, loadConnections config (context |> BoundedContext.id)])

-- UPDATE

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
  | SetRelationship (Maybe RelationshipEdit)
  | SetDescription String 

type Msg
  = CollaborationMsg CollaborationTypeMsg
  | BoundedContextsLoaded (Result Http.Error (List BoundedContextDependency))
  | DomainsLoaded (Result Http.Error (List DomainDependency))
  | ConnectionsLoaded (Result Http.Error (List CollaborationType))
  | AddInboundConnection CollaborationDefinition String
  | AddInboundConnection2 Collaborator String
  | AddOutboundConnection CollaborationDefinition String
  | AddOutboundConnection2 Collaborator String
  | InboundConnectionAdded (Api.ApiResponse Collaboration)
  | InboundConnectionAdded2 (Api.ApiResponse Collaboration2)
  | OutboundConnectionAdded (Api.ApiResponse Collaboration)
  | OutboundConnectionAdded2 (Api.ApiResponse Collaboration2)

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


updateCollaborationDefinition : CollaborationEdit ->  CollaborationEdit
updateCollaborationDefinition model =
  let
    getCollaborator collaboratorType = 
      case collaboratorType of
        BoundedContextType (Just bc) _ ->
          Just <| Collaborator.BoundedContext bc.id
        DomainType (Just d) _ ->
          Just <| Collaborator.Domain d.id
        ExternalSystemType (Just e) ->
          Just <| Collaborator.ExternalSystem e
        FrontendType (Just f) ->
          Just <| Collaborator.Frontend f
        _ ->
          Nothing
  in case (model.selectedCollaborator, model.relationship) of
    (Just collaboratorType, Just relationshipType) ->
      let
        collaborator = getCollaborator collaboratorType
        collaborationDefinition =
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
        { model | collaboration = collaborationDefinition, collaborator = collaborator }
    (Just collaboratorType, _) ->
      { model | collaborator = getCollaborator collaboratorType }
    _ ->
      { model | collaboration = Nothing, collaborator = Nothing }


updateCollaboration : CollaborationTypeMsg -> CollaborationEdit -> (CollaborationEdit, Cmd CollaborationTypeMsg)
updateCollaboration msg model =
  case msg of
    SetRelationship relationship ->
      (updateCollaborationDefinition { model | relationship = relationship }, Cmd.none)
    
    CollaboratorSelection bcMsg ->
      let
        (updated, cmd) =
          updateCollaboratorSelection bcMsg model.selectedCollaborator
        in
          ( updateCollaborationDefinition { model | selectedCollaborator = updated}
          , cmd |> Cmd.map CollaboratorSelection
          )
    SetDescription d ->
      ( { model | description = d}, Cmd.none)
      
update : Msg -> Model -> (Model, Cmd Msg)
update msg model =
  case msg of
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
        (inbound, outbound) = 
          connections
          |> List.foldl(\c (inbounds,outbounds) -> 
              case c of
                Inbound inboundColl ->
                  (inboundColl :: inbounds,outbounds)
                Outbound outboundColl ->
                  (inbounds,outboundColl :: outbounds)
            ) ([],[])

      in 
        ( { model
          | inboundCollaboration2 = List.append model.inboundCollaboration2 inbound
          , outboundCollaboration2 = List.append model.outboundCollaboration2 outbound
          }
        , Cmd.none
        )
    CollaborationMsg col ->
      let
        (m, cmd) = updateCollaboration col model.newCollaborations
      in
        ( { model | newCollaborations = m}, cmd |> Cmd.map CollaborationMsg)
   
    AddInboundConnection coll desc ->
      ( model, Connection.defineInboundCollaboration model.config model.boundedContextId coll desc InboundConnectionAdded )
    AddOutboundConnection coll desc ->
      ( model, Connection.defineOutboundCollaboration model.config model.boundedContextId coll desc OutboundConnectionAdded )

    AddInboundConnection2 coll desc ->
      ( model, Connection.defineInboundCollaboration2 model.config model.boundedContextId coll desc InboundConnectionAdded2 )
    AddOutboundConnection2 coll desc ->
      ( model, Connection.defineOutboundCollaboration2 model.config model.boundedContextId coll desc OutboundConnectionAdded2 )
    
    InboundConnectionAdded (Ok result) ->
      ( { model | inboundCollaboration = result :: model.inboundCollaboration }, Cmd.none)
    OutboundConnectionAdded (Ok result) ->
      ( { model | outboundCollaboration = result :: model.outboundCollaboration }, Cmd.none)
    InboundConnectionAdded2 (Ok result) ->
      ( { model | inboundCollaboration2 = result :: model.inboundCollaboration2 }, Cmd.none)
    OutboundConnectionAdded2 (Ok result) ->
      ( { model | outboundCollaboration2 = result :: model.outboundCollaboration2 }, Cmd.none)

    _ ->
      let
        _ = Debug.log "Dependencies msg" msg
        _ = Debug.log "Dependencies model" model
      in
        (model, Cmd.none)

-- VIEW

toCollaborator2 : CollaboratorReference -> Collaborator
toCollaborator2 dependency =
  case dependency of
    BoundedContext bc ->
      Collaborator.BoundedContext bc.id
    Domain d ->
      Collaborator.Domain d.id
    ExternalSystem s ->
      Collaborator.ExternalSystem s
    Frontend f ->
      Collaborator.Frontend f


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


type alias ResolveCollaboratorCaption = Collaborator -> Html Msg

collaboratorCaption : List CollaboratorReference -> Collaborator -> Html Msg
collaboratorCaption items collaborator =
  case collaborator of
    Collaborator.BoundedContext bc ->
      items
      |> List.filterMap (\r ->
        case r of 
          BoundedContext bcr ->
            if bcr.id == bc then
              Just <|
                Html.div [] 
                  [ Html.h6 [ class "text-muted" ] [ text bcr.domain.name ]
                  , Html.span [] [ text bcr.name ] 
                  ]
            else
              Nothing
          _ ->
            Nothing
      )
      |> List.head
      |> Maybe.withDefault (text "Unknown Bounded Context")
    Collaborator.Domain d ->
      items
      |> List.filterMap (\r ->
        case r of 
          Domain dr ->
            if dr.id == d
            then Just (Html.span [] [ text dr.name ])
            else Nothing
          _ ->
            Nothing
      )
      |> List.head
      |> Maybe.withDefault (text "Unknown Domain" )
    Collaborator.ExternalSystem s ->
      Html.span
          []
          [ Html.h6 [ class "text-muted" ] [ text "External System" ]
          , Html.span [] [ text s] 
          ]
    Collaborator.Frontend s ->
      Html.span
          []
          [ Html.h6 [ class "text-muted" ] [ text "Frontend" ]
          , Html.span [] [ text s] 
          ]
    Collaborator.UserInteraction s ->
      Html.span
          []
          [ Html.h6 [ class "text-muted" ] [ text "User Interaction" ]
          , Html.span [] [ text s] 
          ]


      


viewCollaboration : ResolveCollaboratorCaption -> Collaboration -> Html Msg
viewCollaboration getCollaboratorCaption collaboration =
  let
    relationship = Connection.relationship collaboration
    description = Just ""-- Connection.description collaboration


    cols =
      case relationship of
        Symmetric t p1 _ ->
          [ Grid.col [] [ getCollaboratorCaption p1]
          , Grid.col [] 
            [ Html.h6 [] [ text <| translateSymmetricRelationship t ]
            , Html.span [] [ text <| Maybe.withDefault "" description ]
            ]
          ]
        UpstreamDownstream (CustomerSupplierRole { customer, supplier}) ->
          [ Grid.col [] [ getCollaboratorCaption customer ]
          , Grid.col [] 
            [ Html.h6[] [ text "CUS" ] ]
          , Grid.col [] 
            [ Html.span [] [ text <| Maybe.withDefault "" description ] ]
          , Grid.col [] 
            [ Html.h6[] [ text "SUP" ] ]
          ]
            
        UpstreamDownstream (UpstreamDownstreamRole (collaborator,upstreamType) (_,downstreamType)) ->
          [ Grid.col [] [ getCollaboratorCaption collaborator ]
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
          [ Grid.col [] [ getCollaboratorCaption p1 ]
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


captionAndDescription caption description =
  Html.span [] 
    [ text <| caption
    , Form.help [] [ text description ]
    ]


labelAndDescription caption abbreviation description =
    Radio.label [] [ captionAndDescription (caption ++ " (" ++ abbreviation ++ ")") description ]

specifyRelationshipType relationshipType =
  let
        
    symmetricConfiguration =
      let
        renderOption idValue label abbreviation descriptionText value =
          Radio.createAdvanced
            [ Radio.id idValue
            , Radio.onClick (SetRelationship (Just <| SymmetricCollaboration <| Just value))
            , Radio.checked (
                case relationshipType of
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
          , Radio.onClick (SetRelationship (Just <| SymmetricCollaboration Nothing))
          , Radio.checked (
              case relationshipType of
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
        , Radio.onClick (SetRelationship (Just <| CustomerSupplierCollaboration Nothing))
        , Radio.checked (
            case relationshipType of
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
                , Radio.checked (relationshipType == (Just <| CustomerSupplierCollaboration <| Just IsCustomer))
                , Radio.onClick (SetRelationship (Just <| CustomerSupplierCollaboration <| Just IsCustomer))
                , Radio.inline
                ]
                ( labelAndDescription "Customer" "CUS" "The collaborator is in the customer role" )
              , Radio.createAdvanced
                [ Radio.id "isSupplierOption"
                , Radio.checked (relationshipType == (Just <| CustomerSupplierCollaboration <| Just IsSupplier))
                , Radio.onClick (SetRelationship (Just <| CustomerSupplierCollaboration <| Just IsSupplier))
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
          case relationshipType of
            (Just (UpstreamCollaboration (Just up) _)) ->
              up == upstreamConfig
            _ ->
              False
        setUpstream upstreamConfig =
          case relationshipType of
            (Just (UpstreamCollaboration _ down)) ->
              SetRelationship (Just <| UpstreamCollaboration (Just upstreamConfig) down)
            _ ->
              SetRelationship (Just <| UpstreamCollaboration (Just upstreamConfig) Nothing)

        isDownstream downstreamConfig =
          case relationshipType of
            (Just (UpstreamCollaboration _ (Just down))) ->
              down == downstreamConfig
            _ ->
              False
        setDownstream downstreamConfig =
          case relationshipType of
            (Just (UpstreamCollaboration up _)) ->
              SetRelationship (Just <| UpstreamCollaboration up (Just downstreamConfig))
            _ ->
              SetRelationship (Just <| UpstreamCollaboration Nothing (Just downstreamConfig))

        options = 
          Radio.label [] 
            [ captionAndDescription "Upstream" "The collaborator is upstream and I depend on changes."
            , Html.div [ class "row" ]
              [ Form.group [ Form.attrs [ class "col"]  ]
                ( Html.span[] [ text "Describe the collaborator:" ]
                :: Radio.radioList "upstream-upstreamOptions"
                  [ Radio.createAdvanced
                    [ Radio.id "upstream-upstreamOption"
                    , Radio.checked (isUpstream Upstream)
                    , Radio.onClick (setUpstream Upstream)
                    ]
                    ( labelAndDescription "Upstream" "US" "The collaborator is just Upstream" )
                  , Radio.createAdvanced
                    [ Radio.id "upstream-publishedLanguageOption"
                    , Radio.checked (isUpstream PublishedLanguage)
                    , Radio.onClick (setUpstream PublishedLanguage)
                    ]
                    ( labelAndDescription "Published Language" "PL" "The collaborator is using a Published Language" )
                  , Radio.createAdvanced
                    [ Radio.id "upstream-openHostOption"
                    , Radio.checked (isUpstream OpenHost)
                    , Radio.onClick (setUpstream OpenHost)
                    ]
                    ( labelAndDescription "Open Host Service" "OHS" "The collaborator is providing an Open Host Service" )
                  ]
                )
              , Form.group [ Form.attrs [ class "col"]  ]
                ( Html.span[] [ text "Describe your relationship with the collaborator:" ]
                :: Radio.radioList "upstream-downstreamOptions"
                  [ Radio.createAdvanced
                    [ Radio.id "upstream-downstreamOption"
                    , Radio.checked (isDownstream Downstream)
                    , Radio.onClick (setDownstream Downstream)
                    ]
                    ( labelAndDescription "Downstream" "DS" "I'm just Downstream" )
                  , Radio.createAdvanced
                    [ Radio.id "upstream-aclOption"
                    , Radio.checked (isDownstream AntiCorruptionLayer)
                    , Radio.onClick (setDownstream AntiCorruptionLayer)
                    ]
                    ( labelAndDescription "Anti-Corruption-Layer" "ACL" "I'm using an Anti-Corruption-Layer to shield me from changes" )
                  , Radio.createAdvanced
                    [ Radio.id "upstream-cfOption"
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
          [ Radio.id "upstream-upstreamType"
          , Radio.onClick (SetRelationship (Just <| UpstreamCollaboration Nothing Nothing))
          , Radio.checked (
              case relationshipType of
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
          case relationshipType of
            (Just (DownstreamCollaboration _ (Just up))) ->
              up == upstreamConfig
            _ ->
              False
        setUpstream upstreamConfig =
          case relationshipType of
            (Just (DownstreamCollaboration down _ )) ->
              SetRelationship (Just <| DownstreamCollaboration down (Just upstreamConfig))
            _ ->
              SetRelationship (Just <| DownstreamCollaboration Nothing (Just upstreamConfig) )

        isDownstream downstreamConfig =
          case relationshipType of
            (Just (DownstreamCollaboration (Just down) _ )) ->
              down == downstreamConfig
            _ ->
              False
        setDownstream downstreamConfig =
          case relationshipType of
            (Just (DownstreamCollaboration _ up)) ->
              SetRelationship (Just <| DownstreamCollaboration (Just downstreamConfig) up)
            _ ->
              SetRelationship (Just <| DownstreamCollaboration (Just downstreamConfig) Nothing)

        options = 
          Radio.label [] 
            [ captionAndDescription "Downstream" "The collaborator is downstream and they depend on my changes."
            , Html.div [ class "row" ]
              [ Form.group [ Form.attrs [ class "col"] ]
                ( Html.span[] [ text "Describe the collaborator:" ]
                :: Radio.radioList "downstream-downstreamOptions"
                  [ Radio.createAdvanced
                    [ Radio.id "downstream-downstreamOption"
                    , Radio.checked (isDownstream Downstream)
                    , Radio.onClick (setDownstream Downstream)
                    ]
                    ( labelAndDescription "Downstream" "DS" "The collaborator is just Downstream" )
                  , Radio.createAdvanced
                    [ Radio.id "downstream-aclOption"
                    , Radio.checked (isDownstream AntiCorruptionLayer)
                    , Radio.onClick (setDownstream AntiCorruptionLayer)
                    ]
                    ( labelAndDescription "Anti-Corruption-Layer" "ACL" "The collaborator is using an Anti-Corruption-Layer to shield from my changes" )
                  , Radio.createAdvanced
                    [ Radio.id "downstream-cfOption"
                    , Radio.checked (isDownstream Conformist)
                    , Radio.onClick (setDownstream Conformist)
                    ]
                    ( labelAndDescription "Conformist" "CF" "The collaborator is Conformist to my upstream changes" )
                  ]
                )
              , Form.group [ Form.attrs [ class "col"] ]
                ( Html.span[] [ text "Describe your relationship with the collaborator:" ]
                :: Radio.radioList "downstream-upstreamOptions"
                  [ Radio.createAdvanced
                    [ Radio.id "downstream-upstreamOption"
                    , Radio.checked (isUpstream Upstream)
                    , Radio.onClick (setUpstream Upstream)
                    ]
                    ( labelAndDescription "Upstream" "US" "I'm just Upstream" )
                  , Radio.createAdvanced
                    [ Radio.id "downstream-publishedLanguageOption"
                    , Radio.checked (isUpstream PublishedLanguage)
                    , Radio.onClick (setUpstream PublishedLanguage)
                    ]
                    ( labelAndDescription "Published Language" "PL" "I'm using a Published Language" )
                  , Radio.createAdvanced
                    [ Radio.id "downstream-openHostOption"
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
          [ Radio.id "downstream-upstreamType"
          , Radio.onClick (SetRelationship (Just <| DownstreamCollaboration Nothing Nothing))
          , Radio.checked (
              case relationshipType of
                Just (DownstreamCollaboration _ _) ->
                  True
                _ ->
                  False
            )
          ]
          options

    unknownConfiguration =
      Radio.createAdvanced
        [ Radio.id "uknownType"
        , Radio.onClick (SetRelationship (Just UnknownCollaboration))
        , Radio.checked (relationshipType == Just UnknownCollaboration)
        ]
        ( labelAndDescription  "Unkown" "?" "Is the exact description of the relationship unknown or you are not sure how to describe it."
        )

  in
    Radio.radioList "collaborationTypeSelection"
      [ unknownConfiguration
      , symmetricConfiguration
      , customerSupplierConfiguration
      , upstreamConfiguration
      , downstreamConfiguration
      ]

buildFields  : List CollaboratorReference -> CollaborationEdit -> List (Html CollaborationTypeMsg)
buildFields dependencies model =
  let
   
    
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
    
  in
    [ Fieldset.config
      |> Fieldset.legend [] [ text "Collaborator"]
      |> Fieldset.children
        [ text "Please select the collaborator of the connection"
        , collaboratorSelection
        ]
      |> Fieldset.view
    -- , Fieldset.config
    --   |> Fieldset.legend [] [ text "Collaboration Type"]
    --   |> Fieldset.children
    --     [ Html.div []
    --         (specifyRelationshipType model.relationship)
    --     ]
    --   |> Fieldset.view
    , Fieldset.config
      |> Fieldset.legend [] [ text "Description" ]
      |> Fieldset.children
        [ text "An optional description of the relationship and collaboration."
        , Textarea.textarea 
          [ Textarea.id "description"
          , Textarea.rows 3
          , Textarea.value model.description
          , Textarea.onInput SetDescription
          ]
        ]
      |> Fieldset.view
    ]
    

viewAddConnection : List CollaboratorReference -> CollaborationEdit -> Html Msg
viewAddConnection dependencies model =
  Html.form []
    ( List.append
      ( buildFields dependencies model
        |> List.map (Html.map CollaborationMsg)
      ) 
      [ Button.submitButton
        [ Button.primary
        , Button.disabled (model.collaborator == Nothing)
        , model.collaborator 
          |> Maybe.map (\c -> AddInboundConnection2 c model.description)
          |> Maybe.map Button.onClick
          |> Maybe.withDefault (Button.attrs [])
        ]
        [ text "Add inbound connection" ]
      , Button.submitButton
        [ Button.primary
        , Button.disabled (model.collaborator == Nothing)
        , model.collaborator 
          |> Maybe.map (\c -> AddOutboundConnection2 c model.description)
          |> Maybe.map Button.onClick
          |> Maybe.withDefault (Button.attrs [])
        ]
        [ text "Add outbound connection" ]
      ]
    )

viewConnection : List CollaboratorReference -> String -> List Collaboration -> Html Msg
viewConnection items title collaborations =
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
      (collaborations
      |> List.map (viewCollaboration (collaboratorCaption items)))
    ]

viewInboundConnection : ResolveCollaboratorCaption -> List Collaboration2 -> Html Msg
viewInboundConnection resolveCaption collaborations =
  let
 
    inboundCollaborator collaboration = 
      let
        description = Connection.description collaboration
        initiator = Connection.initiator collaboration
        
      in
        Grid.row 
          [ Row.attrs [ Border.top, Spacing.mb2, Spacing.pt1 ] ]
          [ Grid.col [] [ resolveCaption initiator ]
          , Grid.col [] [ text <| Maybe.withDefault "" description ]
          , Grid.col [ Col.xs2 ]
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
  in div []
    [ Html.h6
      [ class "text-center", Spacing.p2 ]
      [ Html.strong [] [ text "Inbound Connection" ] ]
    , Grid.row []
      [ Grid.col [] [ Html.h6 [] [ text "Name"] ]
      -- , Grid.col [] [ Html.h6 [] [ text "Initiator-Type"] ]
      , Grid.col [] [ Html.h6 [] [ text "Description"] ]
      -- , Grid.col [] [ Html.h6 [] [ text "Recipient-Type"] ]
      , Grid.col [Col.xs2] []
      ]
    , div []
      (collaborations
      |> List.map (inboundCollaborator))
    ]

viewOutboundConnection : ResolveCollaboratorCaption -> List Collaboration2 -> Html Msg
viewOutboundConnection resolveCaption collaborations =
  let
 
    outboundCollaborator collaboration = 
      Grid.row 
        [ Row.attrs [ Border.top, Spacing.mb2, Spacing.pt1 ] ]
        [ Grid.col [] [ text <| Maybe.withDefault "" (Connection.description collaboration) ]
        , Grid.col [] [ collaboration |> Connection.recipient |> resolveCaption ]
        , Grid.col [ Col.xs2 ]
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
  in div []
    [ Html.h6
      [ class "text-center", Spacing.p2 ]
      [ Html.strong [] [ text "Outbound Connection" ] ]
    , Grid.row []
      [ 
      -- , Grid.col [] [ Html.h6 [] [ text "Initiator-Type"] ]
        Grid.col [] [ Html.h6 [] [ text "Description"] ]
      , Grid.col [] [ Html.h6 [] [ text "Name"] ]
      -- , Grid.col [] [ Html.h6 [] [ text "Recipient-Type"] ]
      , Grid.col [Col.xs2] []
      ]
    , div []
      (collaborations
      |> List.map (outboundCollaborator))
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
        [ viewConnection model.availableDependencies "Inbound Connection" model.inboundCollaboration ]
      , Grid.col []
        [ viewConnection model.availableDependencies "Outbound Connection" model.outboundCollaboration ]
      ]
     , Grid.row []
      [ Grid.col []
        [ viewInboundConnection (collaboratorCaption model.availableDependencies) model.inboundCollaboration2 ]
      , Grid.col []
        [ viewOutboundConnection (collaboratorCaption model.availableDependencies) model.outboundCollaboration2 ]
      ]
    , Grid.simpleRow
      [ Grid.col []
          [ viewAddConnection model.availableDependencies model.newCollaborations]

      ]
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
      |> List.filterMap (Connection.isCollaborator (Collaborator.BoundedContext context))
  in Http.get
    { url = Api.collaborations |> Api.url config |> Url.toString
    , expect = Http.expectJson ConnectionsLoaded (Decode.map filterConnections (Decode.list Connection.modelDecoder2))
    }