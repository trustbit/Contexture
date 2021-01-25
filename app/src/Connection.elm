module Connection exposing (
    Collaboration,Collaboration2, Collaborations, CollaborationType(..), 
    CollaborationDefinition(..),
    noCollaborations, defineInboundCollaboration, defineInboundCollaboration2, defineOutboundCollaboration, defineOutboundCollaboration2,
    endCollaboration,
    isCollaborator,
    relationship, description, initiator, recipient,
    modelDecoder,modelDecoder2)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Url
import Http

import Api exposing(ApiResult)

import ContextMapping.CollaborationId as ContextMapping exposing (CollaborationId)
import ContextMapping.Collaborator as Collaborator exposing (Collaborator)
import ContextMapping.RelationshipType as RelationshipType exposing (RelationshipType)
import Domain
import Domain.DomainId as Domain
import BoundedContext.BoundedContextId exposing (BoundedContextId)
import BoundedContext.Dependency exposing (relationshipParser)
import BoundedContext.Dependency exposing (relationshipToString)

import Page.ChangeKey exposing (Msg)


type Collaboration
  = Collaboration CollaborationInternal
type Collaboration2 
  -- = Collaboration CollaborationInternal
  = Collaboration2 CollaborationInternal2
  

type alias CollaborationInternal =
    { id : CollaborationId
    , relationship : RelationshipType
    , description : Maybe String
    , communicationInitiator : Collaborator
    }

type alias CollaborationInternal2 =
    { id : CollaborationId
    , description : Maybe String
    , initiator : Collaborator
    , recipient : Collaborator
    -- , relationship : Maybe RelationshipType
    }


type alias Collaborations = List Collaboration

type CollaborationType
  = Inbound Collaboration2
  | Outbound Collaboration2

type CollaborationDefinition
  = SymmetricCollaboration RelationshipType.SymmetricRelationship Collaborator
  | UpstreamCollaboration RelationshipType.UpstreamRelationship RelationshipType.DownstreamCollaborator
  | ACustomerOfCollaboration Collaborator
  | ASupplierForCollaboration Collaborator
  | DownstreamCollaboration RelationshipType.DownstreamRelationship RelationshipType.UpstreamCollaborator
  | UnknownCollaboration Collaborator


noCollaborations : Collaborations
noCollaborations = []

endCollaboration : Api.Configuration -> CollaborationId -> ApiResult () msg
endCollaboration url id =
  let
    api =
      Api.collaboration id

    request toMsg =
      Http.request
      { method = "DELETE"
      , url = api |> Api.url url |> Url.toString
      , body = Http.emptyBody
      , expect = Http.expectJson toMsg (Decode.succeed ())
      , timeout = Nothing
      , tracker = Nothing
      , headers = []
      }
  in
    request


defineInboundCollaboration2 : Api.Configuration -> BoundedContextId -> Collaborator -> String -> ApiResult Collaboration2 msg
defineInboundCollaboration2 url context connectionInitiator descriptionText =
  let
    api =
      Api.collaborations

    connectionRecipient = Collaborator.BoundedContext context

    request toMsg =
      Http.post
      { url = api |> Api.url url |> Url.toString
      , body = Http.jsonBody <|
              modelEncoder2 connectionInitiator connectionRecipient (if String.isEmpty descriptionText then Nothing else Just descriptionText) 
      , expect = Http.expectJson toMsg modelDecoder2
      }
    in
      request

defineOutboundCollaboration2 : Api.Configuration -> BoundedContextId -> Collaborator -> String -> ApiResult Collaboration2 msg
defineOutboundCollaboration2 url context connectionRecipient descriptionText =
  let
    api =
      Api.collaborations

    connectionInitiator = Collaborator.BoundedContext context

    request toMsg =
      Http.post
      { url = api |> Api.url url |> Url.toString
      , body = Http.jsonBody <|
              modelEncoder2 connectionInitiator connectionRecipient (if String.isEmpty descriptionText then Nothing else Just descriptionText) 
      , expect = Http.expectJson toMsg modelDecoder2
      }
    in
      request

defineInboundCollaboration : Api.Configuration -> BoundedContextId ->  CollaborationDefinition -> String -> ApiResult Collaboration msg
defineInboundCollaboration url context collaboration descriptionText =
  let
    api =
      Api.collaborations

    connectionRecipient = Collaborator.BoundedContext context
    (relationshipType,communicationInitator) =
      case collaboration of
        SymmetricCollaboration symmetricType collaborator ->
          (RelationshipType.Symmetric symmetricType collaborator connectionRecipient,collaborator)
        UpstreamCollaboration upstreamType downstreamCollaborator ->
          (RelationshipType.UpstreamDownstream <| RelationshipType.UpstreamDownstreamRole (connectionRecipient,upstreamType) downstreamCollaborator,Tuple.first downstreamCollaborator)
        DownstreamCollaboration downstreamType upstreamCollaborator ->
          (RelationshipType.UpstreamDownstream <| RelationshipType.UpstreamDownstreamRole upstreamCollaborator (connectionRecipient,downstreamType),Tuple.first upstreamCollaborator)
        ACustomerOfCollaboration sup ->
          (RelationshipType.UpstreamDownstream <| RelationshipType.CustomerSupplierRole { customer = connectionRecipient, supplier = sup }, sup)
        ASupplierForCollaboration cus ->
          (RelationshipType.UpstreamDownstream <| RelationshipType.CustomerSupplierRole { customer = cus, supplier = connectionRecipient }, cus)
        UnknownCollaboration collaborator ->
          (RelationshipType.Unknown collaborator connectionRecipient,collaborator)

    request toMsg =
      Http.post
      { url = api |> Api.url url |> Url.toString
      , body = Http.jsonBody <|
              modelEncoder relationshipType (if String.isEmpty descriptionText then Nothing else Just descriptionText) communicationInitator
      , expect = Http.expectJson toMsg modelDecoder
      }
    in
      request

