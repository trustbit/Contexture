namespace Contexture.Api.Tests

open System
open System.IO
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Logging
open FSharp.Control.Tasks

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
                    .Configure(Action<_> configure)
                    .UseTestServer()
                    .ConfigureTestServices(Action<_> configureTest)
                    .ConfigureLogging(configureLogging)
                |> ignore)
            .ConfigureLogging(configureLogging)
            .Build()

    let runServer environmentSimulation testConfiguration =
        let host =
            createHost testConfiguration Contexture.Api.App.configureServices Contexture.Api.App.configureApp

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
            services.AddSingleton<EventStore>(EventStore.With givens)
            |> ignore

    let private buildEvents environment events = events |> List.map (fun e -> e environment)

    let buildServerWithEvents events =
        events |> registerEvents

    let withGiven environment eventBuilders =
        let testHost =
            eventBuilders
            |> buildEvents environment
            |> buildServerWithEvents
            |> TestHost.runServer environment
            |> TestHost.asTestHost

        testHost
