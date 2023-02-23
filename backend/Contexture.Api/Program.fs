module Contexture.Api.App

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Contexture.Api.Aggregates
open Contexture.Api.FileBased.Database
open Contexture.Api.Infrastructure
open Contexture.Api.FileBasedCommandHandlers
open Contexture.Api.Infrastructure.Storage
open Giraffe
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Options
open FSharp.Control.Tasks

[<CLIMutable>]
type ContextureOptions = 
    { ConnectionString: string
      DatabasePath: string 
      GitHash: string
    }

type ContextureConfiguration =
    { GitHash: string
      Engine : ContextureStorageEngine }
and ContextureStorageEngine =
    | FileBased of path: string
    | SqlServerBased of connectionString:string

module AllRoute =
    open ReadModels

    let getAllData =
        fun (next: HttpFunc) (ctx: HttpContext) -> task {
            let config = ctx.GetService<ContextureConfiguration>()
            let! result =
                match config.Engine with
                | FileBased _ -> task {
                    let database = ctx.GetService<SingleFileBasedDatastore>()
                    let! document = database.Read()
                    return document
                    }
                | SqlServerBased _ -> task {
                    let! domainState = ctx.GetService<ReadModels.Domain.AllDomainReadModel>().State()
                    let! boundedContextState = ctx.GetService<ReadModels.BoundedContext.AllBoundedContextsReadModel>().State()
                    let! namespaceState = ctx.GetService<ReadModels.Namespace.AllNamespacesReadModel>().State()
                    let! collaborationState = ctx.GetService<ReadModels.Collaboration.AllCollaborationsReadModel>().State()
                    let! namespaceTemplatesState = ctx.GetService<ReadModels.Templates.AllTemplatesReadModel>().State()
                    let domains = domainState |> Domain.allDomains
                    let boundedContexts = boundedContextState |> BoundedContext.allBoundedContexts
                    let namespacesOf = namespaceState |> Namespace.namespacesOf
                    let collaborations = collaborationState |> Collaboration.activeCollaborations
                    let templates = namespaceTemplatesState |> Templates.allTemplates
                    let document : Document =
                        { BoundedContexts =
                            collectionOfGuid (
                                boundedContexts
                                |> List.map (fun b ->
                                    {
                                        Serialization.BoundedContext.Id = b.Id
                                        Serialization.BoundedContext.DomainId = b.DomainId
                                        Serialization.BoundedContext.ShortName= b.ShortName
                                        Serialization.BoundedContext.Name = b.Name
                                        Serialization.BoundedContext.Description = b.Description
                                        Serialization.BoundedContext.Classification = b.Classification
                                        Serialization.BoundedContext.BusinessDecisions = b.BusinessDecisions
                                        Serialization.BoundedContext.Messages = b.Messages
                                        Serialization.BoundedContext.DomainRoles= b.DomainRoles
                                        Serialization.BoundedContext.UbiquitousLanguage= b.UbiquitousLanguage
                                        Serialization.BoundedContext.Namespaces= namespacesOf b.Id
                                    })
                                )
                                (fun b -> b.Id)
                            
                          Domains =
                              collectionOfGuid (
                                  domains
                                  |> List.map (fun d ->
                                      {
                                        Serialization.Domain.Id = d.Id
                                        Serialization.Domain.Name = d.Name
                                        Serialization.Domain.Vision = d.Vision
                                        Serialization.Domain.ShortName = d.ShortName
                                        Serialization.Domain.ParentDomainId = d.ParentDomainId
                                      })
                                  )
                                (fun d -> d.Id)
                          Collaborations =
                              collectionOfGuid (
                                  collaborations
                                  |> List.map (fun c ->
                                      {
                                          Serialization.Collaboration.Id = c.Id
                                          Serialization.Collaboration.Description = c.Description
                                          Serialization.Collaboration.Initiator = c.Initiator
                                          Serialization.Collaboration.Recipient = c.Recipient
                                          Serialization.Collaboration.RelationshipType = c.RelationshipType
                                      }))
                                  (fun c -> c.Id)
                          NamespaceTemplates = collectionOfGuid templates (fun t -> t.Id)
                        }
                    return document
                    }
                
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

                if notEmpty data.Domains
                   && notEmpty data.BoundedContexts
                   && notEmpty data.Collaborations then
                    logger.LogWarning(
                        "Replacing stored data with {Domains}, {BoundedContexts}, {Collaborations}, {NamespaceTemplates}",
                        data.Domains.Length,
                        data.BoundedContexts.Length,
                        data.Collaborations.Length,
                        data.NamespaceTemplates.Length
                    )

                    let newDocument : Document =
                        { Domains = collectionOfGuid data.Domains (fun d -> d.Id)
                          BoundedContexts = collectionOfGuid data.BoundedContexts (fun d -> d.Id)
                          Collaborations = collectionOfGuid data.Collaborations (fun d -> d.Id)
                          NamespaceTemplates = collectionOfGuid data.NamespaceTemplates (fun d -> d.Id) }
                    let! result =
                        match config.Engine with
                        | FileBased _ -> task {
                            let database = ctx.GetService<SingleFileBasedDatastore>()
                            let! oldDocument = database.Read()

                            let! result = database.Change (fun _ -> Ok newDocument)
                            return result |> Result.map (fun _ -> Some oldDocument)
                            }
                        | SqlServerBased _ -> task {
                            let store = ctx.GetService<EventStore>()
                            let persistence = ctx.GetService<NStore.Persistence.MsSql.MsSqlPersistence>()
                            do! persistence.DestroyAllAsync(ctx.RequestAborted)
                            do! persistence.InitAsync(ctx.RequestAborted)
                            do! FileBased.Convert.importFromDocument store newDocument
                            return Ok None
                        }
                            
                    match result with
                    | Ok oldDocument ->
                        let lifetime =
                            ctx.GetService<IHostApplicationLifetime>()

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

    let routes =
        route "/all"
        >=> choose [ GET >=> getAllData
                     PUT >=> putReplaceAllData ]


let status : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        let env = ctx.GetService<IOptions<ContextureOptions>>()
        match env.Value.GitHash with
        | hash when not (String.IsNullOrEmpty hash) ->
            json {| GitHash = hash |} next ctx
        | _ ->
            text "No status information" next ctx

let webApp hostFrontend =
    choose [
         subRoute "/api"
             (choose [
                   Apis.Domains.routes
                   Apis.BoundedContexts.routes
                   Apis.Collaborations.routes
                   Apis.Namespaces.routes
                   AllRoute.routes
            ])
         route "/meta" >=> GET >=> status
         hostFrontend
         RequestErrors.NOT_FOUND "Not found"
    ]

let frontendHostRoutes (env: IWebHostEnvironment) : HttpHandler =
    let detectRedirectLoop : HttpHandler =
        fun (next : HttpFunc) (ctx : HttpContext) ->
            let headers = HeaderDictionaryTypeExtensions.GetTypedHeaders(ctx.Request)
            match headers.Referer |> Option.ofObj with
            | Some referer when referer.AbsolutePath = ctx.Request.Path.ToUriComponent() && referer.Query = ctx.Request.QueryString.ToUriComponent() ->
                RequestErrors.NOT_FOUND "Not found and stuck in a redirect loop" next ctx
            | _ ->
                next ctx
    if env.IsDevelopment() then
        detectRedirectLoop >=>
            choose [
                GET >=> 
                    fun (next : HttpFunc) (ctx : HttpContext) -> 
                        let urlBuilder =
                            ctx.GetRequestUrl()
                            |> UriBuilder
                        urlBuilder.Port <- 8000
                        urlBuilder.Scheme <- "http"
                        redirectTo false (urlBuilder.ToString()) next ctx
            ]
     
    else
        detectRedirectLoop >=>
            choose [
                route "/" >=> htmlFile "wwwroot/index.html"
                GET >=> htmlFile "wwwroot/index.html"
            ]

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

let utcNowClock =
    fun () ->  System.DateTimeOffset.UtcNow
        
let configureCors (builder : CorsPolicyBuilder) =
    builder
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader()
        |> ignore

let configureApp (app : IApplicationBuilder) =    
    let env = app.ApplicationServices.GetService<IWebHostEnvironment>()
    (match env.IsDevelopment() with
    | true  ->
        app.UseDeveloperExceptionPage()
    | false ->
        app.UseGiraffeErrorHandler(errorHandler))
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(webApp (frontendHostRoutes env))
        
let configureJsonSerializer (services: IServiceCollection) =
    FileBased.Database.Serialization.serializerOptions
    |> SystemTextJson.Serializer
    |> services.AddSingleton<Json.ISerializer>
    |> ignore
    
let registerReadModel<'R, 'E, 'S when 'R :> ReadModels.ReadModel<'E,'S> and 'R : not struct> (readModel: 'R) (services: IServiceCollection) =
    services.AddSingleton<'R>(readModel) |> ignore
    let initializeReadModel (s: IServiceProvider) =
        ReadModels.ReadModelInitialization.initializeWith (s.GetRequiredService<EventStore>()) readModel.EventHandler
    services.AddSingleton<ReadModels.ReadModelInitialization> initializeReadModel
    
