namespace Contexture.Api

open Contexture.Api
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Aggregates.Domain
open Contexture.Api.Entities
open Database
open Contexture.Api.Infrastructure

module FileBasedCommandHandlers =
    open Aggregates

    type CommandHandlerError<'T, 'Id> =
        | DomainError of 'T
        | InfrastructureError of InfrastructureError<'Id>

    and InfrastructureError<'Id> =
        | Exception of exn
        | EntityNotFound of 'Id

    module BridgeEventSourcingWithDatabase =
        type ChangeOperation<'Item, 'Id> =
            | Add of 'Item
            | Update of 'Item
            | Remove of 'Id
            | NoOp

        let mapEventToDocument fetch project (event: EventEnvelope<_>) =
            let stored = fetch event
            let result = event.Event |> project stored

            match stored, result with
            | None, Some c -> Add c
            | Some _, Some c -> Update c
            | Some c, None -> Remove c.Id
            | None, None -> NoOp

        let applyToCollection fetch project =
            fun state event ->
                match state with
                | Ok (collection: CollectionOfGuid<_>) ->
                    event
                    |> mapEventToDocument (fetch collection) project
                    |> function
                    | Add c -> collection.Add c.Id c
                    | Update c -> c.Id |> collection.Update(fun _ -> Ok c)
                    | Remove id -> collection.Remove id
                    | NoOp -> collection |> Ok
                | Error e -> Error e

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
        open BridgeEventSourcingWithDatabase
        
        let handle clock (store: EventStore) command =
            let identity = Collaboration.identify command
            let streamName = Collaboration.name identity

            let state =
                streamName
                |> store.Stream
                |> List.map (fun e -> e.Event)
                |> List.fold State.Fold State.Initial

            match handle state command with
            | Ok newEvents ->
                newEvents
                |> List.map (fun e ->
                    { Event = e
                      Metadata =
                          { Source = streamName
                            RecordedAt = clock () } })
                |> store.Append

                Ok identity
            | Error e -> Error e

        let asEvents clock (collaboration: Collaboration) =
            { Metadata =
                  { Source = collaboration.Id
                    RecordedAt = clock () }
              Event =
                  CollaborationImported
                      { CollaborationId = collaboration.Id
                        Description = collaboration.Description
                        RelationshipType = collaboration.RelationshipType
                        Initiator = collaboration.Initiator
                        Recipient = collaboration.Recipient } }
            |> List.singleton

        let fetchCollaboration (collection: CollectionOfGuid<_>) (event: EventEnvelope<_>) =
            collection.ById(event.Metadata.Source)

        let subscription (database: FileBased): Subscription<Event> =
            fun (events: EventEnvelope<Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection fetchCollaboration Projections.asCollaboration)
                           (Ok document.Collaborations)
                    |> Result.map (fun c -> { document with Collaborations = c }, System.Guid.Empty))
                |> ignore

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
                |> Result.map (fun namespaces ->
                    { boundedContext with
                          Namespaces = namespaces })

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
                updateNamespaces
                    database
                    boundedContextId
                    (addNewNamespace namespaceCommand.Name namespaceCommand.Labels)
            | RemoveNamespace (boundedContextId, namespaceCommand) ->
                updateNamespaces database boundedContextId (removeNamespace namespaceCommand)
            | RemoveLabel (boundedContextId, namespaceCommand) ->
                updateNamespaces
                    database
                    boundedContextId
                    (removeLabel namespaceCommand.Namespace namespaceCommand.Label)
            | AddLabel (boundedContextId, namespaceId, namespaceCommand) ->
                updateNamespaces
                    database
                    boundedContextId
                    (addLabel namespaceId namespaceCommand.Name namespaceCommand.Value)
