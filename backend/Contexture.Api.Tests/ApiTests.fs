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
open Xunit
open FSharp.Control.Tasks
open Xunit.Sdk
open Contexture.Api.Tests.EnvironmentSimulation

type Given = EventEnvelope list

module Given =
    let noEvents = []
    let anEvent event = [ event ]
    let andOneEvent event given = given @ [ event ]
    let andEvents events given = given @ events

module When =
    open System.Net.Http.Json
    open TestHost

    let postingJson (url: string) (jsonContent: string) (environment: TestHostEnvironment) =
        task {
            let! result = environment.Client.PostAsync(url, new StringContent(jsonContent))
            return result.EnsureSuccessStatusCode()
        }

    let gettingJson<'t> (url: string) (environment: TestHostEnvironment) =
        task {
            let! result = environment.Client.GetFromJsonAsync<'t>(url)
            return result
        }

type Then = Assert

module Utils =
    let asEvent id event =
        fun (environment: ISimulateEnvironment) ->
            { Event = event
              Metadata =
                  { Source = id
                    RecordedAt = environment.Time() } }
            |> EventEnvelope.box

    let singleEvent<'e> (eventStore: EventStore) : EventEnvelope<'e> =
        let events = eventStore.Get<'e>()
        Then.Single events

module Fixtures =
    let domainDefinition domainId : DomainCreated =
        { DomainId = domainId; Name = "domain" }

    let domainCreated definition =
        DomainCreated definition
        |> Utils.asEvent definition.DomainId

    let boundedContextDefinition domainId contextId =
        { BoundedContextId = contextId
          Name = "bounded-context"
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
        let given environment =
            let namespaceId = environment |> Identifiers.guid
            let contextId = environment |> Identifiers.guid
            let domainId = environment |> Identifiers.guid

            Given.noEvents
            |> Given.andOneEvent (
                { domainDefinition domainId with
                      Name =
                          environment
                          |> Identifiers.nameWithGuid "random-domain-name" }
                |> domainCreated
            )
            |> Given.andOneEvent (
                { boundedContextDefinition domainId contextId with
                      Name =
                          environment
                          |> Identifiers.nameWithGuid "random-context-name" }
                |> boundedContextCreated
            )
            |> Given.andOneEvent (
                { BoundedContextId = namespaceId
                  Name =
                      environment
                      |> Identifiers.nameWithGuid "random-namespace-name"
                  NamespaceId = namespaceId
                  NamespaceTemplateId = None
                  Labels =
                      [ { LabelId = environment |> Identifiers.guid
                          Name =
                              environment
                              |> Identifiers.nameWithGuid "random-label-name"
                          Value =
                              environment
                              |> Identifiers.nameWithGuid "random-label-value"
                              |> Some
                          Template = None } ] }
                |> namespaceAdded
            )

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
            let clock = FixedTimeEnvironment.FromSystemClock()
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
        open TestHost

        let searchingFor queryParameter (environment: TestHostEnvironment) =
            task {
                let! result =
                    environment
                    |> When.gettingJson<{| Id: BoundedContextId |} array> $"api/boundedContexts?%s{queryParameter}"

                return result |> Array.map (fun i -> i.Id)
            }

    [<Fact>]
    let ``Can list all bounded contexts`` () =
        task {
            let clock = FixedTimeEnvironment.FromSystemClock()

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
            let clock = FixedTimeEnvironment.FromSystemClock()

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

    [<Theory>]
    [<InlineData("Label.name", "label")>]
    [<InlineData("Label.value", "value")>]
    [<InlineData("Namespace.name", "namespace")>]
    let ``Can find the bounded context when searching with a single, exact parameter``
        (
            parameterName: string,
            parameterValue: string
        ) =
        task {
            let clock = FixedTimeEnvironment.FromSystemClock()

            let searchedBoundedContext =
                Fixtures.DomainWithBoundedContextAndNamespace.given ()

            let randomBoundedContext =
                Fixtures.RandomDomainAndBoundedContextAndNamespace.given clock

            let given =
                searchedBoundedContext @ randomBoundedContext

            use testEnvironment = Prepare.withGiven clock given

            //act
            let! result =
                testEnvironment
                |> When.searchingFor $"%s{parameterName}=%s{parameterValue}"

            // assert
            Then.NotEmpty result
            Then.Collection(result, (fun x -> Then.Equal(Fixtures.DomainWithBoundedContextAndNamespace.contextId, x)))
        }

    module ``When searching bounded contexts with a single string based parameter`` =
        open Fixtures

        let prepareTestEnvironment searchedBoundedContext =
            let simulation = FixedTimeEnvironment.FromSystemClock()

            let randomBoundedContext =
                Fixtures.RandomDomainAndBoundedContextAndNamespace.given simulation

            let given =
                searchedBoundedContext @ randomBoundedContext

            Prepare.withGiven simulation given

        module When =
            let searchingTheLabelName query testEnvironment =
                testEnvironment
                |> When.searchingFor $"Label.Name=%s{query}"

            let searchingTheNamespaceName query testEnvironment =
                testEnvironment
                |> When.searchingFor $"Namespace.Name=%s{query}"

        module Then =
            let itShouldContainOnlyTheBoundedContext result =
                Then.NotEmpty result
                Then.Collection(result, (fun x -> Then.Equal(DomainWithBoundedContextAndNamespace.contextId, x)))

        [<Fact>]
        let ``it is possible to find label names by using 'lab*' as StartsWith`` () =
            task {
                use testEnvironment =
                    DomainWithBoundedContextAndNamespace.given ()
                    |> prepareTestEnvironment

                //act
                let! result =
                    testEnvironment
                    |> When.searchingTheLabelName "lab*"

                result
                |> Then.itShouldContainOnlyTheBoundedContext
            }

        [<Fact>]
        let ``it is possible to find label names by using '*bel' as EndsWith`` () =
            task {
                use testEnvironment =
                    DomainWithBoundedContextAndNamespace.given ()
                    |> prepareTestEnvironment

                //act
                let! result =
                    testEnvironment
                    |> When.searchingTheLabelName "*bel"

                result
                |> Then.itShouldContainOnlyTheBoundedContext
            }

        [<Fact>]
        let ``it is possible to find label names by using '*abe*' as Contains`` () =
            task {
                use testEnvironment =
                    DomainWithBoundedContextAndNamespace.given ()
                    |> prepareTestEnvironment

                //act
                let! result =
                    testEnvironment
                    |> When.searchingTheLabelName "*abe*"

                result
                |> Then.itShouldContainOnlyTheBoundedContext
            }

        [<Fact>]
        let ``it is possible to find namespace names by using '*amespac*' as Contains`` () =
            task {
                use testEnvironment =
                    DomainWithBoundedContextAndNamespace.given ()
                    |> prepareTestEnvironment

                let! result =
                    testEnvironment
                    |> When.searchingTheNamespaceName "*amespac*"

                result
                |> Then.itShouldContainOnlyTheBoundedContext
            }

    [<Fact>]
    let ``Can search for bounded contexts by label and value for a specific template`` () =
        task {
            let clock = FixedTimeEnvironment.FromSystemClock()

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
