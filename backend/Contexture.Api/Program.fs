module Contexture.Api.App

open System
open System.Threading
open System.Threading.Tasks

open Contexture.Api.FileBased.Database
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Infrastructure.Subscriptions
open FsToolkit.ErrorHandling
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
open System.Text.Json
open System.Text.Json.Serialization

module SystemRoutes =
    let errorHandler (ex : Exception) (logger : ILogger) =
        logger.LogError(ex, "An unhandled exception has occurred while executing the request.")
        clearResponse >=> setStatusCode 500 >=> text ex.Message

    let status : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let env = ctx.GetService<ContextureConfiguration>()
            let payload =
                {|
                    Health = true
                    GitHash = env.GitHash
                |}
            json payload next ctx
         
    // is there a better way to surface the subscriptions?
    let mutable subscriptions : Subscription list option = None
            
    let readiness: HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            match subscriptions with
            | Some subs ->
                let status = Runtime.calculateStatistics subs
                let payload =
                    {| CaughtUp =
                         status.CaughtUp
                         |> List.map (fun (position, name) -> {| Name = name; Position = position |})
                       Failed =
                         status.Failed
                         |> List.map (fun (e, position, name) -> {| Name = name; Position = position; Error = e.Message |})
                       Processing =
                         status.Processing
                         |> List.map (fun (position, name) -> {| Name = name; Position = position |})
                       NotRunning =
                         status.NotRunning
                         |> List.map (fun ( name) -> {| Name = name |})
                       Stopped =
                         status.Stopped
                         |> List.map (fun (position, name) -> {| Name = name; Position = position |})
                    |}
                // TODO: what if we are never able to catch up under (very) high load?
                // Are we then really not ready to process or is this an OKish situation?
                if Runtime.didAllSubscriptionsCatchup status.CaughtUp subs then
                    Successful.ok (json payload) next ctx
                else
                    ServerErrors.serviceUnavailable (json payload) next ctx    
            | None ->
                ServerErrors.serviceUnavailable (text "No subscriptions yet") next ctx            

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
             subRoute "/meta"
                ( choose [
                    route "/health" >=> GET >=> SystemRoutes.status
                    route "/readiness" >=> GET >=> SystemRoutes.readiness
                    GET >=> SystemRoutes.status
                ])
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
    let options =
        System.Text.Json.JsonSerializerOptions(
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            IgnoreNullValues = true,
            WriteIndented = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        )
        
    let fSharpOptions =
        JsonFSharpOptions.Default()
            .WithUnionUntagged()
            .WithUnionUnwrapRecordCases()
            .WithUnionUnwrapFieldlessTags()
            .WithUnionTagCaseInsensitive()
            
    fSharpOptions.AddToJsonSerializerOptions(options)
    
    options
    |> SystemTextJson.Serializer
    |> services.AddSingleton<Json.ISerializer>
    |> ignore
    
let registerReadModel<'R, 'E, 'S when 'R :> ReadModels.ReadModel<'E,'S> and 'R : not struct> (readModel: 'R) (services: IServiceCollection) =
    services.AddSingleton<'R>(readModel) |> ignore
    let initializeReadModel (s: IServiceProvider) =
        let rec formatAsString (runtimeType: Type) =
            if runtimeType.IsGenericType
               && (runtimeType.FullName.StartsWith("Microsoft") || runtimeType.FullName.StartsWith("System")) then
                let arguments = runtimeType.GetGenericArguments()
                let typeParameters =
                    arguments
                    |> Array.map formatAsString
                    |> String.concat ","
                $"{runtimeType.Name}<{typeParameters}>"
            else
                runtimeType.FullName
        ReadModels.ReadModelInitialization.initializeWith
            (s.GetRequiredService<EventStore>())
            $"ReadModel of {formatAsString(typeof<'E>)} for {formatAsString(typeof<'S>)}"
            readModel.EventHandler
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

let configureServices (context: HostBuilderContext) (services : IServiceCollection) =
    services
        .AddOptions<Options.ContextureOptions>()
        .Bind(context.Configuration)
        |> ignore
    
    services.AddSingleton<ContextureConfiguration>(fun p ->
        let options = p.GetRequiredService<IOptions<Options.ContextureOptions>>().Value
        Options.buildConfiguration options
    ) |> ignore 

    let configuration = context.Configuration.Get<Options.ContextureOptions>() |> Options.buildConfiguration
    
    match configuration.Engine with
    | FileBased path ->
        services
            .AddSingleton<SingleFileBasedDatastore>(fun services ->
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
            do! Runtime.waitUntilCaughtUp readModelSubscriptions

            let store = host.Services.GetRequiredService<EventStore>()
        
            let! document = database.Read()
            do! FileBased.Convert.importFromDocument store document
        
            let loggerFactory = host.Services.GetRequiredService<ILoggerFactory>()
            let subscriptionLogger = loggerFactory.CreateLogger("subscriptions")

            // subscriptions for syncing back to the filebased-db are added after initial seeding/loading
            let subscribeTo name subscriptionDefinition =
                store.Subscribe name End (subscriptionDefinition subscriptionLogger database)
            let! fileSyncSubscriptions  =
                Async.Parallel [
                    subscribeTo "FileBased.Convert.Collaboration.subscription" FileBased.Convert.Collaboration.subscription
                    subscribeTo "FileBased.Convert.Domain.subscription" FileBased.Convert.Domain.subscription
                    subscribeTo "FileBased.Convert.BoundedContext.subscription" FileBased.Convert.BoundedContext.subscription
                    subscribeTo "FileBased.Convert.Namespace.subscription" FileBased.Convert.Namespace.subscription
                    subscribeTo "FileBased.Convert.NamespaceTemplate.subscription" FileBased.Convert.NamespaceTemplate.subscription
                ]
                |> Async.map Array.toList
      
            SystemRoutes.subscriptions <- Some (readModelSubscriptions @ fileSyncSubscriptions)
            if host.Services.GetRequiredService<IWebHostEnvironment>().IsDevelopment() then
                do! Runtime.waitUntilCaughtUp (readModelSubscriptions @ fileSyncSubscriptions)
        | SqlServerBased _ ->
            // TODO: properly provision database table?
            let persistence = host.Services.GetRequiredService<NStore.Persistence.MsSql.MsSqlPersistence>()
            do! persistence.InitAsync(CancellationToken.None)

            let readModels = host.Services.GetServices<ReadModels.ReadModelInitialization>()

            let! readModelSubscriptions = connectAndReplayReadModels readModels
            SystemRoutes.subscriptions <- Some readModelSubscriptions
            if host.Services.GetRequiredService<IWebHostEnvironment>().IsDevelopment() then
                do! Runtime.waitUntilCaughtUp readModelSubscriptions

        return! host.RunAsync()
    }

[<EntryPoint>]
let main args =
    let host = buildHost args
    let executingHost = runAsync host
    executingHost.GetAwaiter().GetResult()
    0