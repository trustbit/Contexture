module ContextMapping.Communication exposing (
    CollaborationType(..), Communication, CommunicationType(..),
    noCommunication, isCommunicating,
    decoder,asCommunication,communicationFor,
    inboundCollaborators,outboundCollaborators,
    appendCollaborator,removeCollaborator,merge, update
    )

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)

import Dict exposing (Dict)

import ContextMapping.Collaboration as Collaboration exposing (Collaboration)
import ContextMapping.CollaborationId exposing (CollaborationId)
import ContextMapping.Collaborator as Collaborator exposing (Collaborator)
import Api exposing (collaborations)


type alias CommunicationInternal =
  { inbound : CommunicationLookup
  , outbound : CommunicationLookup
  }


type CommunicationType
    = NoCommunication
    | Inbound Collaboration
    | Outbound Collaboration
    | InboundAndOutbound Collaboration Collaboration


type CollaborationType t
  = IsInbound t
  | IsOutbound t


type Communication =
    Communication CommunicationInternal


type alias CommunicationLookup = Dict String (List Collaboration)


noCommunication : Communication
noCommunication =
    Communication
        { inbound = Dict.empty
        , outbound = Dict.empty
        }


inboundCollaborators : Communication -> List Collaboration
inboundCollaborators (Communication communication) =
    communication.inbound
    |> Dict.values
    |> List.concat


outboundCollaborators : Communication -> List Collaboration
outboundCollaborators (Communication communication) =
    communication.outbound
    |> Dict.values
    |> List.concat


isCommunicating : Collaborator -> Communication -> CommunicationType
isCommunicating collaborator communication =
    let
        isInboundCollaborator =
            communication
            |> inboundCollaborators
            |> List.filter (\c -> Collaboration.initiator c == collaborator)
            |> List.head
        isOutboundCollaborator =
            communication
            |> outboundCollaborators
            |> List.filter (\c -> Collaboration.recipient c == collaborator)
            |> List.head
    in
        case ( isInboundCollaborator, isOutboundCollaborator) of
            (Just inbound, Just outbound) ->
                InboundAndOutbound inbound outbound
            (Just inbound, Nothing) ->
                Inbound inbound
            (Nothing, Just outbound) ->
                Outbound outbound
            (Nothing, Nothing) ->
                NoCommunication


appendCollaborator : CollaborationType Collaboration -> Communication -> Communication
appendCollaborator collaboration (Communication communication) =
    Communication <|
        case collaboration of
            IsInbound c ->
                { communication | inbound = dictAppend (Collaboration.recipient c) c communication.inbound }
            IsOutbound c ->
                { communication | outbound = dictAppend (Collaboration.initiator c) c communication.outbound }

removeCollaboration collaborationId collaborations =
    collaborations
    |> List.filter (\v -> (v |> Collaboration.id) /= collaborationId)


removeCollaborator : CollaborationType CollaborationId -> Communication -> Communication
removeCollaborator collaborator (Communication communication) =
    Communication <|
        case collaborator of
            IsInbound c ->
                { communication
                | inbound =
                    communication.inbound
                    |> Dict.map (\_ items -> items |> removeCollaboration c)
                }
            IsOutbound c ->
                { communication
                | outbound =
                    communication.outbound
                    |> Dict.map (\_ items -> items |> removeCollaboration c)
                }


merge : Communication -> Communication -> Communication
merge (Communication first) (Communication second) =
    Communication
        { inbound = dictMerge first.inbound second.inbound
        , outbound = dictMerge first.outbound second.outbound
        }


update : (Collaboration -> Collaboration) -> Communication -> Communication
update updater (Communication communication) =
    Communication
        { communication
        | inbound = communication.inbound |> Dict.map (\_ v -> List.map updater v)
        , outbound = communication.outbound |> Dict.map (\_ v -> List.map updater v)
        }

collaboratorAsString collaborator =
    Encode.encode 0 (Collaborator.encoder collaborator)

dictInsert : Collaborator -> List Collaboration -> CommunicationLookup -> CommunicationLookup
dictInsert collaborator value dict =
    Dict.insert (collaboratorAsString collaborator) value dict


dictAppend : Collaborator -> Collaboration -> CommunicationLookup -> CommunicationLookup
dictAppend collaborator collaboration dict =
    case dictGet collaborator dict of
        Just items ->
            dictInsert collaborator (collaboration :: items) dict
        Nothing ->
            dictInsert collaborator [ collaboration ] dict

dictGet : Collaborator -> CommunicationLookup -> Maybe (List Collaboration)
dictGet collaborator dict =
    Dict.get (collaboratorAsString collaborator) dict

dictGetListWithElements key values dict =
    dict
    |> Dict.get key
    |> Maybe.withDefault []
    |> List.append values

dictMergeAppend key values merged =
    let
        updatedValues = dictGetListWithElements key values merged
    in
        Dict.insert key updatedValues merged

dictMerge first second =
    Dict.merge
        dictMergeAppend
        (\key firstValue secondValue merged -> dictMergeAppend key (List.append firstValue secondValue) merged)
        dictMergeAppend
        first
        second
        Dict.empty


collaborationAsInbound = Collaboration.recipient
collaborationAsOutbound = Collaboration.initiator

asCommunication : Collaboration.Collaborations -> Communication
asCommunication connections =
    let
        updateCollaborationLookup selectCollaborator dictionary collaboration =
            dictionary
            |> dictAppend (selectCollaborator collaboration) collaboration

        (inboundCollaboration, outboundCollaboration) =
            connections
            |> List.foldl(\collaboration (inbound, outbound) ->
                ( updateCollaborationLookup collaborationAsInbound inbound collaboration
                , updateCollaborationLookup collaborationAsOutbound outbound collaboration
                )
            ) (Dict.empty, Dict.empty)
    in
       Communication { inbound = inboundCollaboration, outbound = outboundCollaboration }


communicationFor : Collaborator -> Communication -> Communication
communicationFor collaborator (Communication communication) =
    let
        key = collaboratorAsString collaborator
    in Communication <|
        { inbound = Dict.filter (\k _ -> k == key) communication.inbound
        , outbound = Dict.filter (\k _ -> k == key) communication.outbound
        }


decoder : Collaborator -> Decoder Communication
decoder collaborator =
  ( Decode.list Collaboration.decoder )
  |> Decode.map(\collaborations ->
        collaborations
        |> asCommunication
        |> communicationFor collaborator
    )
