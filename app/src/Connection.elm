module Connection exposing (
    CollaborationId, Collaborator(..), Collaboration, Collaborations, CollaborationType(..), UpstreamCollaborator, DownstreamCollaborator,
    RelationshipType(..), SymmetricRelationship(..), UpstreamRelationship(..), DownstreamRelationship(..),UpstreamDownstreamRelationship(..),
    CollaborationDefinition(..),
    noCollaborations, defineInboundCollaboration, defineOutboundCollaboration,
    isCollaborator,
    relationship, description,
    symmetricRelationshipFromString, symmetricRelationshipToString, downstreamRelationshipFromString, downstreamRelationshipToString,
    modelEncoder, modelDecoder)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Url
import Http

import Api exposing(ApiResult)

import BoundedContext.BoundedContextId exposing (BoundedContextId)
import Domain
import Domain.DomainId as Domain
import BoundedContext.Dependency exposing (relationshipParser)
import BoundedContext.Dependency exposing (relationshipToString)

type CollaborationId = 
  CollaborationId Int

type SymmetricRelationship
  = SharedKernel
  | Partnership
  | SeparateWays
  | BigBallOfMud

type UpstreamRelationship 
  = Upstream
  | PublishedLanguage
  | OpenHost

type DownstreamRelationship 
  = Downstream
  | AntiCorruptionLayer
  | Conformist

type UpstreamDownstreamRelationship
  = CustomerSupplierRole CustomerSupplierInformation
  | UpstreamDownstreamRole UpstreamCollaborator DownstreamCollaborator

type alias CustomerSupplierInformation = { customer: Collaborator, supplier: Collaborator }

type alias UpstreamCollaborator =  (Collaborator, UpstreamRelationship)
type alias DownstreamCollaborator =  (Collaborator, DownstreamRelationship)


type RelationshipType
  = Symmetric SymmetricRelationship Collaborator Collaborator
  | UpstreamDownstream UpstreamDownstreamRelationship
  | Octopus UpstreamCollaborator (List DownstreamCollaborator)
  | Unknown Collaborator Collaborator

type Collaborator
  = BoundedContext BoundedContextId
  | Domain Domain.DomainId
  | ExternalSystem String
  | Frontend String
  | UserInteraction String

type Collaboration =
  Collaboration CollaborationInternal

type alias CollaborationInternal =
    { id : CollaborationId
    , relationship : RelationshipType
    , description : Maybe String 
    }

type alias Collaborations = List Collaboration

type CollaborationType
  = Inbound Collaboration
  | Outbound Collaboration

type CollaborationDefinition
  = SymmetricCollaboration SymmetricRelationship Collaborator
  | UpstreamCollaboration UpstreamRelationship DownstreamCollaborator
  | ACustomerOfCollaboration Collaborator
  | ASupplierForCollaboration Collaborator
  | DownstreamCollaboration DownstreamRelationship UpstreamCollaborator
  | UnknownCollaboration Collaborator


noCollaborations : Collaborations
noCollaborations = []

defineInboundCollaboration : Api.Configuration -> BoundedContextId -> CollaborationDefinition -> String -> ApiResult Collaboration msg
defineInboundCollaboration url context collaboration descriptionText =
  let
    api =
      Api.communication

    recipient = BoundedContext context
    relationshipType =
      case collaboration of
        SymmetricCollaboration t c ->
          Symmetric t c recipient
        UpstreamCollaboration t d ->
          UpstreamDownstream <| UpstreamDownstreamRole (recipient,t) d
        DownstreamCollaboration t u ->
          UpstreamDownstream <| UpstreamDownstreamRole u (recipient,t)
        ACustomerOfCollaboration sup ->
          UpstreamDownstream <| CustomerSupplierRole { customer = recipient, supplier = sup }
        ASupplierForCollaboration cus ->
          UpstreamDownstream <| CustomerSupplierRole { customer = cus, supplier = recipient }
        UnknownCollaboration c ->
          Unknown c recipient

    request toMsg =
      Http.post
      { url = api |> Api.url url |> Url.toString
      , body = Http.jsonBody <|
              Encode.object 
                  [ ("relationship", relationshipCollaboratorEncoder relationshipType )
                  , ("description", Encode.string descriptionText)
                  ]
      , expect = Http.expectJson toMsg modelDecoder
      }
    in
      request

