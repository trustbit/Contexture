namespace Contexture.Api

open System
open Contexture.Api.Aggregates
open Contexture.Api.Database
open Contexture.Api.Domains
open Contexture.Api.Entities
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Infrastructure
open Contexture.Api.ReadModels.Find
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module BoundedContexts =
    module Results =
        
        type BoundedContextResult =
            { Id: BoundedContextId
              ParentDomainId: DomainId
              Key: string option
              Name: string
              Description: string option
              Classification: StrategicClassification
              BusinessDecisions: BusinessDecision list
              UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
              Messages: Messages
              DomainRoles: DomainRole list
              Domain: Domain.Domain
              Namespaces: Namespace list }

        let convertBoundedContextWithDomain
            (findDomain: DomainId -> Domain.Domain option)
            (findNamespaces: BoundedContextId -> Namespace list)
            (boundedContext: BoundedContext)
            =
            { Id = boundedContext.Id
              ParentDomainId = boundedContext.DomainId
              Key = boundedContext.Key
              Name = boundedContext.Name
              Description = boundedContext.Description
              Classification = boundedContext.Classification
              BusinessDecisions = boundedContext.BusinessDecisions
              UbiquitousLanguage = boundedContext.UbiquitousLanguage
              Messages = boundedContext.Messages
              DomainRoles = boundedContext.DomainRoles
              Domain =
                  boundedContext.DomainId
                  |> findDomain
                  |> Option.get
              Namespaces = boundedContext.Id |> findNamespaces }

    module CommandEndpoints =
        open System
        open FileBasedCommandHandlers

        let clock = fun () -> DateTime.UtcNow

        let private updateAndReturnBoundedContext command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()

                    match! BoundedContext.handle clock database command with
                    | Ok updatedContext ->
                        return! redirectTo false (sprintf "/api/boundedcontexts/%O" updatedContext) next ctx
                    | Error (DomainError EmptyName) ->
                        return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let rename contextId (command: RenameBoundedContext) =
            updateAndReturnBoundedContext (RenameBoundedContext(contextId, command))

        let key contextId (command: AssignKey) =
            updateAndReturnBoundedContext (AssignKey(contextId, command))

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

                    match! BoundedContext.handle clock database (RemoveBoundedContext contextId) with
                    | Ok id -> return! json id next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

    module QueryEndpoints =
        open Contexture.Api.ReadModels

        let private mapToBoundedContext eventStore domainState ids =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let namespacesOf =
                    Namespace.allNamespacesByContext eventStore

                let boundedContextLookup =
                    ReadModels.BoundedContext.boundedContextLookup eventStore

                let boundedContexts =
                    ids
                    |> List.choose (fun id -> boundedContextLookup |> Map.tryFind id)
                    |> List.map (Results.convertBoundedContextWithDomain (Domain.domain domainState) namespacesOf)

                json boundedContexts next ctx

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

        let getBoundedContextsByQuery (item: Query) =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let database = ctx.GetService<EventStore>()
                let! domainState = ctx.GetService<ReadModels.Domain.AllDomainReadModel>().State()

                let namespaceIds =
                    SearchFor.NamespaceId.find database item.Namespace

                let domainIds =
                    SearchFor.DomainId.find database item.Domain

                let boundedContextIdsFromLabels =
                    SearchFor.Labels.find database item.Label

                let boundedContextIdsFromSearch =
                    SearchFor.BoundedContextId.find database item.BoundedContext

                let boundedContextsByNamespace =
                    Namespace.BoundedContexts.byNamespace database

                let boundedContextsByDomain =
                    database
                    |> ReadModels.BoundedContext.allBoundedContextsByDomain

                let boundedContextIdsFromNamespace =
                    namespaceIds
                    |> SearchResult.bind (
                        Set.map (
                            boundedContextsByNamespace
                            >> Option.toList
                            >> Set.ofList
                        )
                       >> SearchResult.takeAllResults
                    )

                let boundedContextIdsFromDomain =
                    domainIds
                    |> SearchResult.bind (
                        Set.map (
                            boundedContextsByDomain
                            >> List.map (fun b -> b.Id)
                            >> Set.ofList
                        )
                        >> SearchResult.takeAllResults
                    )

                let boundedContextIds =
                    SearchResult.combineResultsWithAnd
                        [ boundedContextIdsFromSearch
                          boundedContextIdsFromNamespace
                          boundedContextIdsFromDomain
                          boundedContextIdsFromLabels ]
                        
                let idsToLoad =
                    boundedContextIds
                    |> SearchResult.value
                    |> Option.map Set.toList
                    |> Option.defaultValue List.empty

                return! mapToBoundedContext database domainState idsToLoad next ctx
            }

        let getBoundedContexts =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let eventStore = ctx.GetService<EventStore>()
                let! domainState = ctx.GetService<ReadModels.Domain.AllDomainReadModel>().State()

                let allContexts =
                    eventStore
                    |> BoundedContext.boundedContextLookup
                    |> Map.toList
                    |> List.map fst

                return! mapToBoundedContext eventStore domainState allContexts next ctx
            }

        let getOrSearchBoundedContexts =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let nextHandler =
                    if ctx.Request.QueryString.HasValue then
                        match ctx.TryBindQueryString<Query>() with
                        | Ok query when query.IsActive -> getBoundedContextsByQuery query
                        | _ -> getBoundedContexts
                    else
                        getBoundedContexts

                return! nextHandler next ctx
            }

        let getBoundedContext contextId =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let eventStore = ctx.GetService<EventStore>()
                let! domainState = ctx.GetService<ReadModels.Domain.AllDomainReadModel>().State()

                let namespacesOf =
                    Namespace.allNamespacesByContext eventStore

                let result =
                    contextId
                    |> BoundedContext.buildBoundedContext eventStore
                    |> Option.map (Results.convertBoundedContextWithDomain (Domain.domain domainState) namespacesOf)
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
                                        >=> route "/key"
                                        >=> bindJson (CommandEndpoints.key contextId)
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