let configureReadModels (services: IServiceCollection) =
    services
    |> registerReadModel (ReadModels.Domain.domainsReadModel())
    |> registerReadModel (ReadModels.Collaboration.collaborationsReadModel())
    |> registerReadModel (ReadModels.Templates.templatesReadModel())
    |> registerReadModel (ReadModels.BoundedContext.boundedContextsReadModel())
    |> registerReadModel (ReadModels.Namespace.allNamespacesReadModel())
    |> registerReadModel (ReadModels.Find.BoundedContexts.readModel())
    |> registerReadModel (ReadModels.Find.Domains.readModel())
    |> registerReadModel (ReadModels.Find.Labels.readModel())
    |> registerReadModel (ReadModels.Find.Namespaces.readModel())
    |> ignore

let buildContextureConfiguration (options:ContextureOptions) =
    if not (String.IsNullOrEmpty options.ConnectionString) then
        { GitHash = options.GitHash
          Engine = SqlServerBased options.ConnectionString
        }
    else if not (String.IsNullOrEmpty options.DatabasePath) then
        { GitHash = options.GitHash
          Engine = FileBased options.DatabasePath
        }
    else
        failwith "Unable to initialize Contexture configuration"

let configureServices (context: HostBuilderContext) (services : IServiceCollection) =
    services
        .AddOptions<ContextureOptions>()
        .Bind(context.Configuration)
        .Validate((fun options -> not (String.IsNullOrEmpty options.DatabasePath) || not(String.IsNullOrEmpty options.ConnectionString)), "A non-empty Database configuration is required: Configure either a file based DatabasePath or a SQL based ConnectionString")
        |> ignore
    
    services.AddSingleton<ContextureConfiguration>(fun p ->
        let options = p.GetRequiredService<IOptions<ContextureOptions>>().Value
        buildContextureConfiguration options
    ) |> ignore 

    let configuration = context.Configuration.Get<ContextureOptions>() |> buildContextureConfiguration
    
    match configuration.Engine with
    | FileBased path ->
        services
            .AddSingleton<SingleFileBasedDatastore>(fun services ->
                let options = services.GetRequiredService<IOptions<ContextureOptions>>()
                // TODO: danger zone - loading should not be done as part of the initialization
                AgentBased.initializeDatabase(path)
                |> Async.AwaitTask
                |> Async.RunSynchronously
                )
            .AddSingleton<EventStore> (fun (p:IServiceProvider) ->
                let clock = p.GetRequiredService<Clock>()
                let storage = Storage.InMemoryStorage.empty clock
                
                EventStore.With storage
            )
        |> ignore
    | SqlServerBased connectionString ->
        services
            .AddSingleton<NStore.Core.Logging.INStoreLoggerFactory,NStoreBased.MicrosoftLoggingLoggerFactory>()
            .AddSingleton<NStore.Persistence.MsSql.MsSqlPersistence>(fun p ->
                let logger = p.GetRequiredService<NStore.Core.Logging.INStoreLoggerFactory>()
                let config =
                    NStore.Persistence.MsSql.MsSqlPersistenceOptions(
                        logger,
                        ConnectionString = connectionString,
                        Serializer = NStoreBased.JsonMsSqlSerializer.Default
                    )

                NStore.Persistence.MsSql.MsSqlPersistence(config)
                )
            .AddSingleton<EventStore> (fun (p:IServiceProvider) ->
                let clock = p.GetRequiredService<Clock>()
                let logger = NStoreBased.MicrosoftLoggingLoggerFactory(p.GetRequiredService<ILoggerFactory>())
                let persistence = p.GetRequiredService<NStore.Persistence.MsSql.MsSqlPersistence>()
                let storage = NStoreBased.Storage(persistence,clock, logger)
                EventStore.With storage
            )
            |> ignore
        
    
    services.AddSingleton<Clock>(utcNowClock) |> ignore
     
    services |> configureReadModels
    
    services.AddCors() |> ignore
    services.AddGiraffe() |> ignore
    services |> configureJsonSerializer

