namespace Contexture.Api

open System.Collections.Concurrent
open Contexture.Api
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Aggregates.Domain
open Contexture.Api.Entities
open Database

type StreamName = System.Guid
type Subscription<'E> = StreamName -> 'E list -> unit

type Store() =
    let mutable items: Map<StreamName, obj list> = Map.empty

    let subscriptions =
        ConcurrentDictionary<System.Type, Subscription<obj> list>()

    let stream name =
        items
        |> Map.tryFind name
        |> Option.defaultValue []

    let subscriptionsOf key =
        let (success, items) = subscriptions.TryGetValue key
        if success then items else []

    let append name (newItems: 'E list) =
        let newStream =
            name
            |> stream
            |> List.append (newItems |> List.map box)

        items <- items |> Map.add name newStream

        subscriptionsOf typedefof<'E>
        |> List.iter (fun subscription ->
            let upcastSubscription name events =
                events |> List.map box |> subscription name

            upcastSubscription name newItems)

    let subscribe (subscription: Subscription<'E>) =
        let key = typedefof<'E>

        let upcastSubscription name events =
            events |> List.map unbox<'E> |> subscription name

        subscriptions.AddOrUpdate
            (key, (fun _ -> [ upcastSubscription ]), (fun _ subscriptions -> subscriptions @ [ upcastSubscription ]))
        |> ignore

    member __.Stream name = stream name
    member __.Append name items = lock __ (fun () -> append name items)
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

        let asEvents (collaboration: Collaboration) =
            collaboration.Id,
            [ CollaborationImported
                { CollaborationId = collaboration.Id
                  Description = collaboration.Description
                  RelationshipType = collaboration.RelationshipType
                  Initiator = collaboration.Initiator
                  Recipient = collaboration.Recipient } ]

        let fold collaboration event =
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

        let projectToCollaboration collaboration (events: Event list): Collaboration option =
            events |> List.fold fold collaboration


        let private updateCollaborationsIn (document: Document) =
            Result.map (fun collaborations ->
                { document with
                      Collaborations = collaborations })

        type ChangeOperation =
            | Add of Collaboration
            | Update of Collaboration
            | Remove of CollaborationId
            | NoOp

        let handle (store: Store) command =
            let identity = Collaboration.identify command
            let streamName = Collaboration.name identity

            let state =
                streamName
                |> store.Stream
                |> List.map (unbox<Collaboration.Event>)
                |> List.fold State.Fold State.Initial

            match handle state command with
            | Ok newEvents ->
                store.Append streamName newEvents
                Ok identity
            | Error e -> Error e

        let mapEventsToDocument storedCollaboration stream =
            let result =
                stream
                |> projectToCollaboration storedCollaboration

            match storedCollaboration, result with
            | None, Some c -> Add c
            | Some _, Some c -> Update c
            | Some c, None -> Remove c.Id
            | None, None -> NoOp

        let subscription (database: FileBased): Subscription<Event> =
            fun name (events: Event list) ->
                database.Change(fun document ->
                    let collaborations =
                        events
                        |> mapEventsToDocument (document.Collaborations.ById name)
                        |> function
                        | Add c -> document.Collaborations.Add name c
                        | Update c ->
                            name
                            |> document.Collaborations.Update(fun _ -> Ok c)
                        | Remove id -> document.Collaborations.Remove id
                        | NoOp -> document.Collaborations |> Ok

                    collaborations
                    |> Result.map (fun c -> { document with Collaborations = c }, name))
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
