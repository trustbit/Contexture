module Contexture.Api.Tests.ApiTests

open System
open System.IO
open System.Net.Http
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Aggregates.Domain
open Contexture.Api.Aggregates.Namespace
open Contexture.Api.Entities
open Contexture.Api.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Logging
open Xunit
open FSharp.Control.Tasks
open Xunit.Sdk

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


type Given = EventEnvelope list

module Given =

    let noEvents = []
    let anEvent event = [ event ]
    let andOneEvent event given = given @ [ event ]
    let andEvents events given = given @ events

module Prepare =
    let private registerGiven givens =
        fun (services: IServiceCollection) ->
            services.AddSingleton<EventStore>(EventStore.With givens)
            |> ignore

    let private toGiven clock events = events |> List.map (fun e -> e clock)

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

    let buildServerGiven given =
        given |> registerGiven |> TestHost.runServer

    let withGiven clock givenBuilders =
        let server =
            givenBuilders |> toGiven clock |> buildServerGiven

        new TestEnvironment(server)

module When =
    open System.Net.Http.Json
    open Prepare

    let postingJson (url: string) (jsonContent: string) (environment: TestEnvironment) =
        task {
            let! result = environment.Client.PostAsync(url, new StringContent(jsonContent))
            return result.EnsureSuccessStatusCode()
        }

    let gettingJson<'t> (url: string) (environment: TestEnvironment) =
        task {
            let! result = environment.Client.GetFromJsonAsync<'t>(url)
            return result
        }

module Utils =
    let asEvent id event =
        fun clock ->
            { Event = event
              Metadata = { Source = id; RecordedAt = clock () } }
            |> EventEnvelope.box

    let singleEvent<'e> (eventStore: EventStore) : EventEnvelope<'e> =
        let events = eventStore.Get<'e>()
        Assert.Single events

module Fixtures =
    let newDomain domainId =
        DomainCreated { DomainId = domainId; Name = "" }
        |> Utils.asEvent domainId

    let newBoundedContext domainId contextId =
        BoundedContextCreated
            { BoundedContextId = contextId
              Name = ""
              DomainId = domainId }
        |> Utils.asEvent contextId


    let newLabel () =
        { LabelId = Guid.NewGuid()
          Name = "label"
          Value = Some "value"
          Template = None }

module Namespaces =

    [<Fact>]
    let ``Can create a new namespace`` () =
        task {
            // arrange
            let clock = TestHost.staticClock DateTime.UtcNow
            let domainId = Guid.NewGuid()
            let contextId = Guid.NewGuid()

            let given =
                Given.noEvents
                |> Given.andOneEvent (Fixtures.newDomain domainId)
                |> Given.andOneEvent (Fixtures.newBoundedContext domainId contextId)

            use testEnvironment = Prepare.withGiven clock given

            //act
            let createNamespaceContent = "{
                    \"name\":  \"test\",
                    \"labels\": [
                        { \"name\": \"l1\", \"value\": \"v1\" },
                        { \"name\": \"l2\", \"value\": \"v2\" }
                    ]
                }"

            let! _ =
                testEnvironment
                |> When.postingJson (sprintf "api/boundedContexts/%O/namespaces" contextId) createNamespaceContent

            // assert
            let eventStore = testEnvironment.GetService<EventStore>()

            let event =
                Utils.singleEvent<Namespace.Event> eventStore

            match event.Event with
            | NamespaceAdded n ->
                Assert.Equal("test", n.Name)

                Assert.Collection(
                    n.Labels,
                    (fun (l: LabelDefinition) ->
                        Assert.Equal("l1", l.Name)
                        Assert.Equal(Some "v1", l.Value)),
                    (fun (l: LabelDefinition) ->
                        Assert.Equal("l2", l.Name)
                        Assert.Equal(Some "v2", l.Value))
                )
            | e -> raise (XunitException $"Unexpected event: %O{e}")
        }