let configureLogging (builder : ILoggingBuilder) =
    builder.AddConsole()
           .AddDebug() |> ignore

let buildHost args =
    Host.CreateDefaultBuilder(args)
        .ConfigureServices(configureServices)
        .ConfigureWebHostDefaults(
            fun webHostBuilder ->
                webHostBuilder
                    .Configure(Action<IApplicationBuilder> configureApp)
                    .ConfigureLogging(configureLogging)
                    |> ignore)
        .Build()

let connectAndReplayReadModels (readModels: ReadModels.ReadModelInitialization seq) =
    readModels
    |> Seq.map (fun r -> r.ReplayAndConnect Start)
    |> Async.Parallel
    |> Async.map Array.toList
    
let waitUntilCaughtUp (subscriptions: Subscription List) =
    task {
        let getCurrentStatus () =
            subscriptions
            |> List.fold (fun (c,e) item ->
                match item.Status with
                | CaughtUp p ->  (p,item) :: c, e
                | Failed (ex,pos) -> c, (ex,pos,item) :: e
                | _ -> c,e
            ) (List.empty,List.empty)
            
        let selectPositions status =
            status |> fst |> List.map fst |> List.distinct 
        let initialStatus = getCurrentStatus()
        let mutable lastStatus = initialStatus
        let mutable counter = 0
        while not(lastStatus |> fst |> List.length = (subscriptions |> List.length ) && (lastStatus |> selectPositions |> List.length = 1)) do
            do! Task.Delay(100)
            let calculatedStatus = getCurrentStatus()
            lastStatus <- calculatedStatus
            counter <- counter + 1
            if counter > 100 then
                failwithf "No result after %i iterations. Last Status %A" counter lastStatus
    }
    
