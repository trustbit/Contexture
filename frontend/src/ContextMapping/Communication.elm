module ContextMapping.Communication exposing (
    CollaborationType(..), Communication, CommunicationType(..),ScopedCommunication,
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


type ScopedCommunication = 
    ScopedCommunication Collaborator CommunicationInternal


type alias CommunicationLookup = Dict String (List Collaboration)


noCommunication : Collaborator -> ScopedCommunication
noCommunication collaborator =
    { inbound = Dict.empty
    , outbound = Dict.empty
    }
    |> ScopedCommunication collaborator


inboundCollaborators : ScopedCommunication -> List Collaboration
inboundCollaborators (ScopedCommunication _ communication) =
    communication.inbound
    |> Dict.values
    |> List.concat


outboundCollaborators : ScopedCommunication -> List Collaboration
outboundCollaborators (ScopedCommunication _ communication) =
    communication.outbound
    |> Dict.values
    |> List.concat


isCommunicating : Collaborator -> ScopedCommunication -> CommunicationType
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


appendCollaborator : CollaborationType Collaboration -> ScopedCommunication -> ScopedCommunication
appendCollaborator collaboration (ScopedCommunication scope communication) =
    ScopedCommunication scope <|
        case collaboration of
            IsInbound c ->
                { communication | inbound = dictAppend (Collaboration.recipient c) c communication.inbound }
            IsOutbound c ->
                { communication | outbound = dictAppend (Collaboration.initiator c) c communication.outbound }
    

removeCollaboration collaborationId collaborations =
    collaborations
    |> List.filter (\v -> (v |> Collaboration.id) /= collaborationId)


removeCollaborator : CollaborationType CollaborationId -> ScopedCommunication -> ScopedCommunication
removeCollaborator collaborator (ScopedCommunication scope communication) =
    ScopedCommunication scope <|
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


merge : ScopedCommunication -> ScopedCommunication -> ScopedCommunication
merge (ScopedCommunication scope first) (ScopedCommunication _ second) =
    ScopedCommunication scope
        { inbound = dictMerge first.inbound second.inbound
        , outbound = dictMerge first.outbound second.outbound
        }


update : (Collaboration -> Collaboration) -> ScopedCommunication -> ScopedCommunication
update updater (ScopedCommunication scope communication) =
    ScopedCommunication scope
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


communicationFor : Collaborator -> Communication -> ScopedCommunication
communicationFor collaborator (Communication communication) =
    let
        key = collaboratorAsString collaborator
    in 
        { inbound = Dict.filter (\k _ -> k == key) communication.inbound
        , outbound = Dict.filter (\k _ -> k == key) communication.outbound
        }
        |> ScopedCommunication collaborator


decoder : Collaborator -> Decoder ScopedCommunication
decoder collaborator =
  ( Decode.list Collaboration.decoder )
  |> Decode.map(\collaborations ->
        collaborations
        |> asCommunication
        |> communicationFor collaborator
    )
