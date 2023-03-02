module Contexture.Api.AllRoutes


open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

open Contexture.Api.FileBased.Database
open Contexture.Api.Infrastructure
open Contexture.Api.Aggregates
open ReadModels

let private projectAllData (ctx: HttpContext) =
    task {
        let! domainState = ctx |> State.fetch State.fromReadModel<ReadModels.Domain.AllDomainReadModel>

        let! boundedContextState =
            ctx |> State.fetch State.fromReadModel<ReadModels.BoundedContext.AllBoundedContextsReadModel>

        let! namespaceState = ctx |> State.fetch State.fromReadModel<ReadModels.Namespace.AllNamespacesReadModel>

        let! collaborationState =
            ctx |> State.fetch State.fromReadModel<ReadModels.Collaboration.AllCollaborationsReadModel>

        let! namespaceTemplatesState =
            ctx |> State.fetch State.fromReadModel<ReadModels.Templates.AllTemplatesReadModel>

        let domains = domainState |> Domain.allDomains
        let boundedContexts = boundedContextState |> BoundedContext.allBoundedContexts
        let namespacesOf = namespaceState |> Namespace.namespacesOf
        let collaborations = collaborationState |> Collaboration.activeCollaborations
        let templates = namespaceTemplatesState |> Templates.allTemplates

        // for now we use the file format as our canonical import/export model
        let document: Document =
            { BoundedContexts =
                collectionOfGuid
                    (boundedContexts
                     |> List.map (fun b ->
                         { Serialization.BoundedContext.Id = b.Id
                           Serialization.BoundedContext.DomainId = b.DomainId
                           Serialization.BoundedContext.ShortName = b.ShortName
                           Serialization.BoundedContext.Name = b.Name
                           Serialization.BoundedContext.Description = b.Description
                           Serialization.BoundedContext.Classification = b.Classification
                           Serialization.BoundedContext.BusinessDecisions = b.BusinessDecisions
                           Serialization.BoundedContext.Messages = b.Messages
                           Serialization.BoundedContext.DomainRoles = b.DomainRoles
                           Serialization.BoundedContext.UbiquitousLanguage = b.UbiquitousLanguage
                           Serialization.BoundedContext.Namespaces = namespacesOf b.Id }))
                    (fun b -> b.Id)

              Domains =
                  collectionOfGuid
                      (domains
                       |> List.map (fun d ->
                           { Serialization.Domain.Id = d.Id
                             Serialization.Domain.Name = d.Name
                             Serialization.Domain.Vision = d.Vision
                             Serialization.Domain.ShortName = d.ShortName
                             Serialization.Domain.ParentDomainId = d.ParentDomainId }))
                      (fun d -> d.Id)
              Collaborations =
                collectionOfGuid
                    (collaborations
                     |> List.map (fun c ->
                         { Serialization.Collaboration.Id = c.Id
                           Serialization.Collaboration.Description = c.Description
                           Serialization.Collaboration.Initiator = c.Initiator
                           Serialization.Collaboration.Recipient = c.Recipient
                           Serialization.Collaboration.RelationshipType = c.RelationshipType }))
                    (fun c -> c.Id)
              NamespaceTemplates = collectionOfGuid templates (fun t -> t.Id) }

        return document
    }

let getAllData =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let config = ctx.GetService<ContextureConfiguration>()

            let! result =
                match config.Engine with
                | FileBased _ ->
                    task {
                        let database = ctx.GetService<SingleFileBasedDatastore>()
                        let! document = database.Read()
                        return document
                    }
                | SqlServerBased _ ->
                    projectAllData ctx

            let returnValue =
                {| BoundedContexts = result.BoundedContexts.All
                   Domains = result.Domains.All
                   Collaborations = result.Collaborations.All
                   NamespaceTemplates = result.NamespaceTemplates.All |}

            return! json returnValue next ctx
        }

[<CLIMutable>]
type UpdateAllData =
    { Domains: Serialization.Domain list
      BoundedContexts: Serialization.BoundedContext list
      Collaborations: Serialization.Collaboration list
      NamespaceTemplates: NamespaceTemplate.Projections.NamespaceTemplate list }

let putReplaceAllData =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            let config = ctx.GetService<ContextureConfiguration>()
            let logger = ctx.GetLogger()
            let notEmpty items = not (List.isEmpty items)
            let! data = ctx.BindJsonAsync<UpdateAllData>()

            let doNotReturnOldData =
                ctx.TryGetQueryStringValue("doNotReturnOldData")
                |> Option.map (fun value -> value.ToLowerInvariant() = "true")
                |> Option.defaultValue false

            if
                notEmpty data.Domains
                && notEmpty data.BoundedContexts
                && notEmpty data.Collaborations
            then
                logger.LogWarning(
                    "Replacing stored data with {Domains}, {BoundedContexts}, {Collaborations}, {NamespaceTemplates}",
                    data.Domains.Length,
                    data.BoundedContexts.Length,
                    data.Collaborations.Length,
                    data.NamespaceTemplates.Length
                )

                let newDocument: Document =
                    { Domains = collectionOfGuid data.Domains (fun d -> d.Id)
                      BoundedContexts = collectionOfGuid data.BoundedContexts (fun d -> d.Id)
                      Collaborations = collectionOfGuid data.Collaborations (fun d -> d.Id)
                      NamespaceTemplates = collectionOfGuid data.NamespaceTemplates (fun d -> d.Id) }

                let! result =
                    match config.Engine with
                    | FileBased _ ->
                        task {
                            let database = ctx.GetService<SingleFileBasedDatastore>()
                            let! oldDocument = database.Read()

                            let! result = database.Change(fun _ -> Ok newDocument)
                            return result |> Result.map (fun _ -> oldDocument)
                        }
                    | SqlServerBased _ ->
                        task {
                            let! document = projectAllData ctx
                            let store = ctx.GetService<EventStore>()
                            let persistence = ctx.GetService<NStore.Persistence.MsSql.MsSqlPersistence>()
                            do! persistence.DestroyAllAsync(ctx.RequestAborted)
                            do! persistence.InitAsync(ctx.RequestAborted)
                            do! FileBased.Convert.importFromDocument store newDocument
                            return Ok document
                        }

                match result with
                | Ok oldDocument ->
                    let lifetime = ctx.GetService<IHostApplicationLifetime>()

                    logger.LogInformation("Stopping Application after reseeding of data")
                    lifetime.StopApplication()

                    if doNotReturnOldData then
                        return!
                            text
                                "Successfully imported all data - NOTE: an application shutdown was initiated!"
                                next
                                ctx
                    else
                        return!
                            json
                                {| Message =
                                    "Successfully imported all data - NOTE: an application shutdown was initiated!"
                                   OldData = oldDocument |}
                                next
                                ctx
                | Error e -> return! ServerErrors.INTERNAL_ERROR $"Could not import document: %s{e}" next ctx
            else
                return! RequestErrors.BAD_REQUEST "Not overwriting with (partly) missing data" next ctx
        }

let routes: HttpHandler =
    route "/all" >=> choose [ GET >=> getAllData; PUT >=> putReplaceAllData ]
