module Contexture.Api.Aggregates.Collaboration

open Contexture.Api.Entities

type Command =
    | DefineRelationship of CollaborationId * DefineRelationship
    | DefineOutboundConnection of CollaborationId * DefineConnection
    | DefineInboundConnection of CollaborationId * DefineConnection
    | RemoveConnection of CollaborationId

and DefineRelationship =
    { RelationshipType: RelationshipType option }

and DefineConnection =
    { Description: string option
      Initiator: Collaborator
      Recipient: Collaborator }

type Event =
    | CollaborationImported of CollaborationImported
    | RelationshipDefined of RelationshipDefined
    | RelationshipUnknown of RelationshipUnknown
    | CollaboratorsConnected of CollaboratorsConnected
    | ConnectionRemoved of ConnectionRemoved

and CollaborationImported =
    { CollaborationId: CollaborationId
      Description: string option
      Initiator: Collaborator
      Recipient: Collaborator
      RelationshipType: RelationshipType option }

and RelationshipDefined =
    { CollaborationId: CollaborationId
      RelationshipType: RelationshipType }

and RelationshipUnknown = { CollaborationId: CollaborationId }

and CollaboratorsConnected =
    { CollaborationId: CollaborationId
      Description: string option
      Initiator: Collaborator
      Recipient: Collaborator }

and ConnectionRemoved = { CollaborationId: CollaborationId }

type State =
    | Initial
    | Existing
    | Deleted
    static member Fold (state: State) (event: Event) =
        match event with
        | ConnectionRemoved _ -> Deleted
        | _ -> Existing

let identify =
    function
    | DefineInboundConnection (collaborationId, _) -> collaborationId
    | DefineOutboundConnection (collaborationId, _) -> collaborationId
    | DefineRelationship (collaborationId, _) -> collaborationId
    | RemoveConnection (collaborationId) -> collaborationId

let name identity = identity

let handle (state: State) (command: Command) =
    match state, command with
    | Existing, DefineRelationship (collaborationId, relationship) ->
        match relationship.RelationshipType with
        | Some r ->
            Ok [ RelationshipDefined
                     { CollaborationId = collaborationId
                       RelationshipType = r } ]
        | None -> Ok [ RelationshipUnknown { CollaborationId = collaborationId } ]
    | Initial, DefineInboundConnection (collaborationId, connection) ->
        Ok [ CollaboratorsConnected
                 { CollaborationId = collaborationId
                   Description = connection.Description
                   Initiator = connection.Initiator
                   Recipient = connection.Recipient } ]
    | Initial, DefineOutboundConnection (collaborationId, connection) ->
        Ok [ CollaboratorsConnected
                 { CollaborationId = collaborationId
                   Description = connection.Description
                   Initiator = connection.Initiator
                   Recipient = connection.Recipient } ]
    | _, RemoveConnection collaborationId -> Ok [ ConnectionRemoved { CollaborationId = collaborationId } ]
    | _, _ -> Ok []

module Projections =
    let asCollaboration collaboration event =
        match event with
        | CollaborationImported c ->
            Some
                { Id = c.CollaborationId
                  Description = c.Description
                  Initiator = c.Initiator
                  Recipient = c.Recipient
                  RelationshipType = c.RelationshipType }
        | CollaboratorsConnected c ->
            Some
                { Id = c.CollaborationId
                  Description = c.Description
                  Initiator = c.Initiator
                  Recipient = c.Recipient
                  RelationshipType = None }
        | RelationshipDefined c ->
            collaboration
            |> Option.map (fun o ->
                { o with
                      RelationshipType = Some c.RelationshipType })
        | RelationshipUnknown c ->
            collaboration
            |> Option.map (fun o -> { o with RelationshipType = None })
        | ConnectionRemoved _ -> None
