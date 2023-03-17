module Contexture.Api.Tests.ApiTests

open System

open System.Net
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Aggregates.Domain
open Contexture.Api.Aggregates.Namespace
open Contexture.Api.Infrastructure
open Xunit
open Xunit.Sdk
open Contexture.Api.Tests.EnvironmentSimulation
open ValueObjects
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

            use! testEnvironment = Prepare.withGiven environment given

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

    [<Fact>]
    let ``Can list all bounded contexts`` () =
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

            use! testEnvironment = Prepare.withGiven environment given

            //act
            let! result =
                testEnvironment
                |> When.gettingJson<{| Id: BoundedContextId |} array> (sprintf "api/boundedContexts?bar.foo=baz")

            // assert
            Then.NotEmpty result
            Then.Contains(contextId, result |> Array.map (fun i -> i.Id))
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
                
            use! testEnvironment = Prepare.withGiven simulation given

            //act
            let! _,result =
                testEnvironment
                |> When.deleting $"api/boundedContexts/%O{contextId}/namespaces/\"%O{namespaceId}\""

            // assert
            Then.Response.shouldNotBeSuccessful result
            Then.Response.shouldHaveStatusCode HttpStatusCode.NotFound result
        }
