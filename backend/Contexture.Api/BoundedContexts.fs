namespace Contexture.Api

open System
open Contexture.Api.Aggregates
open Contexture.Api.Database
open Contexture.Api.Domains
open Contexture.Api.Entities
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Projections
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
              Domain: Domain
              Namespaces: Namespace list }

        let convertBoundedContextWithDomain (findDomain: DomainId -> Domain option) (findNamespaces: BoundedContextId -> Namespace list ) (boundedContext: BoundedContext) =
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
              Domain = boundedContext.DomainId |> findDomain |> Option.get
              Namespaces = boundedContext.Id |> findNamespaces }

    module CommandEndpoints =
        open System
        open FileBasedCommandHandlers

        let clock = fun () -> DateTime.UtcNow

        let private updateAndReturnBoundedContext command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()

                    match BoundedContext.handle clock database command with
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

                    match BoundedContext.handle clock database (RemoveBoundedContext contextId) with
                    | Ok id -> return! json id next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

    module QueryEndpoints =
        open Contexture.Api.ReadModels

        let private mapToBoundedContext eventStore ids =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let namespacesOf =
                    Namespace.allNamespacesByContext eventStore

                let boundedContextLookup =
                    ReadModels.BoundedContext.boundedContextLookup eventStore

                let boundedContexts =
                    ids
                    |> List.choose (fun id -> boundedContextLookup |> Map.tryFind id)
                    |> List.map (Results.convertBoundedContextWithDomain (Domain.buildDomain eventStore) namespacesOf)

                json boundedContexts next ctx

        module Search =
            [<CLIMutable>]
            type LabelQuery =
                { Name: string option
                  Value: string option }
                member this.IsActive = this.Name.IsSome || this.Value.IsSome

            [<CLIMutable>]
            type NamespaceQuery =
                { Template: NamespaceTemplateId option
                  Name: string option }
                member this.IsActive = this.Name.IsSome || this.Template.IsSome

            [<CLIMutable>]
            type Query =
                { Label: LabelQuery option
                  Namespace: NamespaceQuery option }
                member this.IsActive =
                    this.Label
                    |> Option.map (fun l -> l.IsActive)
                    |> Option.defaultValue false
                    || this.Namespace
                       |> Option.map (fun n -> n.IsActive)
                       |> Option.defaultValue false


            let findRelevantNamespaces (database: EventStore) (item: NamespaceQuery) =
                let namespaces =
                    ReadModels.Namespace.FindNamespace.findNamespaces database

                let namespacesByName =
                    item.Name
                    |> Option.map (ReadModels.Namespace.FindNamespace.byNamespaceName namespaces)

                let namespacesByTemplate =
                    item.Template
                    |> Option.map (ReadModels.Namespace.FindNamespace.byNamespaceTemplate namespaces)

                let relevantNamespaces =
                    match namespacesByName, namespacesByTemplate with
                    | Some byName, Some byTemplate -> Set.intersect byName byTemplate
                    | Some byName, None -> byName
                    | None, Some byTemplate -> byTemplate
                    | None, None -> Set.empty

                relevantNamespaces

            let findRelevantLabels (database: EventStore) (item: LabelQuery) =
                let namespacesByLabel =
                    database
                    |> ReadModels.Namespace.FindNamespace.byLabel

                namespacesByLabel
                |> ReadModels.Namespace.FindNamespace.ByLabel.findByLabelName item.Name
                |> Set.filter
                    (fun { Value = value } ->
                        match item.Value with
                        | Some searchTerm ->
                            value
                            |> Option.exists (fun v -> v.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                        | None -> true)

            let getBoundedContextsByLabel (item: Query) =
                fun (next: HttpFunc) (ctx: HttpContext) ->
                    let database = ctx.GetService<EventStore>()

                    let relevantNamespaceIds =
                        item.Namespace
                        |> Option.map (findRelevantNamespaces database)
                        |> Option.map (Set.map (fun n -> n.NamespaceId))

                    let relevantLabels =
                        item.Label
                        |> Option.map (findRelevantLabels database)

                    let namespacesIds =
                        match relevantNamespaceIds, relevantLabels with
                        | Some namespaces, Some labels ->
                            labels
                            |> Set.filter (fun { NamespaceId = namespaceId } -> namespaces.Contains namespaceId)
                            |> Set.map (fun n -> n.NamespaceId)
                        | Some namespaces, None -> namespaces
                        | None, Some labels -> labels |> Set.map (fun n -> n.NamespaceId)
                        | None, None -> Set.empty

                    let boundedContextsByNamespace =
                        ReadModels.Namespace.FindBoundedContexts.byNamespace database

                    let boundedContextIds =
                        namespacesIds
                        |> Set.map (
                            boundedContextsByNamespace
                            >> Option.toList
                            >> Set.ofList
                        )
                        |> Set.unionMany
                        |> Set.toList

                    mapToBoundedContext database boundedContextIds next ctx

            let getBoundedContextsWithLabel (name, value) =
                fun (next: HttpFunc) (ctx: HttpContext) ->
                    let database = ctx.GetService<EventStore>()

                    let namespaces =
                        database
                        |> ReadModels.Namespace.FindNamespace.byLabel
                        |> ReadModels.Namespace.FindNamespace.ByLabel.getByLabelName name
                        |> Set.filter (fun { Value = v } -> v = Some value)
                        |> Set.map (fun n -> n.NamespaceId)

                    let boundedContextsByNamespace =
                        ReadModels.Namespace.FindBoundedContexts.byNamespace database

                    let boundedContextIds =
                        namespaces
                        |> Set.map (
                            boundedContextsByNamespace
                            >> Option.toList
                            >> Set.ofList
                        )
                        |> Set.unionMany
                        |> Set.toList

                    mapToBoundedContext database boundedContextIds next ctx

        let getBoundedContexts =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let eventStore = ctx.GetService<EventStore>()

                let allContexts =
                    eventStore
                    |> BoundedContext.boundedContextLookup
                    |> Map.toList
                    |> List.map fst

                mapToBoundedContext eventStore allContexts next ctx

        let getOrSearchBoundedContexts =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let nextHandler =
                    if ctx.Request.QueryString.HasValue then
                        match ctx.TryBindQueryString<Search.Query>() with
                        | Ok query when query.IsActive -> Search.getBoundedContextsByLabel query
                        | _ -> getBoundedContexts
                    else
                        getBoundedContexts

                nextHandler next ctx

        let getBoundedContext contextId =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                let eventStore = ctx.GetService<EventStore>()

                let namespacesOf =
                    Namespace.allNamespacesByContext eventStore

                let result =
                    contextId
                    |> BoundedContext.buildBoundedContext eventStore
                    |> Option.map (Results.convertBoundedContextWithDomain (Domain.buildDomain eventStore) namespacesOf)
                    |> Option.map json
                    |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "BoundedContext %O not found" contextId))

                result next ctx

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
                      GET
                      >=> routef "/%s/%s" QueryEndpoints.Search.getBoundedContextsWithLabel
                      GET >=> QueryEndpoints.getOrSearchBoundedContexts ])
