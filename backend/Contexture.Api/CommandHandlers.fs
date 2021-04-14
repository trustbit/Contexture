namespace Contexture.Api

open Contexture.Api
open Contexture.Api.Aggregates.NamespaceTemplate.Projections
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

    module BridgeEventSourcingWithFilebasedDatabase =
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
            | Some c, None -> Remove event.Metadata.Source
            | None, None -> NoOp
        let fetchFromCollection (collection: CollectionOfGuid<_>) (event: EventEnvelope<_>) =
            collection.ById(event.Metadata.Source)
        let applyToCollection project =
            fun state event ->
                match state with
                | Ok (collection: CollectionOfGuid<_>) ->
                    event
                    |> mapEventToDocument (fetchFromCollection collection) project
                    |> function
                    | Add c -> collection.Add event.Metadata.Source c
                    | Update c -> event.Metadata.Source |> collection.Update(fun _ -> Ok c)
                    | Remove id -> collection.Remove id
                    | NoOp -> collection |> Ok
                | Error e -> Error e

    module Domain =
        open Contexture.Api.Aggregates.Domain
        open Contexture.Api.Entities
        open BridgeEventSourcingWithFilebasedDatabase
        
        let handle clock (store: EventStore) command =
            let identity = Domain.identify command
            let streamName = Domain.name identity

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
            | Error e ->
                e |> DomainError |> Error
            
        let asEvents clock (domain: Domain) =
            { Metadata =
                  { Source = domain.Id
                    RecordedAt = clock () }
              Event =
                  DomainImported
                      { DomainId = domain.Id
                        Name = domain.Name
                        ParentDomainId = domain.ParentDomainId
                        Vision = domain.Vision
                        Key = domain.Key } }
            |> List.singleton

        let subscription (database: FileBased): Subscription<Domain.Event> =
            fun (events: EventEnvelope<Domain.Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection Projections.asDomain)
                           (Ok document.Domains)
                    |> Result.map (fun c -> { document with Domains = c }, System.Guid.Empty))
                |> ignore

    module BoundedContext =

        open BridgeEventSourcingWithFilebasedDatabase
        
        open Contexture.Api.Entities
        open BoundedContext
        
        let handle clock (store: EventStore) (command: BoundedContext.Command) =
            let identity = BoundedContext.identify command
            let streamName = BoundedContext.name identity

            let state =
                streamName
                |> store.Stream
                |> List.map (fun e -> e.Event)
                |> List.fold BoundedContext.State.Fold BoundedContext.State.Initial

            match BoundedContext.handle state command with
            | Ok newEvents ->
                newEvents
                |> List.map (fun e ->
                    { Event = e
                      Metadata =
                          { Source = streamName
                            RecordedAt = clock () } })
                |> store.Append

                Ok identity
            | Error e ->
                e |> DomainError |> Error

        let asEvents clock (collaboration: BoundedContext) =
            { Metadata =
                  { Source = collaboration.Id
                    RecordedAt = clock () }
              Event =
                  BoundedContextImported
                      { BoundedContextId = collaboration.Id
                        DomainId = collaboration.DomainId
                        Description = collaboration.Description
                        Messages = collaboration.Messages
                        Classification = collaboration.Classification
                        DomainRoles = collaboration.DomainRoles
                        UbiquitousLanguage = collaboration.UbiquitousLanguage
                        BusinessDecisions = collaboration.BusinessDecisions
                        Key = collaboration.Key
                        Name = collaboration.Name
                        TechnicalDescription = collaboration.TechnicalDescription
                         }
            }
            |> List.singleton

        let subscription (database: FileBased): Subscription<Event> =
            fun (events: EventEnvelope<Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection BoundedContext.Projections.asBoundedContext)
                           (Ok document.BoundedContexts)
                    |> Result.map (fun c -> { document with BoundedContexts = c }, System.Guid.Empty))
                |> ignore

    module Collaboration =
        open Contexture.Api.Entities
        open Collaboration
        open BridgeEventSourcingWithFilebasedDatabase
        
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
            | Error e ->
                e |> DomainError |> Error

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

        let subscription (database: FileBased): Subscription<Event> =
            fun (events: EventEnvelope<Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection Projections.asCollaboration)
                           (Ok document.Collaborations)
                    |> Result.map (fun c -> { document with Collaborations = c }, System.Guid.Empty))
                |> ignore

    module Namespace =
        open Entities
        open Namespace
        open BridgeEventSourcingWithFilebasedDatabase
        
        let handle clock (store: EventStore) command =
            let identity = Namespace.identify command
            let streamName = Namespace.name identity

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
            | Error e ->
                e |> DomainError |> Error

        let asEvents clock (boundedContext: BoundedContext) =
            boundedContext.Namespaces
            |> List.map (fun n ->
                { Metadata =
                      { Source = boundedContext.Id
                        RecordedAt = clock () }
                  Event =
                      NamespaceImported
                          { NamespaceId = n.Id
                            BoundedContextId = boundedContext.Id
                            NamespaceTemplateId = n.Template
                            Name = n.Name
                            Labels = n.Labels |> List.map (fun l -> { LabelId = l.Id; Name = l.Name; Value = Option.ofObj l.Value })
                          }
                }
            )

        let subscription (database: FileBased): Subscription<Event> =
            fun (events: EventEnvelope<Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection Projections.asNamespaceWithBoundedContext)
                           (Ok document.BoundedContexts)
                    |> Result.map (fun c -> { document with BoundedContexts = c }, System.Guid.Empty))
                |> ignore

    module NamespaceTemplate =
        open Entities
        open NamespaceTemplate
        open BridgeEventSourcingWithFilebasedDatabase
        
        let handle clock (store: EventStore) command =
            let identity = NamespaceTemplate.identify command
            let streamName = NamespaceTemplate.name identity

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
            | Error e ->
                e |> DomainError |> Error

        let asEvents clock (template: NamespaceTemplate) =
            { Metadata =
                  { Source = template.Id
                    RecordedAt = clock () }
              Event =
                  NamespaceTemplateImported
                      { NamespaceTemplateId = template.Id
                        Name = template.Name
                        Description = Option.ofObj template.Description
                        Labels =
                            template.Template
                            |> List.map (fun l ->
                                { TemplateLabelId = l.Id; Name = l.Name; Description = Option.ofObj l.Description; Placeholder = Option.ofObj l.Placeholder }
                            )
                      }
            }
            |> List.singleton

        let subscription (database: FileBased): Subscription<Event> =
            fun (events: EventEnvelope<Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection Projections.asTemplate)
                           (Ok document.NamespaceTemplates)
                    |> Result.map (fun c -> { document with NamespaceTemplates = c }, System.Guid.Empty))
                |> ignore
