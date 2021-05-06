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
    module Domain =
        [<Literal>]
        let Name = "domain"

        let definition domainId : DomainCreated =
            { DomainId = domainId; Name = "domain" }

        let domainCreated definition =
            DomainCreated definition
            |> Utils.asEvent definition.DomainId

    module BoundedContext =
        [<Literal>]
        let Name = "bounded-context"

        let definition domainId contextId =
            { BoundedContextId = contextId
              Name = Name
              DomainId = domainId }

        let boundedContextCreated definition =
            BoundedContextCreated definition
            |> Utils.asEvent definition.BoundedContextId

    module Label =

        [<Literal>]
        let Name = "architect"

        [<Literal>]
        let Value = "John Doe"

        let newLabel labelId =
            { LabelId = labelId
              Name = Name
              Value = Some Value
              Template = None }

    module Namespace =

        [<Literal>]
        let Name = "Team"

        let definition2 contextId namespaceId labelId =
            { BoundedContextId = contextId
              Name = Name
              NamespaceId = namespaceId
              NamespaceTemplateId = None
              Labels = [ Label.newLabel labelId ] }

        let definition contextId namespaceId =
            { BoundedContextId = contextId
              Name = Name
              NamespaceId = namespaceId
              NamespaceTemplateId = None
              Labels = [] }

        let appendLabel (label: LabelDefinition) (definition: NamespaceAdded) =
            { definition with
                  Labels = label :: definition.Labels }

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
                { Domain.definition domainId with
                      Name =
                          environment
                          |> Identifiers.nameWithGuid "random-domain-name" }
                |> Domain.domainCreated
            )
            |> Given.andOneEvent (
                { BoundedContext.definition domainId contextId with
                      Name =
                          environment
                          |> Identifiers.nameWithGuid "random-context-name" }
                |> BoundedContext.boundedContextCreated
            )
            |> Given.andOneEvent (
                { Namespace.definition contextId namespaceId with
                      Name =
                          environment
                          |> Identifiers.nameWithGuid "random-namespace-name"
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
                |> Namespace.namespaceAdded
            )

    module Builders =
        let givenADomainWithOneBoundedContext domainId contextId =
            Given.noEvents
            |> Given.andOneEvent (
                domainId
                |> Domain.definition
                |> Domain.domainCreated
            )
            |> Given.andOneEvent (
                contextId
                |> BoundedContext.definition domainId
                |> BoundedContext.boundedContextCreated
            )

        let givenADomainWithOneBoundedContextAndOneNamespace domainId contextId namespaceId =
            givenADomainWithOneBoundedContext domainId contextId
            |> Given.andOneEvent (
                namespaceId
                |> Namespace.definition contextId
                |> Namespace.appendLabel (Label.newLabel (Guid.NewGuid()))
                |> Namespace.namespaceAdded
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
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId

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
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId

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
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId

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
    [<InlineData("Label.name", "Architect")>]
    [<InlineData("Label.value", "John Doe")>]
    [<InlineData("Namespace.name", "Team")>]
    [<InlineData("Namespace.template", "A9F5D70E-B947-40B6-B7BE-4AC45CFE7F34")>]
    let ``Can find the bounded context when searching with a single, exact parameter``
        (
            parameterName: string,
            parameterValue: string
        ) =
        task {
            let simulation = FixedTimeEnvironment.FromSystemClock()

            let namespaceTemplateId =
                Guid("A9F5D70E-B947-40B6-B7BE-4AC45CFE7F34")

            let namespaceId = simulation |> Identifiers.guid
            let contextId = simulation |> Identifiers.guid
            let domainId = simulation |> Identifiers.guid

            let searchedBoundedContext =
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId
                |> Given.andOneEvent (
                    { Fixtures.Namespace.definition contextId namespaceId with
                          NamespaceTemplateId = Some namespaceTemplateId }
                    |> Fixtures.Namespace.appendLabel (Fixtures.Label.newLabel (Guid.NewGuid()))
                    |> Fixtures.Namespace.namespaceAdded
                )

            let randomBoundedContext =
                Fixtures.RandomDomainAndBoundedContextAndNamespace.given simulation

            let given =
                searchedBoundedContext @ randomBoundedContext

            use testEnvironment = Prepare.withGiven simulation given

            //act
            let! result =
                testEnvironment
                |> When.searchingFor $"%s{parameterName}=%s{parameterValue}"

            // assert
            Then.NotEmpty result
            Then.Collection(result, (fun x -> Then.Equal(contextId, x)))
        }

    module ``When searching for bounded contexts`` =
        open Fixtures

        module When =
            let searchingTheLabelName query testEnvironment =
                testEnvironment
                |> When.searchingFor $"Label.Name=%s{query}"

            let searchingTheNamespaceName query testEnvironment =
                testEnvironment
                |> When.searchingFor $"Namespace.Name=%s{query}"

        module Then =
            let itShouldContainOnlyTheBoundedContext (contextId: BoundedContextId) result =
                Then.NotEmpty result
                Then.Collection(result, (fun x -> Then.Equal(contextId, x)))



        let prepareTestEnvironment simulation searchedBoundedContext =
            let randomBoundedContext =
                Fixtures.RandomDomainAndBoundedContextAndNamespace.given simulation

            let given =
                searchedBoundedContext @ randomBoundedContext

            Prepare.withGiven simulation given

        type ``with a single string based parameter``() =
            let simulation = FixedTimeEnvironment.FromSystemClock()

            let namespaceId = simulation |> Identifiers.guid
            let contextId = simulation |> Identifiers.guid
            let domainId = simulation |> Identifiers.guid

            [<Fact>]
            member __.``it is possible to find label names by using 'arch*' as StartsWith``() =
                task {
                    use testEnvironment =
                        Builders.givenADomainWithOneBoundedContextAndOneNamespace domainId contextId namespaceId
                        |> prepareTestEnvironment simulation

                    //act
                    let! result =
                        testEnvironment
                        |> When.searchingTheLabelName "arch*"

                    result
                    |> Then.itShouldContainOnlyTheBoundedContext contextId
                }

            [<Fact>]
            member __.``it is possible to find label names by using '*tect' as EndsWith``() =
                task {
                    use testEnvironment =
                        Builders.givenADomainWithOneBoundedContextAndOneNamespace domainId contextId namespaceId
                        |> prepareTestEnvironment simulation

                    //act
                    let! result =
                        testEnvironment
                        |> When.searchingTheLabelName "*tect"

                    result
                    |> Then.itShouldContainOnlyTheBoundedContext contextId
                }

            [<Fact>]
            member __.``it is possible to find label names by using '*rchitec*' as Contains``() =
                task {
                    use testEnvironment =
                        Builders.givenADomainWithOneBoundedContextAndOneNamespace domainId contextId namespaceId
                        |> prepareTestEnvironment simulation

                    //act
                    let! result =
                        testEnvironment
                        |> When.searchingTheLabelName "*rchitec*"

                    result
                    |> Then.itShouldContainOnlyTheBoundedContext contextId
                }

            [<Fact>]
            member __.``it is possible to find namespace names by using '*ea*' as Contains``() =
                task {
                    use testEnvironment =
                        Builders.givenADomainWithOneBoundedContextAndOneNamespace domainId contextId namespaceId
                        |> prepareTestEnvironment simulation

                    let! result =
                        testEnvironment
                        |> When.searchingTheNamespaceName "*ea*"

                    result
                    |> Then.itShouldContainOnlyTheBoundedContext contextId
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
                { Fixtures.Namespace.definition contextId namespaceId with
                      Name = name
                      NamespaceTemplateId = Some templateId }
                |> Fixtures.Namespace.appendLabel (Fixtures.Label.newLabel (Guid.NewGuid()))
                |> Fixtures.Namespace.namespaceAdded

            let otherNamespaceAdded =

                { Fixtures.Namespace.definition otherContextId (Guid.NewGuid()) with
                      Name = "the other namespace"
                      NamespaceTemplateId = None }
                |> Fixtures.Namespace.appendLabel (Fixtures.Label.newLabel (Guid.NewGuid()))
                |> Fixtures.Namespace.namespaceAdded

            let given =
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId
                |> Given.andOneEvent (
                    otherContextId
                    |> Fixtures.BoundedContext.definition domainId
                    |> Fixtures.BoundedContext.boundedContextCreated
                )

                |> Given.andOneEvent namespaceAdded
                |> Given.andOneEvent otherNamespaceAdded

            use testEnvironment = Prepare.withGiven clock given

            //act - search by name
            let! result =
                testEnvironment
                |> When.searchingFor $"Label.name=arch*&Namespace.Template=%O{templateId}"

            // assert
            Then.NotEmpty result
            Then.Collection(result, (fun x -> Then.Equal(contextId, x)))

            //act - search by value
            let! result =
                testEnvironment
                |> When.searchingFor $"Label.value=Joh*&Namespace.Template=%O{templateId}"

            // assert
            Then.NotEmpty result
            Then.Collection(result, (fun x -> Then.Equal(contextId, x)))

            // act - search by namespace name
            let! result =
                testEnvironment
                |> When.searchingFor $"Label.value=Joh*&Namespace.Name=%s{name}"

            // assert
            Then.NotEmpty result
            Then.Collection(result, (fun x -> Then.Equal(contextId, x)))
        }
