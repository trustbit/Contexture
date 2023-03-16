namespace Contexture.Api

open Contexture.Api.Infrastructure

module CommandHandler =
    type CommandHandlerError<'T, 'Id> =
        | DomainError of 'T
        | InfrastructureError of InfrastructureError<'Id>

    and InfrastructureError<'Id> =
        | Exception of exn
        | EntityNotFound of 'Id

    type HandleIdentityAndCommand<'Identity,'Command,'Error> = 'Identity -> 'Command -> Async<Result<'Identity * Version * Position option,CommandHandlerError<'Error,'Identity>>>
    type HandleCommand<'Identity,'Command,'Error> = 'Command -> Async<Result<'Identity * Version * Position option,CommandHandlerError<'Error,'Identity>>>
    
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
            let! version,stream = loadStream identity
            
            let state =
                stream
                |> List.fold aggregate.Evolve aggregate.Initial
                
            match aggregate.Decider command state with
            | Ok newEvents ->
                match NonEmptyList.fromList newEvents with
                | Some newEvents ->
                    let! version,position = saveStream identity version newEvents
                    return Ok (identity,version, Some position)
                | None ->
                    return Ok (identity, version, None)
            | Error e ->
                return e |> DomainError |> Error
        }
    
    module EventBased =
        type GetStreamName<'Identity> = 'Identity -> EventSource
        type StreamBasedCommandHandler<'Identity,'State,'Cmd,'Event,'Error> = GetStreamName<'Identity> -> Aggregate<'State,'Cmd,'Event,'Error> -> HandleIdentityAndCommand<'Identity, 'Cmd,'Error>
        
        let eventStoreBasedCommandHandler (eventStore:EventStore) : StreamBasedCommandHandler<_,_,_,_,_> =
            fun getStreamName aggregate  ->
                let loadStream identity = async {
                    let streamName = getStreamName identity
                    match! eventStore.Stream streamName Version.start with
                    | Ok (version, stream)->
                        return version, List.map (fun e -> e.Event) stream
                    | Error e ->
                        return failwithf "Failed to get stream %O with:\n%O" streamName e 
                    }
                
                let saveStream identity version newEvents = async {
                    let name = getStreamName identity                        
                    match! eventStore.Append name (AtVersion version) newEvents with
                    | Ok appendedVersion -> return appendedVersion
                    | Error e -> return failwithf "Failed to save events for %O with:\n%O" name e
                }
                
                handleWithStream loadStream saveStream aggregate

    module Decider =
        open System.Collections.Generic
        let private executeCommandSequentially execute commands =
            async {
                let results = List()
                for command in commands do
                    let! result = execute command
                    results.Add result
                return results |> List.ofSeq
            }
        let batch (executeCommand: HandleCommand<'identity,'c,'error>) (commands: 'c list) =
            async {
                let! results =
                    executeCommandSequentially executeCommand commands
                let (position, errors) =
                    results
                    |> List.fold (fun (events, errors) result ->
                        match result with
                        | Ok (_,_,position) ->(Some position, errors)
                        | Error error -> (events, errors @ [ error ])
                        )
                        (None,List.empty)
                
                if not(List.isEmpty errors) then
                    return Error errors
                else
                    return Ok position
            }
module FileBasedCommandHandlers =
    open Contexture.Api.Aggregates
    open CommandHandler
    open CommandHandler.EventBased
    
    module Domain =
        open Contexture.Api.Aggregates.Domain
        
        let aggregate =
                Aggregate.From
                    Domain.decide
                    Domain.State.evolve
                    Domain.State.Initial
                    
        
        let useHandler stateBasedHandler =
            aggregate
            |> stateBasedHandler Domain.name
            |> CommandHandler.getIdentityFromCommand Domain.identify   

    module BoundedContext =

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

        
    module Collaboration =
        
        let aggregate =
                Aggregate.From
                    Collaboration.decide
                    Collaboration.State.evolve
                    Collaboration.State.Initial
        
        let useHandler stateBasedHandler =
            aggregate
            |> stateBasedHandler Collaboration.name
            |> CommandHandler.getIdentityFromCommand Collaboration.identify               
            
    module Namespace =
        open Namespace
        open ValueObjects
        
        let aggregate =
                Aggregate.From
                    Namespace.decide
                    Namespace.State.evolve
                    Namespace.State.Initial
        
        let useHandler stateBasedHandler =
            aggregate
            |> stateBasedHandler Namespace.name
            |> CommandHandler.getIdentityFromCommand Namespace.identify           

    module NamespaceTemplate =
        open NamespaceTemplate
        
        let aggregate =
                Aggregate.From
                    NamespaceTemplate.decide
                    NamespaceTemplate.State.evolve
                    NamespaceTemplate.State.Initial
        
        let useHandler stateBasedHandler =
            aggregate
            |> stateBasedHandler NamespaceTemplate.name
            |> CommandHandler.getIdentityFromCommand NamespaceTemplate.identify   
