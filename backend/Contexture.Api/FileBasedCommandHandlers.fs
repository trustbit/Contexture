namespace Contexture.Api

open System
open System.Collections.Concurrent
open System.Collections.Generic
open Contexture.Api
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Aggregates.Domain
open Contexture.Api.Entities
open Database

type EventSource = System.Guid
type EventMetadata =
    { Source : EventSource
      RecordedAt : System.DateTime  }
type EventEnvelope<'Event> =
    { Metadata : EventMetadata
      Event: 'Event }
type Subscription<'E> = EventEnvelope<'E> list -> unit

type Store() =
    let items = Dictionary<EventSource, EventEnvelope<obj> list>()

    let subscriptions =
        ConcurrentDictionary<System.Type, Subscription<obj> list>()

    let boxEnvelope (envelope: EventEnvelope<'E>) =
        { Metadata = envelope.Metadata; Event = box envelope.Event }
        
    let unboxEnvelope (envelope:EventEnvelope<obj>) : EventEnvelope<'E>=
        { Metadata = envelope.Metadata; Event= unbox<'E> envelope.Event }

    let stream source =
        let (success, events) = items.TryGetValue source
        if success
        then events |> List.map unboxEnvelope
        else []

    let subscriptionsOf key =
        let (success, items) = subscriptions.TryGetValue key
        if success then items else []

    let append (newItems: EventEnvelope<'E> list) =
        newItems
        |> List.iter (fun envelope ->
            let source = envelope.Metadata.Source
            let fullStream =
                source
                |> stream
                |> fun s -> s @ [ envelope ]
                |> List.map boxEnvelope
            items.[source] <- fullStream
            )

        subscriptionsOf typedefof<'E>
        |> List.iter (fun subscription ->
            let upcastSubscription events =
                events |> List.map boxEnvelope |> subscription

            upcastSubscription newItems)

    let subscribe (subscription: Subscription<'E>) =
        let key = typedefof<'E>

        let upcastSubscription events =
            events |> List.map unboxEnvelope |> subscription

        subscriptions.AddOrUpdate
            (key, (fun _ -> [ upcastSubscription ]), (fun _ subscriptions -> subscriptions @ [ upcastSubscription ]))
        |> ignore

    member __.Stream name : EventEnvelope<'E> list = stream name
    member __.Append items = lock __ (fun () -> append items)
    member __.Subscribe(subscription: Subscription<'E>) = subscribe subscription

module FileBasedCommandHandlers =
    open Aggregates

    type CommandHandlerError<'T, 'Id> =
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

        let asEvents clock (collaboration: Collaboration) =
            { Metadata = { Source = collaboration.Id; RecordedAt = clock() }
              Event = CollaborationImported
                { CollaborationId = collaboration.Id
                  Description = collaboration.Description
                  RelationshipType = collaboration.RelationshipType
                  Initiator = collaboration.Initiator
                  Recipient = collaboration.Recipient }
            }
            |> List.singleton

        let fold collaboration event =
            match event.Event with
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

        let private updateCollaborationsIn (document: Document) =
            Result.map (fun collaborations ->
                { document with
                      Collaborations = collaborations })

        type ChangeOperation =
            | Add of Collaboration
            | Update of Collaboration
            | Remove of CollaborationId
            | NoOp

        let handle clock (store: Store) command =
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
                |> List.map (fun e -> { Event = e; Metadata = { Source = streamName; RecordedAt = clock() } })
                |> store.Append
                Ok identity
            | Error e -> Error e
            
            
        let mapEventToDocument fetchCollaboration (event:EventEnvelope<_>) =
            let storedCollaboration = fetchCollaboration event
            let result =
                event
                |> fold storedCollaboration

            match storedCollaboration, result with
            | None, Some c -> Add c
            | Some _, Some c -> Update c
            | Some c, None -> Remove c.Id
            | None, None -> NoOp

        let fetchCollaboration (collection: CollectionOfGuid<_>) (event: EventEnvelope<_>) =
            collection.ById (event.Metadata.Source)

        let subscription (database: FileBased): Subscription<Event> =
            fun (events: EventEnvelope<Event> list) ->
                database.Change(fun document ->
                    let applyToCollection result event =
                        match result with
                        | Ok collection ->
                            event
                            |> mapEventToDocument (fetchCollaboration collection)
                            |> function
                                | Add c -> collection.Add c.Id c
                                | Update c ->
                                    c.Id
                                    |> collection.Update(fun _ -> Ok c)
                                | Remove id -> collection.Remove id
                                | NoOp -> collection |> Ok
                        | Error e ->
                            Error e
                    events
                    |> List.fold applyToCollection (Ok document.Collaborations)
                    |> Result.map (fun c -> { document with Collaborations = c  }, System.Guid.Empty)
                )
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
