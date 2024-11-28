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
open Fixtures
open Fixtures.Builders

module Namespaces =

    [<Fact>]
    let ``Can create a new namespace`` () =
        task {
            // arrange
            let environment = FixedTimeEnvironment.FromSystemClock()
            let domainId = environment |> PseudoRandom.guid
            let contextId = environment |> PseudoRandom.guid

            let given = givenADomainWithOneBoundedContext domainId contextId

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

    [<Fact>]
    let ``can edit namespace label``() = task {
        // arrange
        let environment = FixedTimeEnvironment.FromSystemClock()
        let domainId = environment |> PseudoRandom.guid
        let contextId = environment |> PseudoRandom.guid
        let namespaceId = environment |> PseudoRandom.guid
        let labelId = environment |> PseudoRandom.guid
        let labelDefinition = { 
            LabelId = labelId
            Name = "initial name"
            Value = Some "initial value"
            Template = None 
        }

        let given = 
            givenADomainWithOneBoundedContext domainId contextId
            |> Given.andOneEvent (
                Namespace.definition contextId namespaceId
                |> Namespace.appendLabel (labelDefinition)
                |> Namespace.namespaceAdded
            )

        use! testEnvironment = Prepare.withGiven environment given

        let updateLabelBody = """
            {
                "name": "updated name",
                "value": "updated value"
            }
        """
        let! response = When.postingJson $"api/boundedcontexts/{contextId}/namespaces/{namespaceId}/labels/{labelId}" updateLabelBody testEnvironment
        let! result = response |> WhenResult.asJsonResponse<Projections.Namespace list>

        let namespaceLabels = 
            result.Result 
            |> List.find(fun n -> n.Id = namespaceId)
            |> fun ns -> ns.Labels

        let expectedLabel: Projections.Label = {
            Id = labelId
            Name = "updated name"
            Value = "updated value"
            Template = None
        }

        Then.Single namespaceLabels
        |> fun label -> Then.Equal(expectedLabel, label)
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
            Then.Items.areNotEmpty result
            result |> WhenResult.map (Seq.map (fun i -> i.Id)) |> Then.Items.contains contextId
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
            let! result =
                testEnvironment
                |> When.deleting $"api/boundedContexts/%O{contextId}/namespaces/\"%O{namespaceId}\""

            // assert
            // Then.Response.shouldNotBeSuccessful result
            do! Then.theResponseShould.beNotFound result
        }
