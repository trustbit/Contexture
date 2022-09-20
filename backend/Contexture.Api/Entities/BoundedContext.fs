module Contexture.Api.Aggregates.BoundedContext

open Contexture.Api
open Contexture.Api.Aggregates
open System

module ValueObjects =
    type DomainId = Domain.ValueObjects.DomainId
    type DomainType =
        | Core
        | Supporting
        | Generic
        | OtherDomainType of string

    type BusinessModel =
        | Revenue
        | Engagement
        | Compliance
        | CostReduction
        | OtherBusinessModel of string

    type Evolution =
        | Genesis
        | CustomBuilt
        | Product
        | Commodity

    type StrategicClassification =
        { DomainType: DomainType option
          BusinessModel: BusinessModel list
          Evolution: Evolution option }
        static member Unknown =
            { DomainType = None
              BusinessModel = []
              Evolution = None }

    type BusinessDecision = { Name: string; Description: string }

    type UbiquitousLanguageTerm =
        { Term: string
          Description: string option }

    type Message = string
    type Command = Message
    type Event = Message
    type Query = Message

    type Messages =
        { CommandsHandled: Command list
          CommandsSent: Command list
          EventsHandled: Event list
          EventsPublished: Event list
          QueriesHandled: Query list
          QueriesInvoked: Query list }
        static member Empty =
            { CommandsHandled = []
              CommandsSent = []
              EventsHandled = []
              EventsPublished = []
              QueriesHandled = []
              QueriesInvoked = [] }

    type DomainRole =
        { Name: string
          Description: string option }
        
    type BoundedContextId = Guid


open Domain.ValueObjects
open ValueObjects

type Command =
    | CreateBoundedContext of BoundedContextId * DomainId * CreateBoundedContext
    | RenameBoundedContext of BoundedContextId * RenameBoundedContext
    | AssignShortName of BoundedContextId * AssignShortName
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

and AssignShortName = { ShortName: string }

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
    | ShortNameAssigned of ShortNameAssigned
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
      ShortName: string option
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

and ShortNameAssigned =
    { BoundedContextId: BoundedContextId
      ShortName: string option }

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
    | AssignShortName (contextId, _) -> contextId
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

let assignShortNameToBoundedContext shortName boundedContextId =
    ShortNameAssigned
        { BoundedContextId = boundedContextId
          ShortName =
              shortName
              |> Option.ofObj
              |> Option.filter (String.IsNullOrWhiteSpace >> not) }
    |> Ok


let decide (command: Command) state =
    match command with
    | CreateBoundedContext (id, domainId, createBc) -> newBoundedContext id domainId createBc.Name
    | RenameBoundedContext (contextId, rename) -> renameBoundedContext rename.Name contextId
    | AssignShortName (contextId, shortName) -> assignShortNameToBoundedContext shortName.ShortName contextId
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
    type BoundedContext =
        { Id: BoundedContextId
          DomainId: DomainId
          ShortName: string option
          Name: string
          Description: string option
          Classification: StrategicClassification
          BusinessDecisions: BusinessDecision list
          UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
          Messages: Messages
          DomainRoles: DomainRole list }
    let asBoundedContext (state: BoundedContext option) event =
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
                          ShortName = c.ShortName
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
                      ShortName = c.ShortName
                      Name = c.Name }
        | BoundedContextCreated c ->
            Some
                { Id = c.BoundedContextId
                  DomainId = c.DomainId
                  Description = None
                  Name = c.Name
                  ShortName = None
                  Messages = Messages.Empty
                  Classification = StrategicClassification.Unknown
                  DomainRoles = []
                  BusinessDecisions = []
                  UbiquitousLanguage = Map.empty }
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
        | ShortNameAssigned c ->
            state
            |> Option.map (fun o -> { o with ShortName = c.ShortName })
        | UbiquitousLanguageUpdated c ->
            state
            |> Option.map (fun o ->
                { o with
                      UbiquitousLanguage = c.UbiquitousLanguage })
