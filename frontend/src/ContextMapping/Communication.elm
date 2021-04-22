module ContextMapping.Communication exposing (CollaborationType(..), Communication, CommunicationType(..)
    , noCommunication,isCommunicating,
    communicationDecoder,
    inboundCollaborators,outboundCollaborators,
    appendCollaborator,removeCollaborator,merge, update
    )

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Dict exposing (Dict)

import ContextMapping.Collaboration as Collaboration exposing (Collaboration)
import ContextMapping.CollaborationId exposing (CollaborationId)
import ContextMapping.Collaborator as Collaborator exposing (Collaborator)
import BoundedContext as BoundedContext exposing (BoundedContext)
import BoundedContext.BoundedContextId as BoundedContextId exposing (BoundedContextId)



noCommunication : Communication
noCommunication =
    Communication
        { inbound = []
        , outbound = []
        }


type alias CommunicationInternal =
  { inbound : List Collaboration
  , outbound : List Collaboration
  }


type CommunicationType
    = NoCommunication
    | Inbound Collaboration
    | Outbound Collaboration
    | InboundAndOutbound Collaboration Collaboration


type CollaborationType t
  = IsInbound t
  | IsOutbound t

-- type alias CommunicationInternal =
--   { initiators : Dict String Collaboration.Collaborations
--   , recipients : Dict String Collaboration.Collaborations
--   }

type Communication =
    Communication CommunicationInternal


inboundCollaborators : Communication -> List Collaboration
inboundCollaborators (Communication communication) = 
    communication.inbound


outboundCollaborators : Communication -> List Collaboration
outboundCollaborators (Communication communication) = 
    communication.outbound


isCommunicating : Collaborator -> Communication -> CommunicationType
isCommunicating collaborator (Communication communication) =
    let 
        isInboundCollaborator =
            communication.inbound
            |> List.filter (\c -> Collaboration.initiator c == collaborator)
            |> List.head
        isOutboundCollaborator =
            communication.outbound
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
appendCollaborator collaborator (Communication communication) =
    Communication <|
        case collaborator of
            IsInbound c ->
                { communication | inbound = c :: communication.inbound }
            IsOutbound c ->
                { communication | outbound = c :: communication.outbound }


removeCollaborator : CollaborationType CollaborationId -> Communication -> Communication
removeCollaborator collaborator (Communication communication) =
    Communication <|
        case collaborator of
            IsInbound c ->
                { communication | inbound = 
                    communication.inbound
                    |> List.filter (\i -> (i |> Collaboration.id) /= c) }
            IsOutbound c ->
                { communication | outbound = 
                    communication.outbound
                    |> List.filter (\i -> (i |> Collaboration.id) /= c) }
    

merge : Communication -> Communication -> Communication
merge (Communication first) (Communication second) =
    Communication
        { inbound = List.append first.inbound second.inbound
        , outbound = List.append first.outbound second.outbound
        }


update : (Collaboration -> Collaboration) -> Communication -> Communication
update updater (Communication communication) =
    Communication 
        { communication
        | inbound = communication.inbound |> List.map updater
        , outbound = communication.outbound |> List.map updater
        }

-- forBoundedContext : Communication -> BoundedContextId -> Collaboration.Collaborations
-- forBoundedContext (Communication { })id = Dict.get (BoundedContextId.value id)
-- dictBcInsert id = Dict.insert (BoundedContextId.value id)


-- communicationForBoundedContext : Collaboration.Collaborations -> Communication
-- communicationForBoundedContext connections =
--     let
--         updateCollaborationLookup selectCollaborator dictionary collaboration =
--             case selectCollaborator collaboration of
--               Collaborator.BoundedContext bcId ->
--                   let
--                       items =
--                           dictionary
--                           |> dictBcGet bcId
--                           |> Maybe.withDefault []
--                           |> List.append (List.singleton collaboration)
--                   in
--                       dictionary |> dictBcInsert bcId items
--               _ ->
--                   dictionary

--         (bcInitiators, bcRecipients) =
--             connections
--             |> List.foldl(\collaboration (initiators, recipients) ->
--                 ( updateCollaborationLookup Collaboration.initiator initiators collaboration
--                 , updateCollaborationLookup Collaboration.recipient recipients collaboration
--                 )
--             ) (Dict.empty, Dict.empty)
--     in
--        Communication { initiators = bcInitiators, recipients = bcRecipients }



communicationDecoder : Collaborator -> Decoder Communication
communicationDecoder collaborator =
  ( Decode.list Collaboration.decoder )
  |> Decode.map(\collaborations ->
      let
          (inbound, outbound) =
            collaborations
            |> List.filterMap (isCollaborator collaborator)
            |> List.foldl(\c (inbounds,outbounds) ->
                case c of
                  IsInbound inboundColl ->
                    (inboundColl :: inbounds,outbounds)
                  IsOutbound outboundColl ->
                    (inbounds,outboundColl :: outbounds)
              ) ([],[])
      in
         Communication 
            { inbound = inbound
            , outbound = outbound
            }
    )

isInboundCollaboratoration : Collaborator -> Collaboration -> Bool
isInboundCollaboratoration collaborator collaboration =
    Collaboration.recipient collaboration == collaborator


isCollaborator : Collaborator -> Collaboration -> Maybe (CollaborationType Collaboration)
isCollaborator collaborator collaboration =
  case (Collaboration.areCollaborating collaborator collaboration, isInboundCollaboratoration collaborator collaboration) of
    (True, True) ->
      Just <| IsInbound collaboration
    (True, False) ->
      Just <| IsOutbound collaboration
    _ ->
      Nothing

