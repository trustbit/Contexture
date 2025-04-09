namespace Contexture.Api.ReadModels

open Contexture.Api
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.NamespaceTemplate.Projections
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Projections

module Defaults =
    let ReplyTimeout = (System.TimeSpan.FromSeconds(10)).TotalMilliseconds |> int |> Some

module Domain =

    open Contexture.Api.Aggregates.Domain
    open ValueObjects

    type AllDomainState =
        { Domains: Map<EventSource, Domain.State>
          Subdomains: Map<DomainId, Domain list> }
        static member Initial =
            { Domains = Map.empty
              Subdomains = Map.empty }

    type AllDomainReadModel = ReadModels.ReadModel<Domain.Event, AllDomainState>

    // ATM we reuse the state projection to reduce effort
    let private domainsProjection : Projection<State, Aggregates.Domain.Event> =
        { Init = State.Initial
          Update = State.evolve }

    let private asOption =
        function
        | Existing s -> Some s
        | _ -> None

    let private getDomains domainState =
        domainState
        |> Map.toList
        |> List.map snd
        |> List.choose asOption

    let private subdomainLookup (domains: Domain list) =
        domains
        |> List.groupBy (fun l -> l.ParentDomainId)
        |> List.choose (fun (key, values) -> key |> Option.map (fun parent -> (parent, values)))
        |> Map.ofList

    let domainsReadModel () : AllDomainReadModel =
        let updateState state eventEnvelopes =
            let domains =
                eventEnvelopes
                |> List.fold (projectIntoMapBySourceId domainsProjection) state.Domains

            { Domains = domains
              // this is brute force ATM - but probably good enough for a while
              Subdomains = domains |> getDomains |> subdomainLookup }

        ReadModels.readModel updateState AllDomainState.Initial Defaults.ReplyTimeout

    let allDomains readModel = readModel.Domains |> getDomains

    let subdomainsOf readModel = readModel.Subdomains

    let domain readModel domainId =
        readModel.Domains
        |> Map.tryFind domainId
        |> Option.bind asOption

module BoundedContext =
    open Contexture.Api.Aggregates.BoundedContext
    open ValueObjects
    open Projections


    type BoundedContextState =
        { BoundedContexts: Map<BoundedContextId, BoundedContext option>
          ByDomain: Map<DomainId, BoundedContext list> }
        static member Initial =
            { BoundedContexts = Map.empty
              ByDomain = Map.empty }

    type AllBoundedContextsReadModel = ReadModels.ReadModel<BoundedContext.Event, BoundedContextState>

    let private boundedContextProjection : Projection<BoundedContext option, Aggregates.BoundedContext.Event> =
        { Init = None
          Update = Projections.asBoundedContext }

    let private boundedContextsByDomainLookup (contexts: BoundedContext list) =
        contexts
        |> List.groupBy (fun l -> l.DomainId)
        |> Map.ofList

    let private allContexts state = state |> Map.toList |> List.choose snd

    let boundedContextsReadModel () : AllBoundedContextsReadModel =
        let updateState state eventEnvelopes =
            let contexts =
                eventEnvelopes
                |> List.fold (projectIntoMapBySourceId boundedContextProjection) state.BoundedContexts

            { BoundedContexts = contexts
              // this is brute force ATM - but probably good enough for a while
              ByDomain =
                  contexts
                  |> allContexts
                  |> boundedContextsByDomainLookup }

        ReadModels.readModel updateState BoundedContextState.Initial Defaults.ReplyTimeout

    let boundedContextLookup (state: BoundedContextState) =
        state.BoundedContexts
        |> Map.filter (fun _ v -> Option.isSome v)
        |> Map.map (fun _ v -> Option.get v)

    let allBoundedContexts (state: BoundedContextState) = state.BoundedContexts |> allContexts

    let boundedContextsByDomain (state: BoundedContextState) =
        fun domainId ->
            state.ByDomain
            |> Map.tryFind domainId
            |> Option.defaultValue []

    let boundedContext (state: BoundedContextState) boundedContextId =
        state.BoundedContexts
        |> Map.tryFind boundedContextId
        |> Option.flatten


