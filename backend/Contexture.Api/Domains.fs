namespace Contexture.Api

open System

open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Projections
open Microsoft.AspNetCore.Http
open FSharp.Control.Tasks

open Giraffe

module Domains =
    open Contexture.Api.Aggregates.BoundedContext
    open Contexture.Api.Aggregates.Namespace
    open Aggregates.Domain
    open ValueObjects

    module Results =
        
        
        open Projections

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
              Namespaces: Namespace list }

        type DomainResult =
            { Id: DomainId
              ParentDomainId: DomainId option
              Key: string option
              Name: string
              Vision: string option
              Subdomains: DomainResult list
              BoundedContexts: BoundedContextResult list }


        let convertBoundedContext (findNamespaces: BoundedContextId -> Namespace list ) (boundedContext: BoundedContext) =
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
              Namespaces = boundedContext.Id |> findNamespaces }

        let convertDomain (domain: Domain) =
            { Id = domain.Id
              ParentDomainId = domain.ParentDomainId
              Key = domain.Key
              Name = domain.Name
              Vision = domain.Vision
              Subdomains = []
              BoundedContexts = [] }

        let includingSubdomainsAndBoundedContexts (boundedContexts: DomainId -> BoundedContext list)
                                                  (subDomains: Map<DomainId, Domain list>)
                                                  (findNamespaces: BoundedContextId -> Namespace list )
                                                  (domain: Domain)
                                                  =
            { (domain |> convertDomain) with
                  Subdomains =
                      subDomains
                      |> Map.tryFind domain.Id
                      |> Option.defaultValue []
                      |> List.map convertDomain
                  BoundedContexts =
                      domain.Id
                      |> boundedContexts
                      |> List.map (convertBoundedContext findNamespaces) }

    module CommandEndpoints =
        open FileBasedCommandHandlers
        open CommandHandler
        let clock = fun () -> DateTime.UtcNow

        let removeAndReturnId domainId =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()
                    let eventStoreBased = EventBased.eventStoreBasedCommandHandler clock database
                    match! Domain.useHandler eventStoreBased (RemoveDomain domainId) with
                    | Ok domainId -> return! json domainId next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let private updateAndReturnDomain command =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()
                    let eventStoreBased = EventBased.eventStoreBasedCommandHandler clock database
                    match! Domain.useHandler eventStoreBased command with
                    | Ok updatedDomain -> return! redirectTo false (sprintf "/api/domains/%O" updatedDomain) next ctx
                    | Error (DomainError EmptyName) ->
                        return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

        let createDomain (command: CreateDomain) =
            updateAndReturnDomain (CreateDomain(Guid.NewGuid(), command))

        let createSubDomain domainId (command: CreateDomain) =
            updateAndReturnDomain (CreateSubdomain(Guid.NewGuid(), domainId, command))

        let move domainId (command: MoveDomain) =
            updateAndReturnDomain (MoveDomain(domainId, command))

        let rename domainId (command: RenameDomain) =
            updateAndReturnDomain (RenameDomain(domainId, command))

        let refineVision domainId (command: RefineVision) =
            updateAndReturnDomain (RefineVision(domainId, command))

        let assignKey domainId (command: AssignKey) =
            updateAndReturnDomain (AssignKey(domainId, command))

        let newBoundedContextOn domainId (command: CreateBoundedContext) =
            fun (next: HttpFunc) (ctx: HttpContext) ->
                task {
                    let database = ctx.GetService<EventStore>()
                    let eventStoreBased = EventBased.eventStoreBasedCommandHandler clock database
                    match! BoundedContext.useHandler eventStoreBased (CreateBoundedContext(Guid.NewGuid(), domainId, command)) with
                    | Ok addedContext ->
                        return! redirectTo false (sprintf "/api/boundedcontexts/%O" addedContext) next ctx
                    | Error (DomainError Aggregates.BoundedContext.EmptyName) ->
                        return! RequestErrors.BAD_REQUEST "Name must not be empty" next ctx
                    | Error e -> return! ServerErrors.INTERNAL_ERROR e next ctx
                }

    module QueryEndpoints =
        open Contexture.Api.ReadModels
        open ReadModels

        let getDomains =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! domainState = ctx.GetService<ReadModels.Domain.AllDomainReadModel>().State()
                let! boundedContextState = ctx.GetService<ReadModels.BoundedContext.AllBoundedContextsReadModel>().State()
                let! namespaceState = ctx.GetService<ReadModels.Namespace.AllNamespacesReadModel>().State()
                let domains = domainState |> Domain.allDomains
                let subdomainsOf = Domain.subdomainsOf domainState
                let boundedContextsOf = BoundedContext.boundedContextsByDomain boundedContextState
                let namespacesOf = Namespace.namespacesOf namespaceState

                let result =
                    domains
                    |> List.map (Results.includingSubdomainsAndBoundedContexts boundedContextsOf subdomainsOf namespacesOf)

                return! json result next ctx
                }

        let getSubDomains domainId =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! domainState =  ctx.GetService<ReadModels.Domain.AllDomainReadModel>().State()
                let! boundedContextState = ctx.GetService<ReadModels.BoundedContext.AllBoundedContextsReadModel>().State()
                let! namespaceState = ctx.GetService<ReadModels.Namespace.AllNamespacesReadModel>().State()
                let domains = Domain.allDomains domainState
                let subdomainsOf = Domain.subdomainsOf domainState
                let boundedContextsOf = BoundedContext.boundedContextsByDomain boundedContextState
                let namespacesOf = Namespace.namespacesOf namespaceState

                let result =
                    domains
                    |> List.filter (fun d -> d.ParentDomainId = Some domainId)
                    |> List.map (Results.includingSubdomainsAndBoundedContexts boundedContextsOf subdomainsOf namespacesOf)

                return! json result next ctx
            }
         
        let getDomain domainId =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! domainState = ctx.GetService<ReadModels.Domain.AllDomainReadModel>().State()
                let! boundedContextState = ctx.GetService<ReadModels.BoundedContext.AllBoundedContextsReadModel>().State()
                let! namespaceState = ctx.GetService<ReadModels.Namespace.AllNamespacesReadModel>().State()
                let subdomainsOf = Domain.subdomainsOf domainState
                let boundedContextsOf = BoundedContext.boundedContextsByDomain boundedContextState
                let namespacesOf = Namespace.namespacesOf namespaceState

                let result =
                    domainId
                    |> Domain.domain domainState
                    |> Option.map (Results.includingSubdomainsAndBoundedContexts boundedContextsOf subdomainsOf namespacesOf)
                    |> Option.map json
                    |> Option.defaultValue (RequestErrors.NOT_FOUND(sprintf "Domain %O not found" domainId))

                return! result next ctx
            }

        let getBoundedContextsOf domainId =
            fun (next: HttpFunc) (ctx: HttpContext) -> task {
                let! boundedContextState = ctx.GetService<ReadModels.BoundedContext.AllBoundedContextsReadModel>().State()
                let boundedContextsOf = BoundedContext.boundedContextsByDomain boundedContextState
                let! namespaceState = ctx.GetService<ReadModels.Namespace.AllNamespacesReadModel>().State()
                let namespacesOf = Namespace.namespacesOf namespaceState

                let boundedContexts =
                    boundedContextsOf domainId
                    |> List.map (Results.convertBoundedContext namespacesOf)

                return! json boundedContexts next ctx
            }

    let routes: HttpHandler =
        subRoute
            "/domains"
            (choose [ subRoutef "/%O" (fun domainId ->
                          (choose [ GET
                                    >=> route "/domains"
                                    >=> QueryEndpoints.getSubDomains domainId
                                    POST
                                    >=> route "/domains"
                                    >=> bindJson (CommandEndpoints.createSubDomain domainId)
                                    GET
                                    >=> routeCi "/boundedContexts"
                                    >=> QueryEndpoints.getBoundedContextsOf domainId
                                    POST
                                    >=> routeCi "/boundedContexts"
                                    >=> bindJson (CommandEndpoints.newBoundedContextOn domainId)
                                    GET >=> QueryEndpoints.getDomain domainId
                                    POST
                                    >=> route "/rename"
                                    >=> bindJson (CommandEndpoints.rename domainId)
                                    POST
                                    >=> route "/move"
                                    >=> bindJson (CommandEndpoints.move domainId)
                                    POST
                                    >=> route "/vision"
                                    >=> bindJson (CommandEndpoints.refineVision domainId)
                                    POST
                                    >=> route "/key"
                                    >=> bindJson (CommandEndpoints.assignKey domainId)

                                    DELETE
                                    >=> CommandEndpoints.removeAndReturnId domainId

                                     ]))
                      GET >=> QueryEndpoints.getDomains
                      POST >=> bindJson CommandEndpoints.createDomain

                       ])