let runAsync (host: IHost) =
    task {
        let config = host.Services.GetRequiredService<ContextureConfiguration>()
        
        match config.Engine with
        | FileBased _ ->
            // make sure the database is loaded
            let database =
                host.Services.GetRequiredService<FileBased.Database.SingleFileBasedDatastore>()
                
            // connect and replay before we start import the document
            let readModels =
                host.Services.GetServices<ReadModels.ReadModelInitialization>()

            let! readModelSubscriptions = connectAndReplayReadModels readModels
            do! waitUntilCaughtUp readModelSubscriptions

            let store = host.Services.GetRequiredService<EventStore>()
        
            let! document = database.Read()
            do! FileBased.Convert.importFromDocument store document
        
            let loggerFactory = host.Services.GetRequiredService<ILoggerFactory>()
            let subscriptionLogger = loggerFactory.CreateLogger("subscriptions")

            // subscriptions for syncing back to the filebased-db are added after initial seeding/loading
            let! fileSyncSubscriptions  =
                Async.Parallel [
                    store.Subscribe End (FileBased.Convert.Collaboration.subscription subscriptionLogger database)
                    store.Subscribe End (FileBased.Convert.Domain.subscription subscriptionLogger database)
                    store.Subscribe End (FileBased.Convert.BoundedContext.subscription subscriptionLogger database)
                    store.Subscribe End (FileBased.Convert.Namespace.subscription subscriptionLogger database)
                    store.Subscribe End (FileBased.Convert.NamespaceTemplate.subscription subscriptionLogger database)
                ]
                |> Async.map Array.toList
                
            do! waitUntilCaughtUp (readModelSubscriptions @ fileSyncSubscriptions)
        | SqlServerBased _ ->
            // TODO: properly provision database table?
            let persistence = host.Services.GetRequiredService<NStore.Persistence.MsSql.MsSqlPersistence>()
            do! persistence.InitAsync(CancellationToken.None)

            let readModels = host.Services.GetServices<ReadModels.ReadModelInitialization>()

            let! readModelSubscriptions = connectAndReplayReadModels readModels
            
            do! waitUntilCaughtUp readModelSubscriptions

        return! host.RunAsync()
    }

[<EntryPoint>]
let main args =
    let host = buildHost args
    let executingHost = runAsync host
    executingHost.GetAwaiter().GetResult()
    0