defineOutboundCollaboration : Api.Configuration -> BoundedContextId -> CollaborationDefinition -> String -> ApiResult Collaboration msg
defineOutboundCollaboration url context collaboration descriptionText =
  let
    api =
      Api.collaborations

    connectionRecipient = Collaborator.BoundedContext context
    relationshipType =
      case collaboration of
        SymmetricCollaboration t c ->
          RelationshipType.Symmetric t c connectionRecipient
        UpstreamCollaboration t d ->
          RelationshipType.UpstreamDownstream <| RelationshipType.UpstreamDownstreamRole (connectionRecipient,t) d
        DownstreamCollaboration t u ->
          RelationshipType.UpstreamDownstream <| RelationshipType.UpstreamDownstreamRole u (connectionRecipient,t)
        ACustomerOfCollaboration sup ->
          RelationshipType.UpstreamDownstream <| RelationshipType.CustomerSupplierRole { customer = connectionRecipient, supplier = sup }
        ASupplierForCollaboration cus ->
          RelationshipType.UpstreamDownstream <| RelationshipType.CustomerSupplierRole { customer = cus, supplier = connectionRecipient }
        UnknownCollaboration c ->
          RelationshipType.Unknown c connectionRecipient

    request toMsg =
      Http.post
      { url = api |> Api.url url |> Url.toString
      , body = Http.jsonBody <|
            modelEncoder relationshipType (if String.isEmpty descriptionText then Nothing else Just descriptionText) connectionRecipient
      , expect = Http.expectJson toMsg modelDecoder
      }
    in
      request


isInboundCollaboratoration : Collaborator -> Collaboration2 -> Bool
isInboundCollaboratoration collaborator (Collaboration2 collaboration) =
  collaboration.recipient == collaborator
    

areCollaborating : Collaborator -> Collaboration2 -> Bool
areCollaborating collaborator (Collaboration2 collaboration) =
  -- case collaboration.relationship of
  --   Symmetric _ p1 p2 ->
  --     p1 == collaborator || p2 == collaborator
  --   UpstreamDownstream (CustomerSupplierRole { customer, supplier }) ->
  --     supplier == collaborator || customer == collaborator
  --   UpstreamDownstream (UpstreamDownstreamRole (up,_) (down,_)) ->
  --     down == collaborator || up == collaborator
  --   Octopus (up,_) downs ->
  --     up == collaborator || (downs |> List.any (\(down,_) -> down == collaborator))
  --   Unknown p1 p2 ->
  --     p1 == collaborator || p2 == collaborator
  collaboration.initiator == collaborator || collaboration.recipient == collaborator


isCollaborator : Collaborator -> Collaboration2 -> Maybe CollaborationType
isCollaborator collaborator collaboration =
  case (areCollaborating collaborator collaboration, isInboundCollaboratoration collaborator collaboration) of
    (True, True) -> 
      Just <| Inbound collaboration
    (True, False) ->
      Just <| Outbound collaboration
    _ ->
      Nothing

relationship : Collaboration -> RelationshipType
relationship (Collaboration collaboration) =
  collaboration.relationship

initiator : Collaboration2 -> Collaborator
initiator (Collaboration2 collaboration) =
  collaboration.initiator


recipient : Collaboration2 -> Collaborator
recipient (Collaboration2 collaboration) =
  collaboration.recipient
  

description : Collaboration2 -> Maybe String
description (Collaboration2 collaboration) =
  collaboration.description

idFieldDecoder : Decoder CollaborationId
idFieldDecoder =
  Decode.field "id" ContextMapping.idDecoder

modelDecoder : Decoder Collaboration
modelDecoder =
  ( Decode.succeed CollaborationInternal
    |> JP.custom idFieldDecoder
    |> JP.required "relationship" RelationshipType.decoder
    |> JP.required "description" (Decode.nullable Decode.string)
    |> JP.required "communicationInitiator" Collaborator.decoder
  ) |> Decode.map Collaboration

modelDecoder2 : Decoder Collaboration2
modelDecoder2 =
  ( Decode.succeed CollaborationInternal2
    |> JP.custom idFieldDecoder
    |> JP.required "description" (Decode.nullable Decode.string)
    |> JP.required "initiator" Collaborator.decoder
    |> JP.required "recipient" Collaborator.decoder
  ) |> Decode.map Collaboration2

modelEncoder : RelationshipType -> Maybe String -> Collaborator -> Encode.Value
modelEncoder relationshipType descriptionValue communicationInitiator =
  Encode.object
    [ ("relationship", RelationshipType.encoder relationshipType)
    , ("description", maybeEncoder Encode.string descriptionValue)
    , ("communicationInitiator", Collaborator.encoder communicationInitiator)
    ]

modelEncoder2 : Collaborator -> Collaborator -> Maybe String -> Encode.Value
modelEncoder2 connectionInitiator connectionRecipient descriptionValue =
  Encode.object
    [ ("description", maybeEncoder Encode.string descriptionValue)
    , ("initiator", Collaborator.encoder connectionInitiator)
    , ("recipient", Collaborator.encoder connectionRecipient)
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