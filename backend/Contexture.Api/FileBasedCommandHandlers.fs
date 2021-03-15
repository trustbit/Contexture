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
        let create (database: FileBased) domainId command =
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
        
        
        let handle (database: FileBased) (command: Command) =
            match command with
            | CreateBoundedContext (domainId,createBc) -> create database domainId createBc