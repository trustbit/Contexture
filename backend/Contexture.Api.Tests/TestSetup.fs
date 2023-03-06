namespace Contexture.Api.Tests

open System
open System.IO
open System.Net
open System.Net.Http
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Tests.EnvironmentSimulation
open FsToolkit.ErrorHandling
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Logging

open Contexture.Api.Infrastructure
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

    let runServer environmentSimulation testConfiguration =
        let configureTest (services: IServiceCollection) =
            services
            |> useClockFromEnvironment environmentSimulation
            |> testConfiguration
            
        let host =
            createHost configureTest Contexture.Api.App.configureServices Contexture.Api.App.configureApp
        
        host.Services.GetServices<ReadModels.ReadModelInitialization>()
        |> Contexture.Api.App.connectAndReplayReadModels
        |> Async.bind (Subscriptions.waitUntilCaughtUp >> Async.AwaitTask)
        |> Async.RunSynchronously
        
        host.Start()
        host

    type TestHostEnvironment(server: IHost) =
        let client = lazy (server.GetTestClient())
        member __.Server = server
        member __.Client = client.Value

        member __.GetService<'Service>() =
            server.Services.GetRequiredService<'Service>()

        interface IDisposable with
            member __.Dispose() =
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
                EventStore.With (givens |> InMemoryStorage.initialize clock)
            )
            |> ignore

    let buildServerWithEvents events = events |> registerEvents

    let withGiven environment (givenEvents: EventDefinition list) =           
        let testConfiguration =
           buildServerWithEvents givenEvents
        let testHost =
            TestHost.runServer environment testConfiguration
            |> TestHost.asTestHost

        testHost

