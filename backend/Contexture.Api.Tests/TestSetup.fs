namespace Contexture.Api.Tests

open System
open System.IO
open System.Net
open System.Net.Http
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Subscriptions
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Reactions
open Contexture.Api.Tests.EnvironmentSimulation
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions

module TestHost =
    let configureLogging (builder: ILoggingBuilder) =
        builder
            .AddSimpleConsole(fun f -> f.IncludeScopes <- true)
            .AddDebug()
        |> ignore

    let createHost configureTest configureServices configure =
        Host
            .CreateDefaultBuilder()
            .UseContentRoot(Directory.GetCurrentDirectory())
            .UseEnvironment("Tests")
            .ConfigureServices(Action<_, _> configureServices)
            .ConfigureWebHostDefaults(fun (webHost: IWebHostBuilder) ->
                webHost
                    .UseSetting("FileBased:Path", "unit-tests.json")
                    .Configure(Action<_> configure)
                    .UseTestServer()
                    .ConfigureTestServices(Action<_> configureTest)
                    .ConfigureLogging(configureLogging)
                |> ignore)
            .ConfigureLogging(configureLogging)
            .Build()

    let useClockFromEnvironment (env: ISimulateEnvironment) (services: IServiceCollection) =
        services.AddSingleton<Contexture.Api.Infrastructure.Clock>(env.Time)

    let private waitUntilCaughtUp subscriptionsTask = async {
        let! subscriptions = subscriptionsTask
        let nullLogger = NullLogger.Instance
        let! _ = subscriptions |> (Runtime.waitUntilCaughtUp nullLogger >> Async.AwaitTask)
        return subscriptions
    }
    let runReadModels (host: IHost) = 
        host.Services.GetServices<ReadModels.ReadModelInitialization>()
        |> Contexture.Api.App.Startup.connectAndReplayReadModels
        |> waitUntilCaughtUp
        
    let runReactions (host: IHost) =
        host.Services.GetServices<ReactionInitialization>()
        |> Contexture.Api.App.Startup.connectAndReplayReactions
        |> waitUntilCaughtUp

    let runServer environmentSimulation testConfiguration =
        task {
            let configureTest (services: IServiceCollection) =
                services
                |> useClockFromEnvironment environmentSimulation
                |> testConfiguration
                
            let host =
                createHost configureTest Contexture.Api.App.ServiceConfiguration.configureServices Contexture.Api.App.ApplicationConfiguration.configureApp
            
            let! readModelSubscriptions = runReadModels host
            let! reactionSubscriptions = runReactions host
            
            do! host.StartAsync()
            return host, readModelSubscriptions @ reactionSubscriptions
        }
    type TestHostEnvironment(server: IHost, subscriptions: Subscription list) =
        let client = lazy (server.GetTestClient())
        member _.Server = server
        member _.Client = client.Value
        member _.Subscriptions = subscriptions

        member _.GetService<'Service>() =
            server.Services.GetRequiredService<'Service>()

        interface IDisposable with
            member _.Dispose() =
                server.Dispose()

                if client.IsValueCreated then
                    client.Value.Dispose()

    let asTestHost (server,subscriptions) = new TestHostEnvironment(server,subscriptions)

module Prepare =

    let private registerEvents givens =
        fun (services: IServiceCollection) ->
            services.AddSingleton<EventStore>(fun p ->
                let clock = p.GetRequiredService<Clock>()
                let factory = p.GetRequiredService<ILoggerFactory>()
                EventStore.With (givens |> InMemory.eventStoreWith factory clock)
            )
            |> ignore

    let buildServerWithEvents events = events |> registerEvents

    let withGiven environment (givenEvents: EventDefinition list) = task {       
        let testConfiguration =
           buildServerWithEvents givenEvents
        let! testHost =
            TestHost.runServer environment testConfiguration
            
        return TestHost.asTestHost testHost
        }

