namespace Contexture.Api

open Contexture.Api
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Aggregates.Domain
open Database

module FileBasedCommandHandlers =
    open Aggregates

    type CommandHandlerError<'T> =
        | DomainError of 'T
        | InfrastructureError of InfrastructureError

    and InfrastructureError =
        | Exception of exn
        | EntityNotFound of int

    module Domain =
        open Domain

        let private updateDomainsIn (document: Document) =
            Result.map (fun (domains, item) -> { document with Domains = domains }, item)

        let create (database: FileBased) (command: CreateDomain) =
            match newDomain command.Name with
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

        let handle (database: FileBased) (command: Command): Result<DomainId, CommandHandlerError<Errors>> =
            match command with
            | CreateDomain createDomain -> create database createDomain
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
                        |> Result.map(fun (bcs,item) ->
                            { document with BoundedContexts = bcs },item
                        )
                       )
                changed
                |> Result.map (fun d -> d.Id)
                |> Result.mapError InfrastructureError                        
            | Error domainError ->
                domainError |> DomainError |> Error
                
        let private updateBoundedContextsIn (document: Document) =
            Result.map (fun (contexts, item) -> { document with BoundedContexts = contexts }, item)

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
        
        let handle (database: FileBased) (command: Command) =
            match command with
            | CreateBoundedContext (domainId,createBc) ->
                create database domainId createBc
            | UpdateTechnicalInformation (contextId,technical) ->
                updateBoundedContext database contextId (updateTechnicalDescription technical)
            | RenameBoundedContext (contextId, rename) ->
                updateBoundedContext database contextId (renameBoundedContext rename.Name)
            | AssignKey (contextId, key) ->
                updateBoundedContext database contextId (assignKeyToBoundedContext key.Key)
            | RemoveBoundedContext contextId ->
                remove database contextId
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
        let private updateCollaborationsIn (document: Document) =
            Result.map (fun (collaborations, item) -> { document with Collaborations = collaborations }, item)

        let create (database: FileBased) (command: DefineConnection) =
            let changed =
                database.Change(fun document ->
                    newConnection command.Initiator command.Recipient command.Description 
                    |> document.Collaborations.Add
                    |> updateCollaborationsIn document
                   )
            changed
            |> Result.map (fun d -> d.Id)
            |> Result.mapError InfrastructureError

        let remove (database: FileBased) collaborationId =
            let changed =
                database.Change(fun document ->
                    collaborationId
                    |> document.Collaborations.Remove
                    |> updateCollaborationsIn document)

            changed
            |> Result.map (fun _ -> collaborationId)
            |> Result.mapError InfrastructureError
                        
        let private updateCollaboration (database: FileBased) collaborationId update =
            let changed =
                database.Change(fun document ->
                    collaborationId
                    |> document.Collaborations.Update update
                    |> updateCollaborationsIn document)

            match changed with
            | Ok _ -> Ok collaborationId
            | Error (ChangeError e) -> Error(DomainError e)
            | Error (EntityNotFoundInCollection id) ->
                id
                |> EntityNotFound
                |> InfrastructureError
                |> Error
        
        let handle (database: FileBased) (command: Command) =
            match command with
            | ChangeRelationshipType (collaborationId,relationship) ->
                updateCollaboration database collaborationId (changeRelationship relationship.RelationshipType)
            | DefineInboundConnection connection ->
                create database connection
            | DefineOutboundConnection connection ->
                create database connection