module Collaboration =
    open Contexture.Api.Aggregates.Collaboration

    type CollaborationState = Map<EventSource, State>
    type AllCollaborationsReadModel = ReadModels.ReadModel<Event, CollaborationState>

    // ATM we reuse the state projection to reduce effort
    let private collaborationsProjection : Projection<State, Aggregates.Collaboration.Event> =
        { Init = Initial
          Update = State.evolve }

    let private asOption =
        function
        | Initial -> None
        | Existing e -> Some e
        | Deleted -> None

    let activeCollaborations (state: CollaborationState) =
        state
        |> Map.toList
        |> List.map snd
        |> List.choose asOption

    let collaboration (state: CollaborationState) collaborationId =
        state
        |> Map.tryFind collaborationId
        |> Option.bind asOption

    let collaborationsReadModel () =
        let updateState state eventEnvelopes =
            let collaborations =
                eventEnvelopes
                |> List.fold (projectIntoMapBySourceId collaborationsProjection) state

            collaborations

        ReadModels.readModel updateState Map.empty Defaults.ReplyTimeout

module Namespace =
    open Contexture.Api.Aggregates.Namespace
    open ValueObjects

    type NamespaceState =
        { NamespaceByNamespaceId: Map<NamespaceId, Projections.Namespace option>
          NamespaceByBoundedContextId: Map<BoundedContext.ValueObjects.BoundedContextId, Projections.Namespace list>
          BoundedContextIdByNamespaceId: Map<NamespaceId, BoundedContext.ValueObjects.BoundedContextId> }
        static member Empty =
            { NamespaceByNamespaceId = Map.empty
              NamespaceByBoundedContextId = Map.empty
              BoundedContextIdByNamespaceId = Map.empty }

    type AllNamespacesReadModel = ReadModels.ReadModel<Namespace.Event, NamespaceState>

    let private namespacesProjection : Projection<Projections.Namespace list, Aggregates.Namespace.Event> =
        { Init = List.empty
          Update = Projections.asNamespaces }

    let private namespaceProjection : Projection<Projections.Namespace option, Aggregates.Namespace.Event> =
        { Init = None
          Update = Projections.asNamespace }

    let selectNamespaceId =
        function
        | NamespaceImported e -> e.NamespaceId
        | NamespaceAdded e -> e.NamespaceId
        | NamespaceRemoved e -> e.NamespaceId
        | LabelAdded l -> l.NamespaceId
        | LabelRemoved l -> l.NamespaceId
        | LabelUpdated l -> l.NamespaceId

    let allNamespaces (state: NamespaceState) =
        state.NamespaceByNamespaceId
        |> Map.filter (fun _ v -> Option.isSome v)
        |> Map.map (fun _ v -> Option.get v)
        |> Map.toList
        |> List.map snd

    let namespacesOf (state: NamespaceState) boundedContextId =
        state.NamespaceByBoundedContextId
        |> Map.tryFind boundedContextId
        |> Option.defaultValue []

    module BoundedContexts =
        let projectNamespaceIdToBoundedContextId state eventEnvelope =
            match eventEnvelope.Event with
            | NamespaceAdded n -> state |> Map.add n.NamespaceId n.BoundedContextId
            | NamespaceImported n -> state |> Map.add n.NamespaceId n.BoundedContextId
            | NamespaceRemoved n -> state |> Map.remove n.NamespaceId
            | LabelAdded l -> state
            | LabelRemoved l -> state
            | LabelUpdated _ -> state

        let byNamespace (state: NamespaceState) (namespaceId: NamespaceId) =
            state.BoundedContextIdByNamespaceId
            |> Map.tryFind namespaceId

    let allNamespacesReadModel () =
        let updateState state eventEnvelopes =
            let byNamespaceId =
                eventEnvelopes
                |> List.fold
                    (projectIntoMap (fun e -> selectNamespaceId e.Event) namespaceProjection)
                    state.NamespaceByNamespaceId

            let byBoundedContextId =
                eventEnvelopes
                |> List.fold (projectIntoMapBySourceId namespacesProjection) state.NamespaceByBoundedContextId

            let boundedContextByNamespaceId =
                eventEnvelopes
                |> List.fold (BoundedContexts.projectNamespaceIdToBoundedContextId) state.BoundedContextIdByNamespaceId

            { state with
                  NamespaceByNamespaceId = byNamespaceId
                  NamespaceByBoundedContextId = byBoundedContextId
                  BoundedContextIdByNamespaceId = boundedContextByNamespaceId }

        ReadModels.readModel updateState NamespaceState.Empty Defaults.ReplyTimeout

