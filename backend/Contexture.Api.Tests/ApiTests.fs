module Contexture.Api.Tests.ApiTests

open System
open System.IO
open System.Net.Http
open System.Threading
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

module Prepare =
    let private registerGiven givens =
        fun (services: IServiceCollection) ->
            services.AddSingleton<EventStore>(EventStore.With givens)
            |> ignore

    let private toGiven clock events = events |> List.map (fun e -> e clock)

    let buildServerGiven given =
        given |> registerGiven |> TestHost.runServer

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

    let withGiven clock givenBuilders =
        let server =
            givenBuilders |> toGiven clock |> buildServerGiven

        new TestEnvironment(server)

type Given = EventEnvelope list

module Given =
    let noEvents = []
    let anEvent event = [ event ]
    let andOneEvent event given = given @ [ event ]
    let andEvents events given = given @ events

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

type Then = Assert

module Utils =
    let asEvent id event =
        fun clock ->
            { Event = event
              Metadata = { Source = id; RecordedAt = clock () } }
            |> EventEnvelope.box

    let singleEvent<'e> (eventStore: EventStore) : EventEnvelope<'e> =
        let events = eventStore.Get<'e>()
        Then.Single events

module Fixtures =
    let domainDefinition domainId : DomainCreated = { DomainId = domainId; Name = "" }

    let domainCreated definition =
        DomainCreated definition
        |> Utils.asEvent definition.DomainId

    let boundedContextDefinition domainId contextId =
        { BoundedContextId = contextId
          Name = ""
          DomainId = domainId }

    let boundedContextCreated definition =
        BoundedContextCreated definition
        |> Utils.asEvent definition.BoundedContextId

    let newLabel () =
        { LabelId = Guid.NewGuid()
          Name = "label"
          Value = Some "value"
          Template = None }


    let namespaceDefinition contextId namespaceId =
        { BoundedContextId = contextId
          Name = "namespace"
          NamespaceId = namespaceId
          NamespaceTemplateId = None
          Labels = [ newLabel () ] }

    let namespaceAdded definition =
        NamespaceAdded definition
        |> Utils.asEvent definition.BoundedContextId

    module RandomDomainAndBoundedContextAndNamespace =
        let given =
            let namespaceId = Guid.NewGuid()
            let contextId = Guid.NewGuid()
            let domainId = Guid.NewGuid()

            Given.noEvents
            |> Given.andOneEvent (
                { domainDefinition domainId with
                      Name = $"random-domain-name-%O{Guid.NewGuid()}" }
                |> domainCreated
            )
            |> Given.andOneEvent (
                { boundedContextDefinition domainId contextId with
                      Name = $"random-context-name-%O{Guid.NewGuid()}" }
                |> boundedContextCreated
            )
    //            |> Given.andOneEvent (
//                { BoundedContextId = namespaceId
//                  Name = "random-namespace-name-%" })

    module DomainWithBoundedContextAndNamespace =
        let namespaceId = Guid.NewGuid()
        let contextId = Guid.NewGuid()
        let domainId = Guid.NewGuid()

        let given () =
            Given.noEvents
            |> Given.andOneEvent (domainId |> domainDefinition |> domainCreated)
            |> Given.andOneEvent (
                contextId
                |> boundedContextDefinition domainId
                |> boundedContextCreated
            )
            |> Given.andOneEvent (
                namespaceId
                |> namespaceDefinition contextId
                |> namespaceAdded
            )


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
                |> Given.andOneEvent (
                    domainId
                    |> Fixtures.domainDefinition
                    |> Fixtures.domainCreated
                )
                |> Given.andOneEvent (
                    contextId
                    |> Fixtures.boundedContextDefinition domainId
                    |> Fixtures.boundedContextCreated
                )

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
                Then.Equal("test", n.Name)

                Then.Collection(
                    n.Labels,
                    (fun (l: LabelDefinition) ->
                        Then.Equal("l1", l.Name)
                        Then.Equal(Some "v1", l.Value)),
                    (fun (l: LabelDefinition) ->
                        Then.Equal("l2", l.Name)
                        Then.Equal(Some "v2", l.Value))
                )
            | e -> raise (XunitException $"Unexpected event: %O{e}")
        }

