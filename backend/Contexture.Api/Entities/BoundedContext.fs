module Contexture.Api.Aggregates.BoundedContext

open Contexture.Api.Entities
open System

type Command =
    | CreateBoundedContext of BoundedContextId * DomainId * CreateBoundedContext
    | RenameBoundedContext of BoundedContextId * RenameBoundedContext
    | AssignKey of BoundedContextId * AssignKey
    | RemoveBoundedContext of BoundedContextId
    | MoveBoundedContextToDomain of BoundedContextId * MoveBoundedContextToDomain
    | ReclassifyBoundedContext of BoundedContextId * ReclassifyBoundedContext
    | ChangeDescription of BoundedContextId * ChangeDescription
    // TODO: replace with add/remove instead of updateing all
    | UpdateBusinessDecisions of BoundedContextId * UpdateBusinessDecisions
    | UpdateUbiquitousLanguage of BoundedContextId * UpdateUbiquitousLanguage
    | UpdateDomainRoles of BoundedContextId * UpdateDomainRoles
    | UpdateMessages of BoundedContextId * UpdateMessages

and CreateBoundedContext = { Name: string }
and RenameBoundedContext = { Name: string }

and AssignKey = { Key: string }

and MoveBoundedContextToDomain = { ParentDomainId: DomainId }

and ReclassifyBoundedContext =
    { Classification: StrategicClassification }

and ChangeDescription = { Description: string option }

and UpdateBusinessDecisions =
    { BusinessDecisions: BusinessDecision list }

and UpdateUbiquitousLanguage =
    { UbiquitousLanguage: Map<string, UbiquitousLanguageTerm> }

and UpdateMessages = { Messages: Messages }

and UpdateDomainRoles = { DomainRoles: DomainRole list }

type Event =
    | BoundedContextImported of BoundedContextImported
    | BoundedContextCreated of BoundedContextCreated
    | BoundedContextRenamed of BoundedContextRenamed
    | KeyAssigned of KeyAssigned
    | BoundedContextRemoved of BoundedContextRemoved
    | BoundedContextMovedToDomain of BoundedContextMovedToDomain
    | BoundedContextReclassified of BoundedContextReclassified
    | DescriptionChanged of DescriptionChanged
    // TODO: replace with add/remove instead of updateing all
    | BusinessDecisionsUpdated of BusinessDecisionsUpdated
    | UbiquitousLanguageUpdated of UbiquitousLanguageUpdated
    | DomainRolesUpdated of DomainRolesUpdated
    | MessagesUpdated of MessagesUpdated

and BoundedContextImported =
    { BoundedContextId: BoundedContextId
      DomainId: DomainId
      Key: string option
      Name: string
      Description: string option
      Classification: StrategicClassification
      BusinessDecisions: BusinessDecision list
      UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
      Messages: Messages
      DomainRoles: DomainRole list }

and BoundedContextCreated =
    { BoundedContextId: BoundedContextId
      DomainId: DomainId
      Name: string }

and BoundedContextRenamed =
    { BoundedContextId: BoundedContextId
      Name: string }

and BoundedContextRemoved = { BoundedContextId: BoundedContextId }

and BoundedContextMovedToDomain =
    { BoundedContextId: BoundedContextId
      DomainId: DomainId }

and DescriptionChanged =
    { BoundedContextId: BoundedContextId
      Description: string option }

and KeyAssigned =
    { BoundedContextId: BoundedContextId
      Key: string option }

and BoundedContextReclassified =
    { BoundedContextId: BoundedContextId
      Classification: StrategicClassification }

and BusinessDecisionsUpdated =
    { BoundedContextId: BoundedContextId
      BusinessDecisions: BusinessDecision list }

and UbiquitousLanguageUpdated =
    { BoundedContextId: BoundedContextId
      UbiquitousLanguage: Map<string, UbiquitousLanguageTerm> }

and DomainRolesUpdated =
    { BoundedContextId: BoundedContextId
      DomainRoles: DomainRole list }

and MessagesUpdated =
    { BoundedContextId: BoundedContextId
      Messages: Messages }

type Errors = | EmptyName

let identify =
    function
    | CreateBoundedContext (contextId, _, _) -> contextId
    | RenameBoundedContext (contextId, _) -> contextId
    | ChangeDescription (contextId, _) -> contextId
    | RemoveBoundedContext contextId -> contextId
    | UpdateDomainRoles (contextId, _) -> contextId
    | UpdateUbiquitousLanguage (contextId, _) -> contextId
    | UpdateMessages (contextId, _) -> contextId
    | UpdateBusinessDecisions (contextId, _) -> contextId
    | ReclassifyBoundedContext (contextId, _) -> contextId
    | AssignKey (contextId, _) -> contextId
    | MoveBoundedContextToDomain (contextId, _) -> contextId

let name identity = identity

type State =
    | Initial
    | Existing
    | Deleted
    static member evolve (state: State) (event: Event) =
        match event with
        | BoundedContextRemoved _ -> Deleted
        | _ -> Existing

let nameValidation name =
    if String.IsNullOrWhiteSpace name then Error EmptyName else Ok name