module Templates =
    open Contexture.Api.Aggregates.NamespaceTemplate

    type TemplateState = Map<EventSource, NamespaceTemplate option>
    type AllTemplatesReadModel = ReadModels.ReadModel<Event, TemplateState>

    let private projection : Projection<NamespaceTemplate option, Aggregates.NamespaceTemplate.Event> =
        { Init = None
          Update = Projections.asTemplate }

    let templatesReadModel () =
        let updateState state eventEnvelopes =
            let templates =
                eventEnvelopes
                |> List.fold (projectIntoMapBySourceId projection) state

            templates

        ReadModels.readModel updateState Map.empty Defaults.ReplyTimeout

    let allTemplates (state: TemplateState) = state |> Map.toList |> List.choose snd

    let template (state: TemplateState) templateId =
        state |> Map.tryFind templateId |> Option.flatten

module EventLog =
    open Contexture.Api.AllEvents
    open System

    type EventModel = {
        EventType: string
        Timestamp: DateTimeOffset
        EventData: obj option
    }

    type BoundedContextState = {
        Id: BoundedContext.ValueObjects.BoundedContextId
        DomainId: BoundedContext.ValueObjects.DomainId
        Name: string
        ShortName: string option
    }

    type DomainState = {
        Id: Domain.ValueObjects.DomainId
        Name: string
        ShortName: string option
    }

    type EventLogState = {
        Events: Map<EventSource, EventModel list>
        BoundedContexts: Map<EventSource, BoundedContextState>
        Domains: Map<EventSource, DomainState>
    }

    type EventLogReadModel = ReadModels.ReadModel<AllEvents, EventLogState>

    let asEventData a = a :> obj |> Some

    let getBoundedContext state id fn =
        state.BoundedContexts |> Map.find id |> fn

    let getDomain state id fn =
        state.Domains |> Map.find id |> fn

    type StateModification = EventLogState -> EventLogState
    
    let prependEvent eventSource event : StateModification = fun state ->
        {state with Events = state.Events |> Map.change eventSource (Option.map(fun events -> [event] @ events  ) >> Option.orElse (Some [event]))}

    let updateBoundedContext (boundedContext: BoundedContextState) : StateModification = fun state ->
        {state with BoundedContexts = state.BoundedContexts |> Map.change boundedContext.Id (fun _ -> Some boundedContext) }

    let projectBoundedContextEvent state event metadata =
        match event with
        | BoundedContext.BoundedContextImported b -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.BoundedContextImported
                EventData = {|Name = b.Name; ShortName = b.ShortName|} |> asEventData 
            }
            [
                prependEvent b.BoundedContextId eventToAppend
                updateBoundedContext {Id = b.BoundedContextId; Name = b.Name; ShortName = b.ShortName; DomainId = b.DomainId}
            ]
        | BoundedContext.BoundedContextCreated b -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.BoundedContextCreated
                EventData = {| Name = b.Name|} |> asEventData 
            }
            [ 
                prependEvent b.BoundedContextId eventToAppend
                updateBoundedContext {Id = b.BoundedContextId; Name = b.Name; ShortName = None; DomainId = b.DomainId}
            ]
        | BoundedContext.BoundedContextRenamed b -> 
            let bc = getBoundedContext state b.BoundedContextId id
            let previousName = bc.Name
            let newName = b.Name
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.BoundedContextRenamed
                EventData = {| PreviousName = previousName; Name = b.Name |} |> asEventData 
            }
            [
                prependEvent b.BoundedContextId eventToAppend
                updateBoundedContext {bc with Name = newName}
            ]
        | BoundedContext.ShortNameAssigned b -> 
            let bc = getBoundedContext state b.BoundedContextId id
            let previousShortName = bc.ShortName
            let newShortName = b.ShortName
            let eventToAppend = {
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.ShortNameAssigned
                EventData = {| PreviousShortName = previousShortName; ShortName = newShortName |} |> asEventData 
            }
            [
                prependEvent b.BoundedContextId eventToAppend
                updateBoundedContext {bc with ShortName = newShortName }
            ]
        | BoundedContext.BoundedContextRemoved b -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.BoundedContextRemoved
                EventData = None 
            }
            [
                prependEvent b.BoundedContextId eventToAppend
            ]
        | BoundedContext.BoundedContextMovedToDomain b ->
            let bc  = getBoundedContext state b.BoundedContextId id
            let previousDomainName = getDomain state b.OldDomainId (fun d -> d.Name)
            let domainName = getDomain state b.DomainId (fun d -> d.Name)
            let eventToAppend =  { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.BoundedContextMovedToDomain
                EventData = {|PreviousDomainName = previousDomainName; DomainName = domainName|} |> asEventData 
            }
            [
                prependEvent b.BoundedContextId eventToAppend
                updateBoundedContext {bc with DomainId = b.DomainId}
            ]
        | BoundedContext.BoundedContextReclassified b -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.BoundedContextReclassified
                EventData = None
            }
            [
                prependEvent b.BoundedContextId eventToAppend
            ]
        | BoundedContext.DescriptionChanged b ->
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.DescriptionChanged
                EventData = {| NewDescription = b.Description |} |> asEventData
            }
            [
                prependEvent b.BoundedContextId eventToAppend
            ]
        | BoundedContext.BusinessDecisionsUpdated b -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.BusinessDecisionsUpdated
                EventData = None
            }
            [
                prependEvent b.BoundedContextId eventToAppend
            ]
        | BoundedContext.UbiquitousLanguageUpdated b -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.UbiquitousLanguageUpdated
                EventData = None
            }
            [
                prependEvent b.BoundedContextId eventToAppend
            ]
        | BoundedContext.DomainRolesUpdated b -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.DomainRolesUpdated
                EventData = None
            }
            [
                prependEvent b.BoundedContextId eventToAppend
            ]
        | BoundedContext.MessagesUpdated b -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof BoundedContext.MessagesUpdated
                EventData = None
            }
            [
                prependEvent b.BoundedContextId eventToAppend
            ]

    let updateDomain (domain: DomainState) : StateModification = fun state ->
        { state with Domains = state.Domains |> Map.change domain.Id (fun _ -> Some domain) }

    let projectDomainEvent state event metadata =
        match event with
        | Domain.DomainImported d -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof Domain.DomainImported
                EventData = {| Name = d.Name; ShortName = d.ShortName |} |> asEventData 
            }
            [
                prependEvent d.DomainId eventToAppend
                updateDomain { Id = d.DomainId; Name = d.Name; ShortName = None }
            ]
        | Domain.DomainCreated d -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof Domain.DomainCreated
                EventData = {| Name = d.Name |} |> asEventData 
            }
            [
                prependEvent d.DomainId eventToAppend
                updateDomain { Id = d.DomainId; Name = d.Name; ShortName = None }
            ]
        | Domain.SubDomainCreated d -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof Domain.SubDomainCreated
                EventData = {| Name = d.Name |} |> asEventData 
            }
            [
                prependEvent d.ParentDomainId eventToAppend
                prependEvent d.DomainId eventToAppend
                updateDomain { Id = d.DomainId; Name = d.Name; ShortName = None }
            ]
        | Domain.DomainRenamed d -> 
            let domain = getDomain state d.DomainId id
            let previousName = domain.Name
            let newName = d.Name
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof Domain.DomainRenamed
                EventData = {| PreviousName = previousName; Name = newName |} |> asEventData 
            }
            [
                prependEvent d.DomainId eventToAppend
                updateDomain { domain with Name = newName }
            ]
        | Domain.ShortNameAssigned d -> 
            let domain = getDomain state d.DomainId id
            let previousShortName = domain.ShortName
            let newShortName = d.ShortName
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof Domain.ShortNameAssigned
                EventData = {| PreviousShortName = previousShortName; ShortName = newShortName |} |> asEventData 
            }
            [
                prependEvent d.DomainId eventToAppend
                updateDomain { domain with ShortName = newShortName }
            ]
        | Domain.VisionRefined d -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof Domain.VisionRefined
                EventData = None
            }
            [
                prependEvent d.DomainId eventToAppend
            ]
        | Domain.DomainRemoved d -> 
            let eventToAppend = { 
                Timestamp = metadata.RecordedAt
                EventType = nameof Domain.DomainRemoved
                EventData = None 
            }
            [
                prependEvent d.DomainId eventToAppend
            ]
        | _ -> []


    let projectEvent (state: EventLogState) (event: EventEnvelope<AllEvents>) =
        try
            let modifications =
                match event.Event with
                | BoundedContexts boundedContextEvent -> projectBoundedContextEvent state boundedContextEvent event.Metadata
                | Domains domainEvent -> projectDomainEvent state domainEvent event.Metadata
                | Namespaces _namespaceEvent -> []
                | NamespaceTemplates _templateEvent -> []
                | Collaboration _collaborationEvent -> []

            modifications |> List.fold (fun acc modify -> modify acc) state
        with
        | ex -> 
            printfn $"{ex}"
            state


    let eventLogReadModel () : EventLogReadModel =
        let updateState state eventEnvelopes =
            eventEnvelopes
            |> List.fold projectEvent state

        ReadModels.readModel updateState {Events = Map.empty; BoundedContexts = Map.empty; Domains = Map.empty} Defaults.ReplyTimeout

    let eventsForEntity (state: EventLogState) entityId =
        state.Events
        |> Map.tryFind entityId
        |> Option.defaultValue []
