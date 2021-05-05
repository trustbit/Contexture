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

    let runServer testConfiguration =
        let host =
            createHost testConfiguration Contexture.Api.App.configureServices Contexture.Api.App.configureApp

        host.Start()
        host

    let staticClock time = fun () -> time

    type TestEnvironment(server: IHost) =
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

    let asEnvironment server = new TestEnvironment(server)


module Prepare =

    open Contexture.Api.Infrastructure

    let private registerEvents givens =
        fun (services: IServiceCollection) ->
            services.AddSingleton<EventStore>(EventStore.With givens)
            |> ignore

    let private buildEvents clock events = events |> List.map (fun e -> e clock)

    let buildServerWithEvents events =
        events |> registerEvents |> TestHost.runServer

    let withGiven clock eventBuilders =
        let environment =
            eventBuilders
            |> buildEvents clock
            |> buildServerWithEvents
            |> TestHost.asEnvironment

        environment
