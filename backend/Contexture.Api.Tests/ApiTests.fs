module Contexture.Api.Tests.ApiTests

open System

open System.Net
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

module Namespaces =

    [<Fact>]
    let ``Can create a new namespace`` () =
        task {
            // arrange
            let environment = FixedTimeEnvironment.FromSystemClock()
            let domainId = environment |> PseudoRandom.guid
            let contextId = environment |> PseudoRandom.guid

            let given =
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId

            use testEnvironment = Prepare.withGiven environment given

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

            let! event =
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

        module Searching =
            let forALabelNamed name = $"Label.Name=%s{name}"

    [<Fact>]
    let ``Can list all bounded contexts`` () =
        task {
            let environment = FixedTimeEnvironment.FromSystemClock()

            // arrange
            let contextId = environment |> PseudoRandom.guid
            let domainId = environment |> PseudoRandom.guid

            let given =
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId

            use testEnvironment = Prepare.withGiven environment given

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
            let environment = FixedTimeEnvironment.FromSystemClock()

            // arrange
            let contextId = environment |> PseudoRandom.guid
            let domainId = environment |> PseudoRandom.guid

            let given =
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId

            use testEnvironment = Prepare.withGiven environment given

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
    [<InlineData("Domain.name", "domain")>]
    [<InlineData("Domain.key", "DO-1")>]
    [<InlineData("BoundedContext.name", "bounded-context")>]
    [<InlineData("BoundedContext.key", "BC-1")>]
    let ``Can find the bounded context when searching with a single, exact parameter``
        (
            parameterName: string,
            parameterValue: string
        ) =
        task {
            let simulation = FixedTimeEnvironment.FromSystemClock()

            let namespaceTemplateId =
                Guid("A9F5D70E-B947-40B6-B7BE-4AC45CFE7F34")

            let namespaceId = simulation |> PseudoRandom.guid
            let contextId = simulation |> PseudoRandom.guid
            let domainId = simulation |> PseudoRandom.guid

            let searchedBoundedContext =
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId
                |> Given.andOneEvent (
                    { Fixtures.Namespace.definition contextId namespaceId with
                          NamespaceTemplateId = Some namespaceTemplateId }
                    |> Fixtures.Namespace.appendLabel (Fixtures.Label.newLabel (Guid.NewGuid()))
                    |> Fixtures.Namespace.namespaceAdded
                )

            let randomBoundedContext =
                Fixtures.Builders.givenARandomDomainWithBoundedContextAndNamespace simulation

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
        
    [<Fact>]
    let ``When trying to delete a namespace with a malformed namespace-id then the bounded context is not deleted instead``() =
        task {
            let simulation = FixedTimeEnvironment.FromSystemClock()
            let domainId = simulation |> PseudoRandom.guid
            let contextId = simulation |> PseudoRandom.guid
            let namespaceId = simulation |> PseudoRandom.guid                
        
            let given =
                Fixtures.Builders.givenADomainWithOneBoundedContextAndOneNamespace domainId contextId namespaceId
                
            use testEnvironment = Prepare.withGiven simulation given

            //act
            let! result =
                testEnvironment
                |> When.deleting $"api/boundedContexts/%O{contextId}/namespaces/\"%O{namespaceId}\""

            // assert
            Then.Response.shouldNotBeSuccessful result
            Then.Response.shouldHaveStatusCode HttpStatusCode.NotFound result
        }

    type ``When searching for two different label names``() =

        let simulation = FixedTimeEnvironment.FromSystemClock()

        let namespaceTemplateId =
                Guid("A9F5D70E-B947-40B6-B7BE-4AC45CFE7F34")
        let domainId = simulation |> PseudoRandom.guid

        let firstLabel =
            { Fixtures.Label.newLabel (Guid.NewGuid()) with
                  Name = "first" }

        let secondLabel =
            { Fixtures.Label.newLabel (Guid.NewGuid()) with
                  Name = "second" }
            
        
        let whenSearchingForDifferentLabels =
            When.searchingFor $"Label.Name=%s{firstLabel.Name}&Label.Name=%s{secondLabel.Name}"

        [<Fact>]
        member _. ``Given one bounded context with two different labels in the same namespace, then the bounded context is found`` () =
            task {
                let contextId = simulation |> PseudoRandom.guid
                let singleNamespaceId = simulation |> PseudoRandom.guid                
            
                let searchedBoundedContext =
                    Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId
                    |> Given.andOneEvent (
                        { Fixtures.Namespace.definition contextId singleNamespaceId with
                              NamespaceTemplateId = Some namespaceTemplateId }
                        |> Fixtures.Namespace.appendLabel firstLabel
                        |> Fixtures.Namespace.appendLabel secondLabel
                        |> Fixtures.Namespace.namespaceAdded
                    )

                let randomBoundedContext =
                    Fixtures.Builders.givenARandomDomainWithBoundedContextAndNamespace simulation

                let given =
                    searchedBoundedContext @ randomBoundedContext

                use testEnvironment = Prepare.withGiven simulation given

                //act
                let! result =
                    testEnvironment
                    |> whenSearchingForDifferentLabels

                // assert
                Then.NotEmpty result
                Then.Collection(result, (fun x -> Then.Equal(contextId, x)))
            }
            
        [<Fact>]
        member _. ``Given one bounded context with two different labels in two different namespaces, then the bounded context is found`` () =
            task {
                let contextId = simulation |> PseudoRandom.guid
                let firstNamespaceId = simulation |> PseudoRandom.guid
                let secondNamespaceId = simulation |> PseudoRandom.guid
            
                let firstBoundedContext =
                    Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId
                    |> Given.andOneEvent (
                        { Fixtures.Namespace.definition contextId firstNamespaceId with
                              NamespaceTemplateId = Some namespaceTemplateId }
                        |> Fixtures.Namespace.appendLabel firstLabel
                        |> Fixtures.Namespace.namespaceAdded
                    )
                let secondBoundedContext =
                    Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId
                    |> Given.andOneEvent (
                        { Fixtures.Namespace.definition contextId secondNamespaceId with
                              NamespaceTemplateId = Some namespaceTemplateId }
                        |> Fixtures.Namespace.appendLabel secondLabel
                        |> Fixtures.Namespace.namespaceAdded
                    )

                let randomBoundedContext =
                    Fixtures.Builders.givenARandomDomainWithBoundedContextAndNamespace simulation

                let given =
                    firstBoundedContext @ secondBoundedContext @ randomBoundedContext

                use testEnvironment = Prepare.withGiven simulation given

                //act
                let! result =
                    testEnvironment
                    |> whenSearchingForDifferentLabels

                // assert
                Then.NotEmpty result
                Then.Collection(result, (fun x -> Then.Equal(contextId, x)))
            }
        
        [<Fact>]
        member _.``Given two bounded contexts with different label names, then no bounded context should be found`` () =
            task {
                let firstContextId = simulation |> PseudoRandom.guid                
                let firstBoundedContext =
                    Fixtures.Builders.givenADomainWithOneBoundedContext domainId firstContextId
                    |> Given.andOneEvent (
                        { Fixtures.Namespace.definition firstContextId (simulation |> PseudoRandom.guid) with
                              NamespaceTemplateId = Some namespaceTemplateId }
                        |> Fixtures.Namespace.appendLabel firstLabel
                        |> Fixtures.Namespace.namespaceAdded
                    )

                let secondContextId = simulation |> PseudoRandom.guid
                let secondBoundedContext =
                    Fixtures.Builders.givenADomainWithOneBoundedContext domainId secondContextId
                    |> Given.andOneEvent (
                        { Fixtures.Namespace.definition secondContextId (simulation |> PseudoRandom.guid) with
                              NamespaceTemplateId = Some namespaceTemplateId }
                        |> Fixtures.Namespace.appendLabel secondLabel
                        |> Fixtures.Namespace.namespaceAdded
                    )

                let randomBoundedContext =
                    Fixtures.Builders.givenARandomDomainWithBoundedContextAndNamespace simulation

                let given =
                    firstBoundedContext @ secondBoundedContext @ randomBoundedContext

                use testEnvironment = Prepare.withGiven simulation given

                //act
                let! result =
                    testEnvironment
                    |> whenSearchingForDifferentLabels

                // assert
                Then.Empty result
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
                Fixtures.Builders.givenARandomDomainWithBoundedContextAndNamespace simulation

            let given =
                searchedBoundedContext @ randomBoundedContext

            Prepare.withGiven simulation given

        type ``with a single string based parameter``() =
            let simulation = FixedTimeEnvironment.FromSystemClock()

            let namespaceId = simulation |> PseudoRandom.guid
            let contextId = simulation |> PseudoRandom.guid
            let domainId = simulation |> PseudoRandom.guid

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
            let environment = FixedTimeEnvironment.FromSystemClock()

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

            use testEnvironment = Prepare.withGiven environment given

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