module BoundedContextSearch =

    module When =
        let searchingFor queryParameter (environment: Prepare.TestEnvironment) =
            task {
                let! result =
                    environment
                    |> When.gettingJson<{| Id: BoundedContextId |} array> $"api/boundedContexts?%s{queryParameter}"

                return result |> Array.map (fun i -> i.Id)
            }

    [<Fact>]
    let ``Can list bounded contexts by label and value`` () =
        task {
            let clock = TestHost.staticClock DateTime.UtcNow

            // arrange
            let namespaceId = Guid.NewGuid()
            let contextId = Guid.NewGuid()
            let domainId = Guid.NewGuid()

            let added =
                NamespaceAdded
                    { BoundedContextId = contextId
                      Name = "namespace"
                      NamespaceId = namespaceId
                      NamespaceTemplateId = None
                      Labels =
                          [ { Fixtures.newLabel () with
                                  Name = "l1"
                                  Value = Some "v1" }
                            { Fixtures.newLabel () with
                                  Name = "l2"
                                  Value = Some "v2" } ] }
                |> Utils.asEvent contextId

            let given =
                Given.noEvents
                |> Given.andOneEvent (Fixtures.newDomain domainId)
                |> Given.andOneEvent (Fixtures.newBoundedContext domainId contextId)
                |> Given.andOneEvent added

            use testEnvironment = Prepare.withGiven clock given

            //act
            let! result =
                testEnvironment
                |> When.gettingJson<{| Id: BoundedContextId |} array> (sprintf "api/boundedContexts/%s/%s" "l1" "v1")

            // assert
            Assert.NotEmpty result
            Assert.Contains(contextId, result |> Array.map (fun i -> i.Id))
        }

    [<Fact>]
    let ``Can search for bounded contexts by label and value`` () =
        task {
            let clock = TestHost.staticClock DateTime.UtcNow

            // arrange
            let namespaceId = Guid.NewGuid()
            let contextId = Guid.NewGuid()
            let domainId = Guid.NewGuid()

            let namespaceAdded =
                NamespaceAdded
                    { BoundedContextId = contextId
                      Name = "namespace"
                      NamespaceId = namespaceId
                      NamespaceTemplateId = None
                      Labels = [ Fixtures.newLabel () ] }
                |> Utils.asEvent contextId

            let given =
                Given.noEvents
                |> Given.andOneEvent (Fixtures.newDomain domainId)
                |> Given.andOneEvent (Fixtures.newBoundedContext domainId contextId)
                |> Given.andOneEvent namespaceAdded

            use testEnvironment = Prepare.withGiven clock given

            //act - search by name
            let! result = testEnvironment |> When.searchingFor $"name=lab"

            // assert
            Assert.NotEmpty result
            Assert.Collection(result, (fun x -> Assert.Equal(contextId, x)))

            //act - search by value
            let! result = testEnvironment |> When.searchingFor "value=val"

            // assert
            Assert.NotEmpty result
            Assert.Collection(result, (fun x -> Assert.Equal(contextId, x)))
        }

    [<Fact>]
    let ``Can search for bounded contexts by label and value for a specific template`` () =
        task {
            let clock = TestHost.staticClock DateTime.UtcNow

            // arrange
            let namespaceId = Guid.NewGuid()
            let contextId = Guid.NewGuid()
            let otherContextId = Guid.NewGuid()
            let templateId = Guid.NewGuid()
            let domainId = Guid.NewGuid()

            let added =
                NamespaceAdded
                    { BoundedContextId = contextId
                      Name = "namespace"
                      NamespaceId = namespaceId
                      NamespaceTemplateId = Some templateId
                      Labels = [ Fixtures.newLabel () ] }
                |> Utils.asEvent contextId

            let addedOther =
                NamespaceAdded
                    { BoundedContextId = otherContextId
                      Name = "the other namespace"
                      NamespaceId = Guid.NewGuid()
                      NamespaceTemplateId = None
                      Labels = [ Fixtures.newLabel () ] }
                |> Utils.asEvent otherContextId

            let given =
                Given.noEvents
                |> Given.andOneEvent (Fixtures.newDomain domainId)
                |> Given.andOneEvent (Fixtures.newBoundedContext domainId contextId)
                |> Given.andOneEvent added
                |> Given.andOneEvent addedOther

            use testEnvironment = Prepare.withGiven clock given

            //act - search by name
            let! result =
                testEnvironment
                |> When.searchingFor $"name=lab&NamespaceTemplate=%O{templateId}"

            // assert
            Assert.NotEmpty result
            Assert.Collection(result, (fun x -> Assert.Equal(contextId, x)))

            //act - search by value
            let! result =
                testEnvironment
                |> When.searchingFor $"value=val&NamespaceTemplate=%O{templateId}"

            // assert
            Assert.NotEmpty result
            Assert.Collection(result, (fun x -> Assert.Equal(contextId, x)))
        }
