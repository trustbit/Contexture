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
        |> Async.bind (Contexture.Api.App.waitUntilCaughtUp >> Async.AwaitTask)
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


type Given = EventEnvelope list

module Given =
    let noEvents = []
    let anEvent event = [ event ]
    let andOneEvent event given = given @ [ event ]
    let andEvents events given = given @ events

module When =
    open System.Net.Http.Json
    open System.Net.Http

    open TestHost

    let deleting (url: string) (environment: TestHostEnvironment) =
        task {
            let! result = environment.Client.DeleteAsync(url)
            return result
        }
    
    let postingJson (url: string) (jsonContent: string) (environment: TestHostEnvironment) =
        task {
            let! result = environment.Client.PostAsync(url, new StringContent(jsonContent))
            return result.EnsureSuccessStatusCode()
        }

    let gettingJson<'t> (url: string) (environment: TestHostEnvironment) =
        task {        
            let! result = environment.Client.GetAsync(url)
            if result.IsSuccessStatusCode then
                let! content = result.Content.ReadFromJsonAsync<'t>()
                return content
            else
                let! content = result.Content.ReadAsStringAsync() 
                raise (Xunit.Sdk.XunitException($"Could not get from %O{url}: %O{result} %s{content}"))
                return Unchecked.defaultof<'t>
        }

type Then = Xunit.Assert
module Then =
    module Response =
        let shouldNotBeSuccessful (response: HttpResponseMessage) =
            Then.Equal(false, response.IsSuccessStatusCode)
        let shouldHaveStatusCode (statusCode: HttpStatusCode) (response: HttpResponseMessage) =
            Then.Equal(statusCode,response.StatusCode)

module Utils =
    open Contexture.Api.Tests.EnvironmentSimulation
    open Xunit

    let asEvent id event =
        EventDefinition.from id event

    let singleEvent<'e> (eventStore: EventStore) : Async<EventEnvelope<'e>> = async {
        let! _,events = eventStore.AllStreams<'e>()
        return Then.Single events
    }
