module Connection exposing (
    CollaborationId, Collaborator(..), Collaboration, Collaborations, CollaborationType(..),
    RelationshipType(..), SymmetricRelationship(..), UpstreamRelationship(..), DownstreamRelationship(..),
    noCollaborations, defineInboundCollaboration,
    isCollaborator,
    relationship, description,
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
  | Supplier

type DownstreamRelationship 
  = Downstream
  | AntiCorruptionLayer
  | Conformist
  | Customer

type alias UpstreamCollaborator =  (Collaborator, UpstreamRelationship)
type alias DownstreamCollaborator =  (Collaborator, DownstreamRelationship)

type RelationshipType
  = Symmetric SymmetricRelationship Collaborator Collaborator
  | UpstreamDownstream UpstreamCollaborator DownstreamCollaborator
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

type InboundCollaboration
  = SymmetricCollaboration SymmetricRelationship Collaborator
  | UpstreamCollaboration UpstreamRelationship DownstreamCollaborator
  | DownstreamCollaboration DownstreamRelationship UpstreamCollaborator
  | UnknownCollaboration Collaborator


noCollaborations : Collaborations
noCollaborations = []

defineInboundCollaboration : Api.Configuration -> BoundedContextId -> InboundCollaboration -> String -> ApiResult Collaboration msg
defineInboundCollaboration url context collaboration descriptionText =
  let
    api =
      Api.communication

    recipient = BoundedContext context
    relationshipType =
      case collaboration of
        SymmetricCollaboration t c ->
          Symmetric t recipient c
        UpstreamCollaboration t d ->
          UpstreamDownstream (recipient,t) d
        DownstreamCollaboration t u ->
          UpstreamDownstream u (recipient,t)
        UnknownCollaboration c ->
          Unknown recipient c

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


isInboundCollaborator : Collaborator -> Collaboration -> Bool
isInboundCollaborator collaborator (Collaboration collaboration) =
  case collaboration.relationship of
    Symmetric _ p1 _ ->
      p1 == collaborator
    UpstreamDownstream (up,_) _ ->
      up == collaborator
    Octopus (up,_) _ ->
      up == collaborator
    Unknown p1 _ ->
      p1 == collaborator


isOutboundCollaborator : Collaborator -> Collaboration -> Bool
isOutboundCollaborator collaborator (Collaboration collaboration) =
  case collaboration.relationship of
    Symmetric _ _ p2 ->
      p2 == collaborator
    UpstreamDownstream _ (down,_) ->
      down == collaborator
    Octopus _ downs ->
      downs
      |> List.any (\(down,_) -> down == collaborator)
    Unknown _ p2 ->
      p2 == collaborator


isCollaborator : Collaborator -> Collaboration -> Maybe CollaborationType
isCollaborator collaborator collaboration =
  case (isInboundCollaborator collaborator collaboration, isOutboundCollaborator collaborator collaboration) of
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
            Supplier -> "Supplier"
        
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
          "Supplier" -> Decode.succeed Supplier
          x  -> Decode.fail <| "could not decode pattern: " ++ x
      )
    )

downstreamCollaboratorEncoder : DownstreamCollaborator -> Encode.Value
downstreamCollaboratorEncoder (collaborator, downstreamType) =
  Encode.object
    [ ( "collaborator"
      , collaboratorEncoder collaborator
      )
    , ( "type"
      , Encode.string <|
          case downstreamType of
            Downstream -> "Downstream"
            AntiCorruptionLayer -> "AntiCorruptionLayer"
            Conformist -> "Conformist"
            Customer -> "Customer"
      )
    ]

downstreamCollaboratorDecoder : Decoder DownstreamCollaborator
downstreamCollaboratorDecoder =
  Decode.map2 Tuple.pair
    ( Decode.field "collaborator" collaboratorDecoder)
    ( Decode.field "type" Decode.string |> Decode.andThen (\downstreamType ->
        case downstreamType of
          "Downstream" -> Decode.succeed Downstream
          "AntiCorruptionLayer" -> Decode.succeed AntiCorruptionLayer
          "Conformist" -> Decode.succeed Conformist
          "Customer" -> Decode.succeed Customer
          x  -> Decode.fail <| "could not decode pattern: " ++ x
      )
    )

relationshipCollaboratorEncoder : RelationshipType -> Encode.Value
relationshipCollaboratorEncoder relationshipType =
  case relationshipType of
    Symmetric symmetricType participant1 participant2 ->
      Encode.object
        [ ( "symmetric"
          , Encode.string <|
              case symmetricType of
                SharedKernel -> "SharedKernel"
                SeparateWays -> "SeparateWays"
                BigBallOfMud -> "BigBallOfMud"
                Partnership -> "Partnership"
          )
        , ( "participant1", collaboratorEncoder participant1 )
        , ( "participant2", collaboratorEncoder participant2 )
        ]
    UpstreamDownstream upstream downstream ->
      Encode.object
        [ ( "upstream", upstreamCollaboratorEncoder upstream
          )
        , ( "downstream",downstreamCollaboratorEncoder downstream)
        ]
    Octopus upstream downstreams ->
      Encode.object
        [ ( "upstream", upstreamCollaboratorEncoder upstream)
        , ( "downstreams", downstreams |> Encode.list downstreamCollaboratorEncoder)
        ]
    Unknown participant1 participant2 ->
      Encode.object
        [ ( "participant1", collaboratorEncoder participant1 )
        , ( "participant2", collaboratorEncoder participant2 )
        ]

relationshipCollaboratorDecoder : Decoder RelationshipType
relationshipCollaboratorDecoder =
  Decode.oneOf 
    [ Decode.map3 Symmetric
      ( Decode.field 
          "symmetric" 
          Decode.string 
          |> Decode.andThen (\symmetricType -> 
              case symmetricType of
                "SharedKernel" -> Decode.succeed SharedKernel
                "SeparateWays" -> Decode.succeed SeparateWays
                "BigBallOfMud" -> Decode.succeed BigBallOfMud
                "Partnership" -> Decode.succeed Partnership
                x  -> Decode.fail <| "could not decode pattern: " ++ x
              )
      )      
      ( Decode.field "participant1" collaboratorDecoder )
      ( Decode.field "participant2" collaboratorDecoder )
    , Decode.map2 UpstreamDownstream
      ( Decode.field "upstream" upstreamCollaboratorDecoder )
      ( Decode.field "downstream" downstreamCollaboratorDecoder )
    , Decode.map2 Octopus
      ( Decode.field "upstream" upstreamCollaboratorDecoder )
      ( Decode.field "downstreams" ( Decode.list downstreamCollaboratorDecoder ) )
    , Decode.map2 Unknown
      ( Decode.field "participant1" collaboratorDecoder )
      ( Decode.field "participant2" collaboratorDecoder )
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