module BoundedContexts =

    module When =
        let searchingFor queryParameter (environment: Prepare.TestEnvironment) =
            task {
                let! result =
                    environment
                    |> When.gettingJson<{| Id: BoundedContextId |} array> $"api/boundedContexts?%s{queryParameter}"

                return result |> Array.map (fun i -> i.Id)
            }

    [<Fact>]
    let ``Can list all bounded contexts`` () =
        task {
            let clock = TestHost.staticClock DateTime.UtcNow

            // arrange
            let contextId = Guid.NewGuid()
            let domainId = Guid.NewGuid()

            let given =
                Given.noEvents
                |> Given.andOneEvent (
                    domainId
                    |> Fixtures.domainDefinition
                    |> Fixtures.domainCreated
                )
                |> Given.andOneEvent (
                    contextId
                    |> Fixtures.boundedContextDefinition domainId
                    |> Fixtures.boundedContextCreated
                )

            use testEnvironment = Prepare.withGiven clock given

            //act
            let! result =
                testEnvironment
                |> When.gettingJson<{| Id: BoundedContextId |} array> (sprintf "api/boundedContexts")

            // assert
            Then.NotEmpty result
            Then.Contains(contextId, result |> Array.map (fun i -> i.Id))
        }

    [<Fact>]
    let ``Can still list bounded contexts when attaching a random query string`` () =
        task {
            let clock = TestHost.staticClock DateTime.UtcNow

            // arrange
            let contextId = Guid.NewGuid()
            let domainId = Guid.NewGuid()

            let given =
                Given.noEvents
                |> Given.andOneEvent (
                    domainId
                    |> Fixtures.domainDefinition
                    |> Fixtures.domainCreated
                )
                |> Given.andOneEvent (
                    contextId
                    |> Fixtures.boundedContextDefinition domainId
                    |> Fixtures.boundedContextCreated
                )


            use testEnvironment = Prepare.withGiven clock given

            //act
            let! result =
                testEnvironment
                |> When.gettingJson<{| Id: BoundedContextId |} array> (sprintf "api/boundedContexts?bar.foo=baz")

            // assert
            Then.NotEmpty result
            Then.Contains(contextId, result |> Array.map (fun i -> i.Id))
        }

    [<Fact>]
    let ``Can still list bounded contexts when attaching a query string`` () =
        task {
            let clock = TestHost.staticClock DateTime.UtcNow

            // arrange
            let contextId = Guid.NewGuid()
            let domainId = Guid.NewGuid()

            let given =
                Given.noEvents
                |> Given.andOneEvent (
                    domainId
                    |> Fixtures.domainDefinition
                    |> Fixtures.domainCreated
                )
                |> Given.andOneEvent (
                    contextId
                    |> Fixtures.boundedContextDefinition domainId
                    |> Fixtures.boundedContextCreated
                )


            use testEnvironment = Prepare.withGiven clock given

            //act
            let! result =
                testEnvironment
                |> When.gettingJson<{| Id: BoundedContextId |} array> (
                    sprintf "api/boundedContexts?bar.foo=like|baz&foo=bar,baz,blub&bar=*test*"
                )

            // assert
            Then.NotEmpty result
            Then.Contains(contextId, result |> Array.map (fun i -> i.Id))
        }

    [<Fact>]
    let ``Can list bounded contexts by label and value`` () =
        task {
            let clock = TestHost.staticClock DateTime.UtcNow

            // arrange
            let namespaceId = Guid.NewGuid()
            let contextId = Guid.NewGuid()
            let domainId = Guid.NewGuid()

            let namespaceAdded =
                NamespaceAdded
                    { Fixtures.namespaceDefinition contextId namespaceId with
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
                |> Given.andOneEvent (
                    domainId
                    |> Fixtures.domainDefinition
                    |> Fixtures.domainCreated
                )
                |> Given.andOneEvent (
                    contextId
                    |> Fixtures.boundedContextDefinition domainId
                    |> Fixtures.boundedContextCreated
                )
                |> Given.andOneEvent namespaceAdded

            use testEnvironment = Prepare.withGiven clock given

            //act
            let! result =
                testEnvironment
                |> When.gettingJson<{| Id: BoundedContextId |} array> (sprintf "api/boundedContexts/%s/%s" "l1" "v1")

            // assert
            Then.NotEmpty result
            Then.Contains(contextId, result |> Array.map (fun i -> i.Id))
        }

    [<Theory>]
    [<InlineData("Label.name", "label")>]
    [<InlineData("Label.value", "value")>]
    let ``Can find the bounded context when searching with a single, exact parameter``
        (
            parameterName: string,
            parameterValue: string
        ) =
        task {
            let clock = TestHost.staticClock DateTime.UtcNow

            // arrange
            let namespaceId = Guid.NewGuid()
            let contextId = Guid.NewGuid()
            let domainId = Guid.NewGuid()

            let namespaceAdded =
                NamespaceAdded(Fixtures.namespaceDefinition contextId namespaceId)
                |> Utils.asEvent contextId

            let given =
                Given.noEvents
                |> Given.andOneEvent (
                    domainId
                    |> Fixtures.domainDefinition
                    |> Fixtures.domainCreated
                )
                |> Given.andOneEvent (
                    contextId
                    |> Fixtures.boundedContextDefinition domainId
                    |> Fixtures.boundedContextCreated
                )
                |> Given.andOneEvent namespaceAdded

            use testEnvironment = Prepare.withGiven clock given

            //act
            let! result =
                testEnvironment
                |> When.searchingFor $"%s{parameterName}=%s{parameterValue}"

            // assert
            Then.NotEmpty result
            Then.Collection(result, (fun x -> Then.Equal(contextId, x)))
        }



    module ``Search bounded contexts with a single string based parameter`` =
        open Fixtures

        let prepare given =
            let clock = TestHost.staticClock DateTime.UtcNow
            Prepare.withGiven clock given


        module When =
            let searchingTheLabelName query testEnvironment =
                testEnvironment
                |> When.searchingFor $"Label.Name=%s{query}"

        module Then =
            let itShouldContainOnlyTheBoundedContext result =
                Then.NotEmpty result
                Then.Collection(result, (fun x -> Then.Equal(DomainWithBoundedContextAndNamespace.contextId, x)))

        [<Fact>]
        let ``Is possible by using 'lab*' as StartsWith`` () =
            task {
                use testEnvironment =
                    prepare
                    <| DomainWithBoundedContextAndNamespace.given ()

                //act
                let! result =
                    testEnvironment
                    |> When.searchingTheLabelName "lab*"

                result
                |> Then.itShouldContainOnlyTheBoundedContext
            }

        [<Fact>]
        let ``Is possible by using '*bel' as EndsWith`` () =
            task {
                use testEnvironment =
                    prepare
                    <| DomainWithBoundedContextAndNamespace.given ()

                //act
                let! result =
                    testEnvironment
                    |> When.searchingTheLabelName "*bel"

                result
                |> Then.itShouldContainOnlyTheBoundedContext
            }

        [<Fact>]
        let ``Is possible by using '*abe*' as Contains`` () =
            task {
                use testEnvironment =
                    prepare
                    <| DomainWithBoundedContextAndNamespace.given ()

                //act
                let! result =
                    testEnvironment
                    |> When.searchingTheLabelName "*abe*"

                result
                |> Then.itShouldContainOnlyTheBoundedContext
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
            let name = "myname"

            let namespaceAdded =
                NamespaceAdded
                    { Fixtures.namespaceDefinition contextId namespaceId with
                          Name = name
                          NamespaceTemplateId = Some templateId }
                |> Utils.asEvent contextId

            let otherNamespaceAdded =
                NamespaceAdded
                    { Fixtures.namespaceDefinition otherContextId (Guid.NewGuid()) with
                          Name = "the other namespace"
                          NamespaceTemplateId = None }
                |> Utils.asEvent otherContextId

            let given =
                Given.noEvents
                |> Given.andOneEvent (
                    domainId
                    |> Fixtures.domainDefinition
                    |> Fixtures.domainCreated
                )
                |> Given.andOneEvent (
                    otherContextId
                    |> Fixtures.boundedContextDefinition domainId
                    |> Fixtures.boundedContextCreated
                )
                |> Given.andOneEvent (
                    contextId
                    |> Fixtures.boundedContextDefinition domainId
                    |> Fixtures.boundedContextCreated
                )
                |> Given.andOneEvent namespaceAdded
                |> Given.andOneEvent otherNamespaceAdded

            use testEnvironment = Prepare.withGiven clock given

            //act - search by name
            let! result =
                testEnvironment
                |> When.searchingFor $"Label.name=lab*&Namespace.Template=%O{templateId}"

            // assert
            Then.NotEmpty result
            Then.Collection(result, (fun x -> Then.Equal(contextId, x)))

            //act - search by value
            let! result =
                testEnvironment
                |> When.searchingFor $"Label.value=val*&Namespace.Template=%O{templateId}"

            // assert
            Then.NotEmpty result
            Then.Collection(result, (fun x -> Then.Equal(contextId, x)))

            // act - search by namespace name
            let! result =
                testEnvironment
                |> When.searchingFor $"Label.value=val*&Namespace.Name=%s{name}"

            // assert
            Then.NotEmpty result
            Then.Collection(result, (fun x -> Then.Equal(contextId, x)))
        }
