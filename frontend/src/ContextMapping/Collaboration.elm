module ContextMapping.Collaboration exposing (
    Collaboration, Collaborations, 
    noCollaborations,
    defineInboundCollaboration, defineOutboundCollaboration, defineRelationshipType,
    endCollaboration,
    areCollaborating,
    relationship, description, initiator, recipient, id, otherCollaborator,
    decoder)

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

type Collaboration
  = Collaboration CollaborationInternal


type alias CollaborationInternal =
    { id : CollaborationId
    , description : Maybe String
    , initiator : Collaborator
    , recipient : Collaborator
    , relationship : Maybe RelationshipType
    }


type alias Collaborations = List Collaboration


noCollaborations : Collaborations
noCollaborations = []

endCollaboration : Api.Configuration -> CollaborationId -> ApiResult CollaborationId msg
endCollaboration url collaborationId =
  let
    api =
      Api.collaboration collaborationId

    request toMsg =
      Http.request
      { method = "DELETE"
      , url = api |> Api.url url 
      , body = Http.emptyBody
      , expect = Http.expectJson toMsg (Decode.succeed collaborationId)
      , timeout = Nothing
      , tracker = Nothing
      , headers = []
      }
  in
    request


defineInboundCollaboration : Api.Configuration -> BoundedContextId -> Collaborator -> String -> ApiResult Collaboration msg
defineInboundCollaboration url context connectionInitiator descriptionText =
  let
    api =
      Api.collaborations

    connectionRecipient = Collaborator.BoundedContext context

    request toMsg =
      Http.post
      { url = api |> Api.url url  |> (\c -> c ++ "/inboundConnection")
      , body = Http.jsonBody <|
              modelEncoder
                connectionInitiator
                connectionRecipient
                (if String.isEmpty descriptionText then Nothing else Just descriptionText)
                Nothing
      , expect = Http.expectJson toMsg decoder
      }
    in
      request


defineOutboundCollaboration : Api.Configuration -> BoundedContextId -> Collaborator -> String -> ApiResult Collaboration msg
defineOutboundCollaboration url context connectionRecipient descriptionText =
  let
    api =
      Api.collaborations

    connectionInitiator = Collaborator.BoundedContext context

    request toMsg =
      Http.post
      { url = api |> Api.url url  |> (\c -> c ++ "/outboundConnection")
      , body = Http.jsonBody <|
              modelEncoder
                connectionInitiator
                connectionRecipient
                (if String.isEmpty descriptionText then Nothing else Just descriptionText)
                Nothing
      , expect = Http.expectJson toMsg decoder
      }
    in
      request


defineRelationshipType : Api.Configuration -> CollaborationId ->  RelationshipType -> ApiResult Collaboration msg
defineRelationshipType url collaboration relationshipType =
  let
    api =
      collaboration |> Api.collaboration

    request toMsg =
      Http.request
      { method = "POST"
      , url = api |> Api.url url  |> (\c -> c ++ "/relationship")
      , body = Http.jsonBody <|
          Encode.object [ ("relationshipType", RelationshipType.encoder relationshipType) ]
      , expect = Http.expectJson toMsg decoder
      , timeout = Nothing
      , tracker = Nothing
      , headers = []
      }
  in
    request


areCollaborating : Collaborator -> Collaboration -> Bool
areCollaborating collaborator (Collaboration collaboration) =
  collaboration.initiator == collaborator || collaboration.recipient == collaborator


id : Collaboration -> CollaborationId
id (Collaboration collaboration) =
  collaboration.id


relationship : Collaboration -> Maybe RelationshipType
relationship (Collaboration collaboration) =
  collaboration.relationship


initiator : Collaboration -> Collaborator
initiator (Collaboration collaboration) =
  collaboration.initiator


recipient : Collaboration -> Collaborator
recipient (Collaboration collaboration) =
  collaboration.recipient


otherCollaborator : Collaborator -> Collaboration -> Collaborator
otherCollaborator knownCollaborator (Collaboration collaboration) =
  if collaboration.recipient == knownCollaborator
  then collaboration.initiator
  else collaboration.recipient


description : Collaboration -> Maybe String
description (Collaboration collaboration) =
  collaboration.description


idFieldDecoder : Decoder CollaborationId
idFieldDecoder =
  Decode.field "id" ContextMapping.idDecoder


decoder : Decoder Collaboration
decoder =
  ( Decode.succeed CollaborationInternal
    |> JP.custom idFieldDecoder
    |> JP.optional "description" (Decode.nullable Decode.string) Nothing
    |> JP.required "initiator" Collaborator.decoder
    |> JP.required "recipient" Collaborator.decoder
    |> JP.optional "relationshipType" (Decode.nullable RelationshipType.decoder) Nothing
  ) |> Decode.map Collaboration


modelEncoder : Collaborator -> Collaborator -> Maybe String -> Maybe RelationshipType -> Encode.Value
modelEncoder connectionInitiator connectionRecipient descriptionValue relationshipType =
  Encode.object
    [ ("description", maybeEncoder Encode.string descriptionValue)
    , ("initiator", Collaborator.encoder connectionInitiator)
    , ("recipient", Collaborator.encoder connectionRecipient)
    , ("relationshipType", maybeEncoder RelationshipType.encoder relationshipType)
    ]


maybeEncoder : (t -> Encode.Value) -> Maybe t -> Encode.Value
maybeEncoder encoder value =
  case value of
    Just v -> encoder v
    Nothing -> Encode.null