defineOutboundCollaboration : Api.Configuration -> BoundedContextId -> CollaborationDefinition -> String -> ApiResult Collaboration msg
defineOutboundCollaboration url context collaboration descriptionText =
  let
    api =
      Api.communication

    recipient = BoundedContext context
    relationshipType =
      case collaboration of
        SymmetricCollaboration t c ->
          Symmetric t c recipient
        UpstreamCollaboration t d ->
          UpstreamDownstream <| UpstreamDownstreamRole (recipient,t) d
        DownstreamCollaboration t u ->
          UpstreamDownstream <| UpstreamDownstreamRole u (recipient,t)
        ACustomerOfCollaboration sup ->
          UpstreamDownstream <| CustomerSupplierRole { customer = recipient, supplier = sup }
        ASupplierForCollaboration cus ->
          UpstreamDownstream <| CustomerSupplierRole { customer = cus, supplier = recipient }
        UnknownCollaboration c ->
          Unknown c recipient

    request toMsg =
      Http.post
      { url = api |> Api.url url |> Url.toString
      , body = Http.jsonBody <|
              Encode.object 
                  [ ("relationship", relationshipCollaboratorEncoder relationshipType )
                  , ("description", Encode.string descriptionText)
                  ]
      , expect = Http.expectJson toMsg modelDecoder
      }
    in
      request


isInboundCollaboratoration : Collaborator -> Collaboration -> Bool
isInboundCollaboratoration collaborator (Collaboration collaboration) =
  case collaboration.relationship of
    Symmetric _ _ p ->
      p == collaborator
    UpstreamDownstream (CustomerSupplierRole { customer, supplier }) ->
      customer == collaborator
    UpstreamDownstream (UpstreamDownstreamRole (up,_) _) ->
      up == collaborator
    Octopus (up,_) _ ->
      up == collaborator
    Unknown _ p ->
      p == collaborator


isOutboundCollaboratoration : Collaborator -> Collaboration -> Bool
isOutboundCollaboratoration collaborator (Collaboration collaboration) =
  case collaboration.relationship of
    Symmetric _ p _ ->
      p == collaborator
    UpstreamDownstream (CustomerSupplierRole { customer, supplier }) ->
      supplier == collaborator
    UpstreamDownstream (UpstreamDownstreamRole _ (down,_)) ->
      down == collaborator
    Octopus _ downs ->
      downs
      |> List.any (\(down,_) -> down == collaborator)
    Unknown p _ ->
      p == collaborator


isCollaborator : Collaborator -> Collaboration -> Maybe CollaborationType
isCollaborator collaborator collaboration =
  case (isInboundCollaboratoration collaborator collaboration, isOutboundCollaboratoration collaborator collaboration) of
    (True, False) -> 
      Just <| Inbound collaboration
    (False, True) ->
      Just <| Outbound collaboration
    _ ->
      Nothing

relationship : Collaboration -> RelationshipType
relationship (Collaboration collaboration) =
  collaboration.relationship

description : Collaboration -> Maybe String
description (Collaboration collaboration) =
  collaboration.description

idToString : CollaborationId -> String
idToString (CollaborationId contextId) =
  String.fromInt contextId

idFromString : String -> Maybe CollaborationId
idFromString value =
  value
  |> String.toInt
  |> Maybe.map CollaborationId

idDecoder : Decoder CollaborationId
idDecoder =
  Decode.map CollaborationId Decode.int

idFieldDecoder : Decoder CollaborationId
idFieldDecoder =
  Decode.field "id" idDecoder

modelDecoder : Decoder Collaboration
modelDecoder =
  ( Decode.succeed CollaborationInternal
    |> JP.custom idFieldDecoder
    |> JP.required "relationship" relationshipCollaboratorDecoder
    |> JP.required "description" (Decode.nullable Decode.string)
  ) |> Decode.map Collaboration

