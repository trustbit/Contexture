module Contexture.Api.Tests.Specs.BoundedContext.Searching

open System
open Contexture.Api.Tests
open Contexture.Api.Tests.EnvironmentSimulation

open Contexture.Api.Aggregates.BoundedContext.ValueObjects
open Xunit

module When =
    open TestHost

    let searchingFor queryParameter (environment: TestHostEnvironment) =
        task {
            let! result =
                environment
                |> When.gettingJson<{| Id: BoundedContextId |} array> $"api/boundedContexts?%s{queryParameter}"

            return result |> WhenResult.map (Seq.map (fun i -> i.Id))
        }

    module Searching =
        let forALabelNamed name = $"Label.Name=%s{name}"

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

            use! testEnvironment = Prepare.withGiven simulation given

            //act
            let! result =
                testEnvironment
                |> whenSearchingForDifferentLabels

            // assert
            Then.Items.areNotEmpty result
            Then.Collection(result.Result, (fun x -> Then.Equal(contextId, x)))
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

            use! testEnvironment = Prepare.withGiven simulation given

            //act
            let! result =
                testEnvironment
                |> whenSearchingForDifferentLabels

            // assert
            Then.Items.areNotEmpty result
            Then.Collection(result.Result, (fun x -> Then.Equal(contextId, x)))
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

            use! testEnvironment = Prepare.withGiven simulation given

            //act
            let! result =
                testEnvironment
                |> whenSearchingForDifferentLabels

            // assert
            Then.Items.areEmpty result
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
            Then.Items.areNotEmpty result
            Then.Collection(result.Result, (fun x -> Then.Equal(contextId, x)))
            
    let prepareTestEnvironment simulation searchedBoundedContext =
        let randomBoundedContext =
            Fixtures.Builders.givenARandomDomainWithBoundedContextAndNamespace simulation

        let given =
            searchedBoundedContext @ randomBoundedContext

        Prepare.withGiven simulation given
 
    [<Fact>]
    let ``When searching with a random query string a bad request is returned`` () =
        task {
            let environment = FixedTimeEnvironment.FromSystemClock()

            // arrange
            let contextId = environment |> PseudoRandom.guid
            let domainId = environment |> PseudoRandom.guid

            let given =
                Fixtures.Builders.givenADomainWithOneBoundedContext domainId contextId

            use! testEnvironment = Prepare.withGiven environment given

            //act
            let! result =
                testEnvironment
                |> When.getting (sprintf "api/boundedContexts?bar.foo=baz")

            // assert
            do! Then.theResponseShould.beBadRequest result
        }
    
    [<Theory>]
    [<InlineData("Label.name", "Architect")>]
    [<InlineData("Label.value", "John Doe")>]
    [<InlineData("Namespace.name", "Team")>]
    [<InlineData("Namespace.template", "A9F5D70E-B947-40B6-B7BE-4AC45CFE7F34")>]
    [<InlineData("Domain.name", "domain")>]
    [<InlineData("Domain.shortName", "DO-1")>]
    [<InlineData("BoundedContext.name", "bounded-context")>]
    [<InlineData("BoundedContext.shortName", "BC-1")>]
    let ``with a single, exact parameter then only the bounded context is found``
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
                
            use! testEnvironment = prepareTestEnvironment simulation searchedBoundedContext
            
            //act
            let! result =
                testEnvironment
                |> When.searchingFor $"%s{parameterName}=%s{parameterValue}"

            // assert
            Then.Items.areNotEmpty result
            Then.Collection(result.Result, (fun x -> Then.Equal(contextId, x)))
        }

    [<Fact>]
    let ``with one single and exact parameter and one non-matching wildcard search, then no bounded context should be found`` () =
        task {
            let simulation = FixedTimeEnvironment.FromSystemClock()

            let domainId = simulation |> PseudoRandom.guid
            
            let firstContextId = simulation |> PseudoRandom.guid
            let secondContextId = simulation |> PseudoRandom.guid                
            
            let given =
                Given.noEvents
                |> Given.andEvents [
                     domainId
                     |> Domain.domainDefinition
                     |> Domain.domainCreated
                     { Domain.shortName domainId 
                       with ShortName = Some "DomainShortName" }
                        |> Domain.shortNameAssigned
                ]
                |> Given.andEvents [
                    { BoundedContext.definition domainId firstContextId
                        with Name = "First" }
                    |> BoundedContext.boundedContextCreated
                    firstContextId |> BoundedContext.shortName |> BoundedContext.shortNameAssigned
                ]
                |> Given.andEvents [
                    { BoundedContext.definition domainId secondContextId
                        with Name = "Second" }
                    |> BoundedContext.boundedContextCreated
                    secondContextId |> BoundedContext.shortName |> BoundedContext.shortNameAssigned
                ]                
                
            use! testEnvironment = prepareTestEnvironment simulation given
            
            //act
            let! result =
                testEnvironment
                |> When.searchingFor $"Domain.shortName=DomainShortName&BoundedContext.Name=*Third*"

            // assert
            Then.Items.areEmpty result
        }       

    type ``with a single string based parameter``() =
        let simulation = FixedTimeEnvironment.FromSystemClock()

        let namespaceId = simulation |> PseudoRandom.guid
        let contextId = simulation |> PseudoRandom.guid
        let domainId = simulation |> PseudoRandom.guid

        [<Fact>]
        member __.``it is possible to find label names by using 'arch*' as StartsWith``() =
            task {
                use! testEnvironment =
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
                use! testEnvironment =
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
                use! testEnvironment =
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
                use! testEnvironment =
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

        use! testEnvironment = Prepare.withGiven environment given

        //act - search by name
        let! result =
            testEnvironment
            |> When.searchingFor $"Label.name=arch*&Namespace.Template=%O{templateId}"

        // assert
        Then.Items.areNotEmpty result
        Then.Collection(result.Result, (fun x -> Then.Equal(contextId, x)))

        //act - search by value
        let! result =
            testEnvironment
            |> When.searchingFor $"Label.value=Joh*&Namespace.Template=%O{templateId}"

        // assert
        Then.Items.areNotEmpty result
        Then.Collection(result.Result, (fun x -> Then.Equal(contextId, x)))

        // act - search by namespace name
        let! result =
            testEnvironment
            |> When.searchingFor $"Label.value=Joh*&Namespace.Name=%s{name}"

        // assert
        Then.Items.areNotEmpty result
        Then.Collection(result.Result, (fun x -> Then.Equal(contextId, x)))
    }
