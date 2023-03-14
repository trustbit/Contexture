namespace Contexture.Api.Tests

open System
open System.IO
open System.Net
open System.Net.Http
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Subscriptions
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Tests.EnvironmentSimulation
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Logging


open Microsoft.Extensions.Options

module TestHost =
    let configureLogging (builder: ILoggingBuilder) =
        builder.AddConsole().AddDebug() |> ignore

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

    let runReadModels (host: IHost) =
        host.Services.GetServices<ReadModels.ReadModelInitialization>()
        |> Contexture.Api.App.connectAndReplayReadModels
        |> Async.bind (Runtime.waitUntilCaughtUp >> Async.AwaitTask)
        
    let runReactions (host: IHost) =
        let loggerFactory = host.Services.GetRequiredService<ILoggerFactory>()
        [ Contexture.Api.Reactions.CascadeDelete.subscribe
            (loggerFactory.CreateLogger (nameof Contexture.Api.Reactions.CascadeDelete))
             (host.Services.GetRequiredService<EventStore>())
             (host.Services.GetRequiredService<PositionStorage.IStorePositions>())
        ]
        |> Async.Parallel
        |> Async.map Array.toList
        |> Async.bind(Runtime.waitUntilCaughtUp >> Async.AwaitTask)

    let runServer environmentSimulation testConfiguration =
        task {
            let configureTest (services: IServiceCollection) =
                services
                |> useClockFromEnvironment environmentSimulation
                |> testConfiguration
                
            let host =
                createHost configureTest Contexture.Api.App.configureServices Contexture.Api.App.configureApp
            
            do! runReadModels host
            do! runReactions host
            
            do! host.StartAsync()
            return host
        }
    type TestHostEnvironment(server: IHost) =
        let client = lazy (server.GetTestClient())
        member _.Server = server
        member _.Client = client.Value

        member _.GetService<'Service>() =
            server.Services.GetRequiredService<'Service>()

        interface IDisposable with
            member _.Dispose() =
                server.Dispose()

                if client.IsValueCreated then
                    client.Value.Dispose()

    let asTestHost server = new TestHostEnvironment(server)

module Prepare =

    open Contexture.Api.Infrastructure

    let private registerEvents givens =
        fun (services: IServiceCollection) ->
            services.AddSingleton<EventStore>(fun p ->
                let clock = p.GetRequiredService<Clock>()
                EventStore.With (givens |> InMemory.eventStoreWith clock)
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

