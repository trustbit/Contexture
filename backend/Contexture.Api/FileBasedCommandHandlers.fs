namespace Contexture.Api

open Contexture.Api
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Aggregates.Domain
open Contexture.Api.Entities
open Database

module FileBasedCommandHandlers =
    open Aggregates

    type CommandHandlerError<'T,'Id> =
        | DomainError of 'T
        | InfrastructureError of InfrastructureError<'Id>

    and InfrastructureError<'Id> =
        | Exception of exn
        | EntityNotFound of 'Id

    module Domain =
        open Entities
        open Domain

        let private updateDomainsIn (document: Document) =
            Result.map (fun (domains, item) -> { document with Domains = domains }, item)

        let create (database: FileBased) parentDomain (command: CreateDomain) =
            match newDomain command.Name parentDomain with
            | Ok addNewDomain ->
                let changed =
                    database.Change(fun document ->
                        addNewDomain
                        |> document.Domains.Add
                        |> updateDomainsIn document)

                changed
                |> Result.map (fun d -> d.Id)
                |> Result.mapError InfrastructureError
            | Error domainError -> domainError |> DomainError |> Error

        let remove (database: FileBased) domainId =
            let changed =
                database.Change(fun document ->
                    domainId
                    |> document.Domains.Remove
                    |> updateDomainsIn document)

            changed
            |> Result.map (fun _ -> domainId)
            |> Result.mapError InfrastructureError

        let private updateDomain (database: FileBased) domainId updateDomain =
            let changed =
                database.Change(fun document ->
                    domainId
                    |> document.Domains.Update updateDomain
                    |> updateDomainsIn document)

            match changed with
            | Ok _ -> Ok domainId
            | Error (ChangeError e) -> Error(DomainError e)
            | Error (EntityNotFoundInCollection id) ->
                id
                |> EntityNotFound
                |> InfrastructureError
                |> Error
            | Error (DuplicateKey id) ->
                id
                |> EntityNotFound
                |> InfrastructureError
                |> Error

        let handle (database: FileBased) (command: Command) =
            match command with
            | CreateDomain createDomain -> create database None createDomain
            | CreateSubdomain (domainId, createDomain) -> create database (Some domainId) createDomain
            | RemoveDomain domainId -> remove database domainId
            | MoveDomain (domainId, move) -> updateDomain database domainId (moveDomain move.ParentDomainId)
            | RenameDomain (domainId, rename) -> updateDomain database domainId (renameDomain rename.Name)
            | RefineVision (domainId, refineVision) ->
                updateDomain database domainId (refineVisionOfDomain refineVision.Vision)
            | AssignKey (domainId, assignKey) -> updateDomain database domainId (assignKeyToDomain assignKey.Key)

    module BoundedContext =
        open BoundedContext

        let create (database: FileBased) domainId (command: CreateBoundedContext) =
            match newBoundedContext domainId command.Name with
            | Ok addNewBoundedContext ->
                let changed =
                    database.Change(fun document ->
                        addNewBoundedContext
                        |> document.BoundedContexts.Add
                        |> Result.map (fun (bcs, item) -> { document with BoundedContexts = bcs }, item))

                changed
                |> Result.map (fun d -> d.Id)
                |> Result.mapError InfrastructureError
            | Error domainError -> domainError |> DomainError |> Error

        let private updateBoundedContextsIn (document: Document) =
            Result.map (fun (contexts, item) ->
                { document with
                      BoundedContexts = contexts },
                item)

        let remove (database: FileBased) contextId =
            let changed =
                database.Change(fun document ->
                    contextId
                    |> document.BoundedContexts.Remove
                    |> updateBoundedContextsIn document)

            changed
            |> Result.map (fun _ -> contextId)
            |> Result.mapError InfrastructureError

        let private updateBoundedContext (database: FileBased) contextId update =
            let changed =
                database.Change(fun document ->
                    contextId
                    |> document.BoundedContexts.Update update
                    |> updateBoundedContextsIn document)

            match changed with
            | Ok _ -> Ok contextId
            | Error (ChangeError e) -> Error(DomainError e)
            | Error (EntityNotFoundInCollection id) ->
                id
                |> EntityNotFound
                |> InfrastructureError
                |> Error
            | Error (DuplicateKey id) ->
                id
                |> EntityNotFound
                |> InfrastructureError
                |> Error

        let handle (database: FileBased) (command: Command) =
            match command with
            | CreateBoundedContext (domainId, createBc) -> create database domainId createBc
            | UpdateTechnicalInformation (contextId, technical) ->
                updateBoundedContext database contextId (updateTechnicalDescription technical)
            | RenameBoundedContext (contextId, rename) ->
                updateBoundedContext database contextId (renameBoundedContext rename.Name)
            | AssignKey (contextId, key) -> updateBoundedContext database contextId (assignKeyToBoundedContext key.Key)
            | RemoveBoundedContext contextId -> remove database contextId
            | MoveBoundedContextToDomain (contextId, move) ->
                updateBoundedContext database contextId (moveBoundedContext move.ParentDomainId)
            | ReclassifyBoundedContext (contextId, classification) ->
                updateBoundedContext database contextId (reclassify classification.Classification)
            | ChangeDescription (contextId, descriptionText) ->
                updateBoundedContext database contextId (description descriptionText.Description)
            | UpdateBusinessDecisions (contextId, decisions) ->
                updateBoundedContext database contextId (updateBusinessDecisions decisions.BusinessDecisions)
            | UpdateUbiquitousLanguage (contextId, language) ->
                updateBoundedContext database contextId (updateUbiquitousLanguage language.UbiquitousLanguage)
            | UpdateDomainRoles (contextId, roles) ->
                updateBoundedContext database contextId (updateDomainRoles roles.DomainRoles)
            | UpdateMessages (contextId, roles) ->
                updateBoundedContext database contextId (updateMessages roles.Messages)

    module Collaboration =
        open Collaboration
        
        let asEvents (collaboration: Collaboration option) =
            collaboration
            |> Option.map (fun c ->
                CollaborationImported {
                    CollaborationId = c.Id
                    Description = c.Description
                    RelationshipType = c.RelationshipType
                    Initiator = c.Initiator
                    Recipient = c.Recipient
                }
            )
            |> Option.toList
            
        let projectToCollaboration (events: Event list) : Collaboration option =
            let fold collaboration event =
                match event with
                | CollaborationImported c ->
                    Some { Id = c.CollaborationId; Description = c.Description; Initiator = c.Initiator; Recipient = c.Recipient; RelationshipType = c.RelationshipType }
                | CollaboratorsConnected c ->
                    Some { Id = c.CollaborationId; Description = c.Description; Initiator = c.Initiator; Recipient = c.Recipient; RelationshipType = None }
                | RelationshipDefined c ->
                    collaboration
                    |> Option.map ( fun o -> { o with RelationshipType = Some c.RelationshipType })
                | RelationshipUnknown c ->
                    collaboration
                    |> Option.map ( fun o -> { o with RelationshipType = None })
                | ConnectionRemoved _ ->
                    None
                
            events
            |> List.fold fold None 
            

        let private updateCollaborationsIn (document: Document) =
            Result.map (fun collaborations ->
                { document with
                      Collaborations = collaborations }
                )
                
        type ChangeOperation =
            | Add of Collaboration
            | Update of Collaboration
            | Remove of CollaborationId
            | NoOp
                
        let plugEventsIntoToDocument collaboration command =
            let stream = collaboration |> asEvents
            
            let state =
                stream   
                |> List.fold State.Fold State.Initial
            
            let result = handle state command
            result
            |> Result.map (fun publishedEvents ->
                let result =
                    stream @ publishedEvents
                    |> projectToCollaboration
                
                match collaboration,result with
                | None, Some c ->
                    Add c
                | Some _, Some c ->
                    Update c
                | Some c, None ->
                    Remove c.Id
                | None, None ->
                    NoOp
            )

        let handle (database: FileBased) command =
            let changed =
                database.Change(fun document ->
                    let collaborationId = identify command
                    let collaborations =
                        command
                        |> plugEventsIntoToDocument (document.Collaborations.ById collaborationId)
                        |> Result.bind (function
                            | Add c ->
                                document.Collaborations.Add collaborationId c
                            | Update c ->
                                collaborationId
                                |> document.Collaborations.Update (fun _ -> Ok c)
                            | Remove id ->
                                document.Collaborations.Remove id
                            | NoOp ->
                                document.Collaborations |> Ok
                            )
                        
                    collaborations
                    |> updateCollaborationsIn document
                    |> Result.map (fun d -> d,collaborationId)
                    )

            match changed with
            | Ok id -> Ok id
            | Error (ChangeError e) -> Error(DomainError e)
            | Error (EntityNotFoundInCollection id) ->
                id
                |> EntityNotFound
                |> InfrastructureError
                |> Error
            | Error (DuplicateKey k) -> k |> EntityNotFound |> InfrastructureError |> Error

    module Namespaces =
        open Entities
        open Namespaces        
        let private updateBoundedContextsIn (document: Document) =
            Result.map (fun (contexts, item) ->
                { document with
                      BoundedContexts = contexts },
                item)
        let private updateNamespaces (database: FileBased) contextId update =
            let updateNamespacesOnly (boundedContext: BoundedContext) =
                boundedContext.Namespaces
                |> tryUnbox<Namespace list>
                |> Option.defaultValue []
                |> update
                |> Result.map (fun namespaces -> { boundedContext with Namespaces = namespaces })
                
            let changed =
                database.Change(fun document ->
                    contextId
                    |> document.BoundedContexts.Update updateNamespacesOnly
                    |> updateBoundedContextsIn document)

            match changed with
            | Ok _ -> Ok contextId
            | Error (ChangeError e) -> Error(DomainError e)
            | Error (EntityNotFoundInCollection id) ->
                id
                |> EntityNotFound
                |> InfrastructureError
                |> Error
            | Error (DuplicateKey id) ->
                id
                |> EntityNotFound
                |> InfrastructureError
                |> Error
        
        let handle (database: FileBased) (command: Command) =
            match command with
            | NewNamespace (boundedContextId, namespaceCommand) ->
                updateNamespaces database boundedContextId (addNewNamespace namespaceCommand.Name namespaceCommand.Labels)
            | RemoveNamespace (boundedContextId, namespaceCommand) ->
                updateNamespaces database boundedContextId (removeNamespace namespaceCommand)
            | RemoveLabel (boundedContextId, namespaceCommand) ->
                updateNamespaces database boundedContextId (removeLabel namespaceCommand.Namespace namespaceCommand.Label)
            | AddLabel(boundedContextId, namespaceId, namespaceCommand) ->
                updateNamespaces database boundedContextId (addLabel namespaceId namespaceCommand.Name namespaceCommand.Value)