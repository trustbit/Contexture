module Contexture.Api.App

open System
open System.Threading
open System.Threading.Tasks

open Contexture.Api.FileBased.Database
open Contexture.Api.Infrastructure
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

[<CLIMutable>]
type ContextureOptions = 
    { ConnectionString: string
      DatabasePath: string 
      GitHash: string
    }

module SystemRoutes =
    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

    let status : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let env = ctx.GetService<ContextureConfiguration>()
            match env.GitHash with
            | hash when not (String.IsNullOrEmpty hash) ->
                json {| GitHash = hash |} next ctx
            | _ ->
                text "No status information" next ctx

module Routes =
    let webApp hostFrontend =
        choose [
             subRoute "/api"
                 (choose [
                       Apis.Domains.routes
                       Apis.BoundedContexts.routes
                       Apis.Collaborations.routes
                       Apis.Namespaces.routes
                       AllRoutes.routes
                ])
             route "/meta" >=> GET >=> SystemRoutes.status
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
        app.UseGiraffeErrorHandler(SystemRoutes.errorHandler))
        .UseCors(configureCors)
        .UseStaticFiles()
        .UseGiraffe(Routes.webApp (Routes.frontendHostRoutes env))
        
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