collaboratorEncoder : Collaborator -> Encode.Value
collaboratorEncoder collaborator =
    let
        encoder =
            case collaborator of
                BoundedContext bcId -> 
                    ( "boundedContext", BoundedContext.BoundedContextId.idEncoder bcId)
                Domain domainId ->
                    ( "domain", Domain.idEncoder domainId)
                ExternalSystem external ->
                    ( "externalSystem", Encode.string external)
                Frontend frontend ->
                    ( "frontend", Encode.string frontend)
                UserInteraction user ->
                    ( "userInteraction", Encode.string user)
    in
        Encode.object [ encoder ]

collaboratorDecoder : Decoder Collaborator
collaboratorDecoder =
    Decode.oneOf
        [ Decode.map BoundedContext <| Decode.field "boundedContext" BoundedContext.BoundedContextId.idDecoder
        , Decode.map Domain <| Decode.field "domain" Domain.idDecoder
        , Decode.map ExternalSystem <| Decode.field "externalSystem" Decode.string
        , Decode.map Frontend <| Decode.field "frontend" Decode.string
        , Decode.map UserInteraction <| Decode.field "userInteraction" Decode.string
        ]


upstreamCollaboratorEncoder : UpstreamCollaborator -> Encode.Value
upstreamCollaboratorEncoder (collaborator, upstreamType) =
  Encode.object
    [ ( "collaborator", collaboratorEncoder collaborator)
    , ( "type"
      , Encode.string <|
          case upstreamType of
            Upstream -> "Upstream"
            PublishedLanguage -> "PublishedLanguage"
            OpenHost -> "OpenHost"       
      )
    ]

upstreamCollaboratorDecoder : Decoder UpstreamCollaborator
upstreamCollaboratorDecoder =
  Decode.map2 Tuple.pair
    ( Decode.field "collaborator" collaboratorDecoder)
    ( Decode.field "type" Decode.string |> Decode.andThen (\upstreamType ->
        case upstreamType of
          "Upstream" -> Decode.succeed Upstream
          "PublishedLanguage" -> Decode.succeed PublishedLanguage
          "OpenHost" -> Decode.succeed OpenHost
          x  -> Decode.fail <| "could not decode pattern: " ++ x
      )
    )

downstreamCollaboratorEncoder : DownstreamCollaborator -> Encode.Value
downstreamCollaboratorEncoder (collaborator, downstreamType) =
  Encode.object
    [ ( "collaborator", collaboratorEncoder collaborator )
    , ( "type", Encode.string <| downstreamRelationshipToString downstreamType )
    ]

downstreamRelationshipToString : DownstreamRelationship -> String
downstreamRelationshipToString downstreamType =
  case downstreamType of
    Downstream -> "Downstream"
    AntiCorruptionLayer -> "AntiCorruptionLayer"
    Conformist -> "Conformist"

downstreamRelationshipFromString : String -> Result String DownstreamRelationship
downstreamRelationshipFromString downstreamType =
  case downstreamType of
    "Downstream" -> Ok Downstream
    "AntiCorruptionLayer" -> Ok AntiCorruptionLayer
    "Conformist" -> Ok Conformist
    x  -> Err x

downstreamCollaboratorDecoder : Decoder DownstreamCollaborator
downstreamCollaboratorDecoder =
  Decode.map2 Tuple.pair
    ( Decode.field "collaborator" collaboratorDecoder )
    ( Decode.field "type" Decode.string |> Decode.andThen ( resultToDecoder downstreamRelationshipFromString ) )

upstreamDownstreamEncoder : UpstreamDownstreamRelationship -> Encode.Value
upstreamDownstreamEncoder upstreamDownstreamRealtionship =
  case upstreamDownstreamRealtionship of
    CustomerSupplierRole { customer, supplier} ->
      Encode.object
        [ ( "customer",  collaboratorEncoder customer )
        , ( "supplier", collaboratorEncoder supplier )
        ]
    UpstreamDownstreamRole upstream downstream ->
      Encode.object
        [ ( "upstream", upstreamCollaboratorEncoder upstream )
        , ( "downstream", downstreamCollaboratorEncoder downstream )
        ] 