let newBoundedContext id domainId name =
    name
    |> nameValidation
    |> Result.map (fun name ->
        BoundedContextCreated
            { BoundedContextId = id
              DomainId = domainId
              Name = name })

let renameBoundedContext potentialName boundedContextId =
    potentialName
    |> nameValidation
    |> Result.map (fun name ->
        BoundedContextRenamed
            { Name = name
              BoundedContextId = boundedContextId })

let assignKeyToBoundedContext key boundedContextId =
    KeyAssigned
        { BoundedContextId = boundedContextId
          Key =
              key
              |> Option.ofObj
              |> Option.filter (String.IsNullOrWhiteSpace >> not) }
    |> Ok


let decide (command: Command) state =
    match command with
    | CreateBoundedContext (id, domainId, createBc) -> newBoundedContext id domainId createBc.Name
    | RenameBoundedContext (contextId, rename) -> renameBoundedContext rename.Name contextId
    | AssignKey (contextId, key) -> assignKeyToBoundedContext key.Key contextId
    | RemoveBoundedContext contextId ->
        BoundedContextRemoved { BoundedContextId = contextId }
        |> Ok
    | MoveBoundedContextToDomain (contextId, move) ->
        BoundedContextMovedToDomain
            { DomainId = move.ParentDomainId
              BoundedContextId = contextId }
        |> Ok
    | ReclassifyBoundedContext (contextId, classification) ->
        BoundedContextReclassified
            { Classification = classification.Classification
              BoundedContextId = contextId }
        |> Ok
    | ChangeDescription (contextId, descriptionText) ->
        DescriptionChanged
            { Description = descriptionText.Description
              BoundedContextId = contextId }
        |> Ok
    | UpdateBusinessDecisions (contextId, decisions) ->
        BusinessDecisionsUpdated
            { BusinessDecisions = decisions.BusinessDecisions
              BoundedContextId = contextId }
        |> Ok
    | UpdateUbiquitousLanguage (contextId, language) ->
        UbiquitousLanguageUpdated
            { UbiquitousLanguage = language.UbiquitousLanguage
              BoundedContextId = contextId }
        |> Ok
    | UpdateDomainRoles (contextId, roles) ->
        DomainRolesUpdated
            { DomainRoles = roles.DomainRoles
              BoundedContextId = contextId }
        |> Ok
    | UpdateMessages (contextId, roles) ->
        MessagesUpdated
            { Messages = roles.Messages
              BoundedContextId = contextId }
        |> Ok
    |> Result.map List.singleton

module Projections =
    let asBoundedContext state event =
        match event with
        | BoundedContextImported c ->
            match state with
            | Some s ->
                Some
                    { s with
                          Id = c.BoundedContextId
                          DomainId = c.DomainId
                          Description = c.Description
                          Messages = c.Messages
                          Classification = c.Classification
                          DomainRoles = c.DomainRoles
                          UbiquitousLanguage = c.UbiquitousLanguage
                          BusinessDecisions = c.BusinessDecisions
                          Key = c.Key
                          Name = c.Name }
            | None ->
                Some
                    { Id = c.BoundedContextId
                      DomainId = c.DomainId
                      Description = c.Description
                      Messages = c.Messages
                      Classification = c.Classification
                      DomainRoles = c.DomainRoles
                      UbiquitousLanguage = c.UbiquitousLanguage
                      BusinessDecisions = c.BusinessDecisions
                      Key = c.Key
                      Name = c.Name
                      Namespaces = [] }
        | BoundedContextCreated c ->
            Some
                { Id = c.BoundedContextId
                  DomainId = c.DomainId
                  Description = None
                  Name = c.Name
                  Key = None
                  Messages = Messages.Empty
                  Classification = StrategicClassification.Unknown
                  DomainRoles = []
                  BusinessDecisions = []
                  UbiquitousLanguage = Map.empty
                  Namespaces = [] }
        | BoundedContextRemoved c -> None
        | BoundedContextRenamed c ->
            state
            |> Option.map (fun o -> { o with Name = c.Name })
        | BoundedContextMovedToDomain c ->
            state
            |> Option.map (fun o -> { o with DomainId = c.DomainId })
        | BoundedContextReclassified c ->
            state
            |> Option.map (fun o ->
                { o with
                      Classification = c.Classification })
        | BusinessDecisionsUpdated c ->
            state
            |> Option.map (fun o ->
                { o with
                      BusinessDecisions = c.BusinessDecisions })
        | DomainRolesUpdated c ->
            state
            |> Option.map (fun o -> { o with DomainRoles = c.DomainRoles })
        | MessagesUpdated c ->
            state
            |> Option.map (fun o -> { o with Messages = c.Messages })
        | DescriptionChanged c ->
            state
            |> Option.map (fun o -> { o with Description = c.Description })
        | KeyAssigned c ->
            state
            |> Option.map (fun o -> { o with Key = c.Key })
        | UbiquitousLanguageUpdated c ->
            state
            |> Option.map (fun o ->
                { o with
                      UbiquitousLanguage = c.UbiquitousLanguage })
