namespace Contexture.Api.Apis

open System
open Contexture.Api.Aggregates
open Contexture.Api
open Contexture.Api.Infrastructure

open Microsoft.AspNetCore.Http

open Giraffe

module BoundedContexts =
    open Contexture.Api.Aggregates.Namespace
    open Contexture.Api.Aggregates.BoundedContext
    open ValueObjects
    module Results =
        
        type BoundedContextResult =
            { Id: BoundedContextId
              ParentDomainId: DomainId
              ShortName: string option
              Name: string
              Description: string option
              Classification: StrategicClassification
              BusinessDecisions: BusinessDecision list
              UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
              Messages: Messages
              DomainRoles: DomainRole list
              Domain: Domain.Domain
              Namespaces: Projections.Namespace list }

        let convertBoundedContextWithDomain
            (findDomain: DomainId -> Domain.Domain option)
            (findNamespaces: BoundedContextId -> Projections.Namespace list)
            (boundedContext: Projections.BoundedContext)
            =
            boundedContext.DomainId
            |> findDomain
            |> Option.map (fun domain ->
                { Id = boundedContext.Id
                  ParentDomainId = boundedContext.DomainId
                  ShortName = boundedContext.ShortName
                  Name = boundedContext.Name
                  Description = boundedContext.Description
                  Classification = boundedContext.Classification
                  BusinessDecisions = boundedContext.BusinessDecisions
                  UbiquitousLanguage = boundedContext.UbiquitousLanguage
                  Messages = boundedContext.Messages
                  DomainRoles = boundedContext.DomainRoles
                  Domain = domain
                  Namespaces = boundedContext.Id |> findNamespaces }
                )

    module CommandEndpoints =
        open System
        open FileBasedCommandHandlers
        open CommandHandler
        open ReadModels
        let private updateAndReturnBoundedContext command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()
                    let eventStoreBased = EventBased.eventStoreBasedCommandHandler database
                    match! BoundedContext.useHandler eventStoreBased command with
                    | Ok (updatedContext,_,position) ->
                        return! redirectTo false (State.appendProcessedPosition (sprintf "/api/boundedcontexts/%O" updatedContext) position) next ctx
                    | Error (DomainError EmptyName) ->
                        return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let rename contextId (command: RenameBoundedContext) =
            updateAndReturnBoundedContext (RenameBoundedContext(contextId, command))

        let shortName contextId (command: AssignShortName) =
            updateAndReturnBoundedContext (AssignShortName(contextId, command))

        let move contextId (command: MoveBoundedContextToDomain) =
            updateAndReturnBoundedContext (MoveBoundedContextToDomain(contextId, command))

        let reclassify contextId (command: ReclassifyBoundedContext) =
            updateAndReturnBoundedContext (ReclassifyBoundedContext(contextId, command))

        let description contextId (command: ChangeDescription) =
            updateAndReturnBoundedContext (ChangeDescription(contextId, command))

        let businessDecisions contextId (command: UpdateBusinessDecisions) =
            updateAndReturnBoundedContext (UpdateBusinessDecisions(contextId, command))

        let ubiquitousLanguage contextId (command: UpdateUbiquitousLanguage) =
            updateAndReturnBoundedContext (UpdateUbiquitousLanguage(contextId, command))

        let domainRoles contextId (command: UpdateDomainRoles) =
            updateAndReturnBoundedContext (UpdateDomainRoles(contextId, command))

        let messages contextId (command: UpdateMessages) =
            updateAndReturnBoundedContext (UpdateMessages(contextId, command))

        let removeAndReturnId contextId =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()
                    let eventStoreBased = EventBased.eventStoreBasedCommandHandler database
                    match! BoundedContext.useHandler eventStoreBased (RemoveBoundedContext contextId) with
                    | Ok (id,version,_) -> return! json id next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

    module QueryEndpoints =
        open Contexture.Api.ReadModels
        open Contexture.Api.ReadModels.Find
        open ReadModels

        let private mapToBoundedContext namespaceState domainState boundedContextState ids =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let namespacesOf =
                    Namespace.namespacesOf namespaceState

                let boundedContextLookup =
                    ReadModels.BoundedContext.boundedContextLookup boundedContextState

                let boundedContexts =
                    ids
                    |> List.choose (fun id -> boundedContextLookup |> Map.tryFind id)
                    |> List.choose (Results.convertBoundedContextWithDomain (Domain.domain domainState) namespacesOf)

                return! json boundedContexts next ctx
            }

        [<CLIMutable>]
        type Query =
            { Label: SearchFor.Labels.LabelQuery option
              Namespace: SearchFor.NamespaceId.NamespaceQuery option
              Domain: SearchFor.DomainId.DomainQuery option
              BoundedContext: SearchFor.BoundedContextId.BoundedContextQuery option }
            member this.IsActive =
                [ this.Label |> Option.map (fun l -> l.IsActive)
                  this.Namespace |> Option.map (fun n -> n.IsActive)
                  this.Domain |> Option.map (fun n -> n.IsActive)
                  this.BoundedContext
                  |> Option.map (fun n -> n.IsActive) ]
                |> List.map (Option.defaultValue false)
                |> List.exists id
            static member ValidKeys =
                let dummy = Unchecked.defaultof<Query>
                [ SearchFor.Labels.LabelQuery.ValidKeys |> List.map (fun name -> $"{nameof dummy.Label}.{name}")
                  SearchFor.NamespaceId.NamespaceQuery.ValidKeys |> List.map (fun name -> $"{nameof dummy.Namespace}.{name}")
                  SearchFor.DomainId.DomainQuery.ValidKeys |> List.map (fun name -> $"{nameof dummy.Domain}.{name}")
                  SearchFor.BoundedContextId.BoundedContextQuery.ValidKeys |> List.map (fun name -> $"{nameof dummy.BoundedContext}.{name}")
                ]
                |> List.collect id
                |> Set.ofList

        let getBoundedContextsByQuery (item: Query) =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! domainState = ctx |> State.fetch State.fromReadModel<ReadModels.Domain.AllDomainReadModel>
                let! boundedContextState = ctx |> State.fetch State.fromReadModel<ReadModels.BoundedContext.AllBoundedContextsReadModel>
                let! namespaceState = ctx |> State.fetch State.fromReadModel<ReadModels.Namespace.AllNamespacesReadModel>
                let! boundedContextFindState = ctx |> State.fetch State.fromReadModel<Find.BoundedContexts.ReadModel>
                let! domainFindState = ctx |> State.fetch State.fromReadModel<Find.Domains.ReadModel>
                let! labelFindState = ctx |> State.fetch State.fromReadModel<Find.Labels.ReadModel>
                let! namespaceFindState = ctx |> State.fetch State.fromReadModel<Find.Namespaces.ReadModel>

                let namespaceIds =
                    SearchFor.NamespaceId.find namespaceFindState item.Namespace

                let domainIds =
                    SearchFor.DomainId.find domainFindState item.Domain

                let boundedContextIdsFromLabels =
                    SearchFor.Labels.find labelFindState item.Label

                let boundedContextIdsFromSearch =
                    SearchFor.BoundedContextId.find boundedContextFindState item.BoundedContext

                let boundedContextsByNamespace =
                    Namespace.BoundedContexts.byNamespace namespaceState

                let boundedContextsByDomain =
                    boundedContextState
                    |> ReadModels.BoundedContext.boundedContextsByDomain

                let boundedContextIdsFromNamespace =
                    namespaceIds
                    |> Find.SearchResult.bind (
                        Set.map (
                            boundedContextsByNamespace
                            >> Option.toList
                            >> Set.ofList
                        )
                       >> Find.SearchResult.fromManyResults
                    )

                let boundedContextIdsFromDomain =
                    domainIds
                    |> Find.SearchResult.bind (
                        Set.map (
                            boundedContextsByDomain
                            >> List.map (fun b -> b.Id)
                            >> Set.ofList
                        )
                        >> Find.SearchResult.fromManyResults
                    )

                let boundedContextIds =
                    Find.SearchResult.combineResults
                        [ boundedContextIdsFromSearch
                          boundedContextIdsFromNamespace
                          boundedContextIdsFromDomain
                          boundedContextIdsFromLabels ]
                        
                let idsToLoad =
                    boundedContextIds
                    |> Find.SearchResult.value
                    |> Option.map Set.toList
                    |> Option.defaultValue List.empty

                return! mapToBoundedContext namespaceState domainState boundedContextState idsToLoad next ctx
            }

        let getBoundedContexts =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! domainState = ctx |> State.fetch State.fromReadModel<ReadModels.Domain.AllDomainReadModel>
                let! boundedContextState = ctx |> State.fetch State.fromReadModel<ReadModels.BoundedContext.AllBoundedContextsReadModel>
                let! namespaceState = ctx |> State.fetch State.fromReadModel<ReadModels.Namespace.AllNamespacesReadModel>
                let lookup =
                    boundedContextState
                    |> BoundedContext.boundedContextLookup
                let allContexts =
                    lookup
                    |> Map.toList
                    |> List.map fst

                return! mapToBoundedContext namespaceState domainState boundedContextState allContexts next ctx
            }

        let getOrSearchBoundedContexts =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let nextHandler =
                    if ctx.Request.QueryString.HasValue then
                        let requestKeys =
                            ctx.Request.Query.Keys
                            |> Set.ofSeq
                            |> Set.map (fun s -> s.ToLowerInvariant())
                        let lowerCaseKeys =
                            Query.ValidKeys |> Set.map (fun s -> s.ToLowerInvariant())
                        if lowerCaseKeys |> Set.isSubset requestKeys then
                            match ctx.TryBindQueryString<Query>() with
                            | Ok query when query.IsActive -> getBoundedContextsByQuery query
                            | _ -> getBoundedContexts
                        else
                            let unknownQueryParameter =
                                lowerCaseKeys
                                |> Set.difference requestKeys
                                |> String.concat ", "
                            let supportedQueryParameters =
                                Query.ValidKeys
                                |> String.concat ", "
                            RequestErrors.badRequest (text $"Unknown query parameters(s):\n{unknownQueryParameter}\n\nSupported query parameters:\n{supportedQueryParameters}")
                    else
                        getBoundedContexts

                return! nextHandler next ctx
            }

        let getBoundedContext contextId =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! domainState = ctx |> State.fetch State.fromReadModel<ReadModels.Domain.AllDomainReadModel>
                let! boundedContextState = ctx |> State.fetch State.fromReadModel<ReadModels.BoundedContext.AllBoundedContextsReadModel>
                let! namespaceState = ctx |> State.fetch State.fromReadModel<ReadModels.Namespace.AllNamespacesReadModel>

                let namespacesOf =
                    Namespace.namespacesOf namespaceState
                    
                let boundedContext = 
                    contextId
                    |> BoundedContext.boundedContext boundedContextState
                let result =
                    boundedContext
                    |> Option.bind (Results.convertBoundedContextWithDomain (Domain.domain domainState) namespacesOf)
                    |> Option.map json
                    |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "BoundedContext %O not found" contextId))

                return! result next ctx
            }

    let routes : HttpHandler =
        subRouteCi
            "/boundedcontexts"
            (choose [ subRoutef
                          "/%O"
                          (fun contextId ->
                              (choose [ Namespaces.routesForBoundedContext contextId
                                        GET >=> QueryEndpoints.getBoundedContext contextId
                                        POST
                                        >=> route "/rename"
                                        >=> bindJson (CommandEndpoints.rename contextId)
                                        POST
                                        >=> route "/shortName"
                                        >=> bindJson (CommandEndpoints.shortName contextId)
                                        POST
                                        >=> route "/move"
                                        >=> bindJson (CommandEndpoints.move contextId)
                                        POST
                                        >=> route "/reclassify"
                                        >=> bindJson (CommandEndpoints.reclassify contextId)
                                        POST
                                        >=> route "/description"
                                        >=> bindJson (CommandEndpoints.description contextId)
                                        POST
                                        >=> route "/businessDecisions"
                                        >=> bindJson (CommandEndpoints.businessDecisions contextId)
                                        POST
                                        >=> route "/ubiquitousLanguage"
                                        >=> bindJson (CommandEndpoints.ubiquitousLanguage contextId)
                                        POST
                                        >=> route "/domainRoles"
                                        >=> bindJson (CommandEndpoints.domainRoles contextId)
                                        POST
                                        >=> route "/messages"
                                        >=> bindJson (CommandEndpoints.messages contextId)
                                        DELETE
                                        >=> CommandEndpoints.removeAndReturnId contextId ]))
                      GET >=> QueryEndpoints.getOrSearchBoundedContexts ])
