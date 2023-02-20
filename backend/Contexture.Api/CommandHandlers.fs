namespace Contexture.Api

open System.Threading.Tasks
open Contexture.Api
open Contexture.Api.Aggregates.Domain
open Contexture.Api.Aggregates.NamespaceTemplate.Projections
open Database
open Contexture.Api.Infrastructure
open FSharp.Control.Tasks
open Microsoft.Extensions.Logging

module CommandHandler =
    type CommandHandlerError<'T, 'Id> =
        | DomainError of 'T
        | InfrastructureError of InfrastructureError<'Id>

    and InfrastructureError<'Id> =
        | Exception of exn
        | EntityNotFound of 'Id

    type HandleIdentityAndCommand<'Identity,'Command,'Error> = 'Identity -> 'Command -> Async<Result<'Identity,CommandHandlerError<'Error,'Identity>>>
    type HandleCommand<'Identity,'Command,'Error> = 'Command -> Async<Result<'Identity,CommandHandlerError<'Error,'Identity>>>
    
    let getIdentityFromCommand identifyCommand (handler: HandleIdentityAndCommand<_,_,_>) : HandleCommand<_,_,_> =
        fun command -> async {
            let identity = identifyCommand command
            return! handler identity command
        }
    
    type Aggregate<'State,'Cmd, 'Event, 'Error> =
        { Decider : 'Cmd -> 'State -> Result<'Event list,'Error>
          Evolve: 'State -> 'Event -> 'State
          Initial : 'State }
        with
            static member From decider evolve initial : Aggregate<'State, 'Cmd,'Event,'Error>=
                { Decider = decider
                  Evolve = evolve
                  Initial = initial }
        
        
    let handleWithStream loadStream saveStream (aggregate: Aggregate<'State,'Cmd,'Event,'Error>) : HandleIdentityAndCommand<'Identity,'Cmd,'Error> =
        fun identity (command : 'Cmd) -> async {
            let! stream = loadStream identity
            
            let state =
                stream
                |> List.fold aggregate.Evolve aggregate.Initial
                
            match aggregate.Decider command state with
            | Ok newEvents ->
                do! saveStream identity newEvents
                return Ok identity
            | Error e ->
                return e |> DomainError |> Error
        }
    
    module EventBased =
        type GetStreamName<'Identity> = 'Identity -> EventSource
        type StreamBasedCommandHandler<'Identity,'State,'Cmd,'Event,'Error> = GetStreamName<'Identity> -> Aggregate<'State,'Cmd,'Event,'Error> -> HandleIdentityAndCommand<'Identity, 'Cmd,'Error>
        
        let eventStoreBasedCommandHandler clock (eventStore:EventStore) : StreamBasedCommandHandler<_,_,_,_,_> =
            fun getStreamName aggregate  ->
                let loadStream identity = async {
                    let streamName = getStreamName identity
                    match! eventStore.Stream streamName Version.start with
                    | Ok (version, stream)->
                        return List.map (fun e -> e.Event) stream
                    | Error e ->
                        return failwithf "Failed to get stream %O with:\n%O" streamName e 
                    }
                
                let saveStream identity newEvents = async {
                    let name = getStreamName identity
                    let mappedEvents =
                        newEvents
                        |> List.map (fun e ->
                            { Event = e
                              Metadata =
                                  { Source = name
                                    RecordedAt = clock () } })
                        
                    match! eventStore.Append name Unknown newEvents with
                    | Ok _ -> return ()
                    | Error e -> return failwithf "Failed to save events for %O with:\n%O" name e
                }
                
                handleWithStream loadStream saveStream aggregate

module FileBasedCommandHandlers =
    open Contexture.Api.Aggregates
    open CommandHandler
    open CommandHandler.EventBased
    
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
                
        let waitForDbChange (logger: ILogger) results = async {
            match! results with
            | Ok _ -> return ()
            | Error (e: string) -> logger.LogCritical("Could not update database: {Error}", e)
        }

    module Domain =
        open Contexture.Api.Aggregates.Domain
        open BridgeEventSourcingWithFilebasedDatabase
        
        let aggregate =
                Aggregate.From
                    Domain.decide
                    Domain.State.evolve
                    Domain.State.Initial
        
        let useHandler stateBasedHandler =
            aggregate
            |> stateBasedHandler Domain.name
            |> CommandHandler.getIdentityFromCommand Domain.identify   
            
        let asEvents clock (domain: Serialization.Domain) =
            { Metadata =
                  { Source = domain.Id
                    RecordedAt = clock () }
              Event =
                  DomainImported
                      { DomainId = domain.Id
                        Name = domain.Name
                        ParentDomainId = domain.ParentDomainId
                        Vision = domain.Vision
                        ShortName = domain.ShortName } }
            |> List.singleton

        let mapDomainToSerialization (state: Serialization.Domain option) event : Serialization.Domain option =
            let convertToOption =
                function
                | Initial -> None
                | Existing state ->
                    let mappedState: Serialization.Domain =
                        {
                            Id = state.Id
                            ShortName = state.ShortName
                            Name = state.Name
                            Vision = state.Vision
                            ParentDomainId = state.ParentDomainId
                        }
                    Some mappedState
                | Deleted -> None
            
            match state with
            | Some s ->
                let mappedState =
                    Existing {
                        Id = s.Id
                        ShortName = s.ShortName
                        Name = s.Name
                        Vision = s.Vision
                        ParentDomainId = s.ParentDomainId
                    }
                State.evolve mappedState event
            | None ->
                State.evolve Initial event
            |> convertToOption 
                
        let subscription logger (database: SingleFileBasedDatastore): SubscriptionHandler<Domain.Event> =
            fun (events: EventEnvelope<Domain.Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection mapDomainToSerialization)
                           (Ok document.Domains)
                    |> Result.map (fun c -> { document with Domains = c })
                    |> Result.mapError (fun e -> $"%O{e}"))
                |> waitForDbChange logger

    module BoundedContext =

        open BridgeEventSourcingWithFilebasedDatabase
        open BoundedContext
        
        let aggregate =
                Aggregate.From
                    BoundedContext.decide
                    BoundedContext.State.evolve
                    BoundedContext.State.Initial
        
        let useHandler stateBasedHandler =
            aggregate
            |> stateBasedHandler BoundedContext.name
            |> CommandHandler.getIdentityFromCommand BoundedContext.identify   

        let asEvents clock (collaboration: Serialization.BoundedContext) =
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
                        ShortName = collaboration.ShortName
                        Name = collaboration.Name }
            }
            |> List.singleton
            
            
        let mapSerialization (boundedContext: Serialization.BoundedContext option) (event: BoundedContext.Event): Serialization.BoundedContext option =
            let convertToSerialization namespaces (context: Projections.BoundedContext): Serialization.BoundedContext =
                { Id = context.Id
                  DomainId = context.DomainId
                  ShortName = context.ShortName
                  Name = context.Name
                  Description = context.Description
                  Classification = context.Classification
                  BusinessDecisions = context.BusinessDecisions
                  UbiquitousLanguage = context.UbiquitousLanguage
                  Messages = context.Messages
                  DomainRoles = context.DomainRoles
                  Namespaces = namespaces
                }
                
            match boundedContext with
            | Some bc ->
                let mapped: Projections.BoundedContext =
                    { Id = bc.Id
                      DomainId = bc.DomainId
                      ShortName = bc.ShortName
                      Name = bc.Name
                      Description = bc.Description
                      Classification = bc.Classification
                      BusinessDecisions = bc.BusinessDecisions
                      UbiquitousLanguage = bc.UbiquitousLanguage
                      Messages = bc.Messages
                      DomainRoles = bc.DomainRoles                        
                    }
                BoundedContext.Projections.asBoundedContext (Some mapped) event
                |> Option.map (convertToSerialization bc.Namespaces)
            | None ->
                BoundedContext.Projections.asBoundedContext None event
                |> Option.map (convertToSerialization [])
            

        let subscription logger (database: SingleFileBasedDatastore): SubscriptionHandler<Event> =
            fun (events: EventEnvelope<Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection mapSerialization)
                           (Ok document.BoundedContexts)
                    |> Result.map (fun c -> { document with BoundedContexts = c })
                    |> Result.mapError (fun e -> $"%O{e}"))
                |> waitForDbChange logger

    module Collaboration =
        
        open BridgeEventSourcingWithFilebasedDatabase
        let aggregate =
                Aggregate.From
                    Collaboration.decide
                    Collaboration.State.evolve
                    Collaboration.State.Initial
        
        let useHandler stateBasedHandler =
            aggregate
            |> stateBasedHandler Collaboration.name
            |> CommandHandler.getIdentityFromCommand Collaboration.identify               
            
        
        let asEvents clock (collaboration: Serialization.Collaboration) =
            { Metadata =
                  { Source = collaboration.Id
                    RecordedAt = clock () }
              Event =
                  Collaboration.CollaborationImported
                      { CollaborationId = collaboration.Id
                        Description = collaboration.Description
                        RelationshipType = collaboration.RelationshipType
                        Initiator = collaboration.Initiator
                        Recipient = collaboration.Recipient } }
            |> List.singleton
            
        let mapToSerialization (state: Serialization.Collaboration option) event : Serialization.Collaboration option =
            let convertToOption =
                function
                | Collaboration.Initial -> None
                | Collaboration.Existing s ->
                    let mappedState: Serialization.Collaboration =
                        {
                            Id = s.Id
                            Initiator = s.Initiator
                            Recipient = s.Recipient
                            Description = s.Description
                            RelationshipType = s.RelationshipType
                        }
                    Some mappedState
                | Collaboration.Deleted -> None
            match state with
            | Some s ->
                let mappedState =
                    Collaboration.Existing {
                        Id = s.Id
                        Initiator = s.Initiator
                        Recipient = s.Recipient
                        Description = s.Description
                        RelationshipType = s.RelationshipType
                    }
                Collaboration.State.evolve mappedState event
            | None ->
                Collaboration.State.evolve Collaboration.Initial event
            |> convertToOption 

        let subscription logger (database: SingleFileBasedDatastore): SubscriptionHandler<Collaboration.Event> =
            fun (events: EventEnvelope<Collaboration.Event> list) -> 
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection mapToSerialization)
                           (Ok document.Collaborations)
                    |> Result.map (fun c -> { document with Collaborations = c })
                    |> Result.mapError (fun e -> $"%O{e}")
                )
                |> waitForDbChange logger

    module Namespace =
        open Namespace
        open ValueObjects
        open BridgeEventSourcingWithFilebasedDatabase
        
        let aggregate =
                Aggregate.From
                    Namespace.decide
                    Namespace.State.evolve
                    Namespace.State.Initial
        
        let useHandler stateBasedHandler =
            aggregate
            |> stateBasedHandler Namespace.name
            |> CommandHandler.getIdentityFromCommand Namespace.identify   

        let asEvents clock (boundedContext: Serialization.BoundedContext) =
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
                            Labels =
                                n.Labels
                                |> List.map (fun l ->
                                    { LabelId = l.Id
                                      Name = l.Name
                                      Value = Option.ofObj l.Value
                                      Template = l.Template })
                          }
                }
            )
        let asNamespaceWithBoundedContext (boundedContextOption: Serialization.BoundedContext option) event =
            boundedContextOption
            |> Option.map (fun boundedContext ->
                { boundedContext with Namespaces = Projections.asNamespaces boundedContext.Namespaces event })


        let subscription logger (database: SingleFileBasedDatastore): SubscriptionHandler<Event> =
            fun (events: EventEnvelope<Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection asNamespaceWithBoundedContext)
                           (Ok document.BoundedContexts)
                    |> Result.map (fun c -> { document with BoundedContexts = c })
                    |> Result.mapError (fun e -> $"%O{e}"))
                |> waitForDbChange logger

    module NamespaceTemplate =
        open NamespaceTemplate
        open BridgeEventSourcingWithFilebasedDatabase
        
        let aggregate =
                Aggregate.From
                    NamespaceTemplate.decide
                    NamespaceTemplate.State.evolve
                    NamespaceTemplate.State.Initial
        
        let useHandler stateBasedHandler =
            aggregate
            |> stateBasedHandler NamespaceTemplate.name
            |> CommandHandler.getIdentityFromCommand NamespaceTemplate.identify   

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

        let subscription logger (database: SingleFileBasedDatastore): SubscriptionHandler<Event> =
            fun (events: EventEnvelope<Event> list) ->
                database.Change(fun document ->
                    events
                    |> List.fold
                        (applyToCollection Projections.asTemplate)
                           (Ok document.NamespaceTemplates)
                    |> Result.map (fun c -> { document with NamespaceTemplates = c })
                    |> Result.mapError (fun e -> $"%O{e}"))
                |> waitForDbChange logger