relationshipCollaboratorEncoder : RelationshipType -> Encode.Value
relationshipCollaboratorEncoder relationshipType =
  case relationshipType of
    Symmetric symmetricType participant1 participant2 ->
      Encode.object
        [ ( "symmetric", Encode.string <| symmetricRelationshipToString symmetricType )
        , ( "participant1", collaboratorEncoder participant1 )
        , ( "participant2", collaboratorEncoder participant2 )
        ]
    UpstreamDownstream upstreamDownstreamType ->
      upstreamDownstreamEncoder upstreamDownstreamType
    Octopus upstream downstreams ->
      Encode.object
        [ ( "upstream", upstreamCollaboratorEncoder upstream )
        , ( "downstreams", downstreams |> Encode.list downstreamCollaboratorEncoder )
        ]
    Unknown participant1 participant2 ->
      Encode.object
        [ ( "participant1", collaboratorEncoder participant1 )
        , ( "participant2", collaboratorEncoder participant2 )
        ]

symmetricRelationshipFromString : String -> Result String SymmetricRelationship
symmetricRelationshipFromString symmetricType =
  case symmetricType of
    "SharedKernel" -> Ok SharedKernel
    "SeparateWays" -> Ok SeparateWays
    "BigBallOfMud" -> Ok BigBallOfMud
    "Partnership" -> Ok Partnership
    x  -> Err x

symmetricRelationshipToString : SymmetricRelationship -> String
symmetricRelationshipToString symmetricType =
 case symmetricType of
    SharedKernel -> "SharedKernel"
    SeparateWays -> "SeparateWays"
    BigBallOfMud -> "BigBallOfMud"
    Partnership -> "Partnership"

collaboratorFieldDecoder fieldName =
   Decode.field fieldName collaboratorDecoder 

upstreamDownstreamDecoder : Decoder UpstreamDownstreamRelationship
upstreamDownstreamDecoder =
  let
    customerSupplierDecode =
      Decode.map2 CustomerSupplierInformation
        ( collaboratorFieldDecoder "customer" )
        ( collaboratorFieldDecoder "supplier" )
  in
    Decode.oneOf
      [ Decode.map CustomerSupplierRole
          customerSupplierDecode
      , Decode.map2 UpstreamDownstreamRole
        ( Decode.field "upstream" upstreamCollaboratorDecoder )
        ( Decode.field "downstream" downstreamCollaboratorDecoder )
      ]


relationshipCollaboratorDecoder : Decoder RelationshipType
relationshipCollaboratorDecoder =
  Decode.oneOf 
    [ Decode.map3 Symmetric
      ( Decode.field 
          "symmetric" 
          Decode.string 
          |> Decode.andThen (resultToDecoder symmetricRelationshipFromString)
      )      
      ( collaboratorFieldDecoder "participant1" )
      ( collaboratorFieldDecoder "participant2" )
    , Decode.map UpstreamDownstream
        upstreamDownstreamDecoder
    , Decode.map2 Octopus
      ( Decode.field "upstream" upstreamCollaboratorDecoder )
      ( Decode.field "downstreams" ( Decode.list downstreamCollaboratorDecoder ) )
    , Decode.map2 Unknown
      ( collaboratorFieldDecoder "participant1" )
      ( collaboratorFieldDecoder "participant2" )
    ]


modelEncoder : Collaboration -> Encode.Value
modelEncoder (Collaboration collaboration) =
  Encode.object
    [ ("relationship", relationshipCollaboratorEncoder collaboration.relationship)
    , ("description", maybeEncoder Encode.string collaboration.description)
    ]

maybeEncoder : (t -> Encode.Value) -> Maybe t -> Encode.Value
maybeEncoder encoder value =
  case value of
    Just v -> encoder v
    Nothing -> Encode.null

maybeStringEncoder : (t -> String) -> Maybe t -> Encode.Value
maybeStringEncoder encoder value =
  maybeEncoder (encoder >> Encode.string) value

maybeStringDecoder : (String -> Maybe v) -> Decoder (Maybe v)
maybeStringDecoder parser =
  Decode.oneOf
    [ Decode.null Nothing
    , Decode.map parser Decode.string
    ]

resultToDecoder : (a -> Result String b) -> (a -> Decoder b)
resultToDecoder convert =
  \value ->
    case convert value of
      Ok v -> Decode.succeed v
      Err e -> Decode.fail <| "Could not decode pattern:" ++ e
   