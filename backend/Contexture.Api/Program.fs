module Contexture.Api.App

open System
open System.IO
open Contexture.Api.Aggregates
open Contexture.Api.Database
open Contexture.Api.Infrastructure
open Contexture.Api.FileBasedCommandHandlers
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
type ContextureOptions = { DatabasePath: string }

module AllRoute =

    let getAllData =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            let database = ctx.GetService<FileBased>()
            let document = database.Read

            let result =
                {| BoundedContexts = document.BoundedContexts.All
                   Domains = document.Domains.All
                   Collaborations = document.Collaborations.All
                   NamespaceTemplates = document.NamespaceTemplates.All |}

            json result next ctx

    open Entities

    [<CLIMutable>]
    type UpdateAllData =
        { Domains: Serialization.Domain list
          BoundedContexts: BoundedContext list
          Collaborations: Serialization.Collaboration list
          NamespaceTemplates: NamespaceTemplate.Projections.NamespaceTemplate list }

    let postAllData =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let database = ctx.GetService<FileBased>()
                let notEmpty items = not (List.isEmpty items)
                let! data = ctx.BindJsonAsync<UpdateAllData>()

                if notEmpty data.Domains
                   && notEmpty data.BoundedContexts
                   && notEmpty data.Collaborations then
                    let document =
                        database.Change
                            (fun document ->
                                let newDocument : Document =
                                    { Domains = collectionOfGuid data.Domains (fun d -> d.Id)
                                      BoundedContexts = collectionOfGuid data.BoundedContexts (fun d -> d.Id)
                                      Collaborations = collectionOfGuid data.Collaborations (fun d -> d.Id)
                                      NamespaceTemplates = collectionOfGuid data.NamespaceTemplates (fun d -> d.Id) }

                                Ok(newDocument, ()))

                    return! text "Successfully imported all data - NOTE: you need to restart the application" next ctx
                else
                    return! RequestErrors.BAD_REQUEST "Not overwriting with (partly) missing data" next ctx
            }

    let routes =
        route "/all"
        >=> choose [ GET >=> getAllData
                     POST >=> postAllData ]

let webApp hostFrontend =
    choose [
         subRoute "/api"
             (choose [
                   Domains.routes
                   BoundedContexts.routes
                   Collaborations.routes
                   Namespaces.routes
                   Search.apiRoutes
                   AllRoute.routes
                   RequestErrors.NOT_FOUND "Not found"
            ])
         Search.routes
         GET
         >=> routef "/boundedContext/%O/namespaces" Namespaces.index
         hostFrontend
         setStatusCode 404 >=> text "Not Found"
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
    Database.Serialization.serializerOptions
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
    |> ignore

let configureServices (context: HostBuilderContext) (services : IServiceCollection) =
    services
        .AddOptions<ContextureOptions>()
        .Bind(context.Configuration)
        .Validate((fun options -> not (String.IsNullOrEmpty options.DatabasePath)), "A non-empty DatabasePath configuration is required")
        |> ignore
    services.AddSingleton<FileBased>(fun services ->
        let options = services.GetRequiredService<IOptions<ContextureOptions>>()
        FileBased.InitializeDatabase(options.Value.DatabasePath))
        |> ignore
    
    services.AddSingleton<EventStore> (EventStore.Empty) |> ignore 
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
    |> Seq.map (fun r -> r.ReplayAndConnect())
    |> Async.Sequential
    |> Async.Ignore


let importFromDocument (store: EventStore) (database: Document) = async {
    let runAsync (items: Async<Unit> list) =
        items
        |> Async.Sequential
        |> Async.Ignore
        
    let clock = fun () -> System.DateTime.UtcNow
    do!
        database.Collaborations.All
        |> List.map (Collaboration.asEvents clock)
        |> List.map store.Append
        |> runAsync
    
    do!
        database.Domains.All
        |> List.map (Domain.asEvents clock)
        |> List.map store.Append
        |> runAsync
    
    do!
        database.BoundedContexts.All
        |> List.map (BoundedContext.asEvents clock)
        |> List.map store.Append
        |> runAsync
    
    do!
        database.BoundedContexts.All
        |> List.map (Namespace.asEvents clock)
        |> List.map store.Append
        |> runAsync

    do!
        database.NamespaceTemplates.All
        |> List.map (NamespaceTemplate.asEvents clock)
        |> List.map store.Append
        |> runAsync
    }

[<EntryPoint>]
let main args =
    let host = buildHost args

    // make sure the database is loaded
    let database = host.Services.GetRequiredService<FileBased>()
    let store = host.Services.GetRequiredService<EventStore>()
    
    // Do replays before we import the document
    let readModels = host.Services.GetServices<ReadModels.ReadModelInitialization>()
    Async.RunSynchronously(connectAndReplayReadModels readModels)
        
    Async.RunSynchronously (importFromDocument store database.Read)
    
    // collaboration subscription is added after initial seeding
    store.Subscribe (Collaboration.subscription database)
    store.Subscribe (Domain.subscription database)
    store.Subscribe (BoundedContext.subscription database)
    store.Subscribe (Namespace.subscription database)
    store.Subscribe (NamespaceTemplate.subscription database)
    
    
    host.Run()
    0