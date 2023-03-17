module Contexture.Api.Tests.Specs.Deleting

open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.Collaboration.ValueObjects
open Contexture.Api.Infrastructure
open Contexture.Api.Reactions
open Contexture.Api.Tests
open Contexture.Api.Tests.EnvironmentSimulation
open Xunit

let scenario (env: ISimulateEnvironment) =
    let parentDomain = PseudoRandom.guid env
    let parentBc = PseudoRandom.guid env
    let parentNamespace = PseudoRandom.guid env

    let parentEvents =
        Fixtures.Builders.givenADomainWithOneBoundedContextAndOneNamespace parentDomain parentBc parentNamespace

    let subDomain = PseudoRandom.guid env
    let subBc = PseudoRandom.guid env
    let subNamespace = PseudoRandom.guid env

    let subEvents =
        Fixtures.Builders.givenADomainWithOneBoundedContextAndOneNamespace subDomain subBc subNamespace
        @ [ Domain.CategorizedAsSubdomain
                { DomainId = subDomain
                  ParentDomainId = parentDomain
                  OldParentDomainId = None }
            |> Utils.asEvent subDomain ]


    let subSubDomain = PseudoRandom.guid env
    let subSubBc = PseudoRandom.guid env
    let subSubNamespace = PseudoRandom.guid env

    let subSubEvents =
        Fixtures.Builders.givenADomainWithOneBoundedContextAndOneNamespace subSubDomain subSubBc subSubNamespace
        @ [ Domain.CategorizedAsSubdomain
                { DomainId = subSubDomain
                  ParentDomainId = subDomain
                  OldParentDomainId = None }
            |> Utils.asEvent subSubDomain ]

    [ parentDomain, parentBc, parentNamespace
      subDomain, subBc, subNamespace
      subSubDomain, subSubBc, subSubNamespace ],
    parentEvents @ subEvents @ subSubEvents

module Then =
    let containsBoundedContextRemoved boundedContextId { Changes = events } =
        Then.Contains(
            events,
            fun item ->
                match item.Event with
                | AllEvents.BoundedContexts(BoundedContext.BoundedContextRemoved e) ->
                    e.BoundedContextId = boundedContextId
                | _ -> false
        )

    let containsDomainRemoved domainId { Changes = events } =
        Then.Contains(
            events,
            fun item ->
                match item.Event with
                | AllEvents.Domains(Domain.DomainRemoved e) -> e.DomainId = domainId
                | _ -> false
        )

    let containsNamespaceRemoved namespaceId { Changes = events } =
        Then.Contains(
            events,
            fun item ->
                match item.Event with
                | AllEvents.Namespaces(Namespace.NamespaceRemoved e) -> e.NamespaceId = namespaceId
                | _ -> false
        )

[<Fact>]
let ``When deleting the bounded context the collaborations and namespaces are deleted with it`` () =
    task {
        let environment = FixedTimeEnvironment.FromSystemClock()
        let identifiers, events = scenario environment

        let _, bcToBeDeleted, namespaceOfDeletedBc = identifiers |> List.item 2
        let referencedDomain, referencedBc, _ = identifiers |> List.head

        let bcToBcCollaborationInitiator = PseudoRandom.guid environment
        let bcToDomainCollaborationInitiator = PseudoRandom.guid environment
        let bcToBcCollaborationRecipient = PseudoRandom.guid environment
        let bcToDomainCollaborationRecipient = PseudoRandom.guid environment

        let given =
            events
            @ [ Fixtures.Builders.givenACollaborationBetween
                    bcToBcCollaborationInitiator
                    (BoundedContext bcToBeDeleted)
                    (BoundedContext referencedBc)
                Fixtures.Builders.givenACollaborationBetween
                    bcToDomainCollaborationInitiator
                    (BoundedContext bcToBeDeleted)
                    (Domain referencedDomain)
                Fixtures.Builders.givenACollaborationBetween
                    bcToBcCollaborationRecipient
                    (BoundedContext referencedBc)
                    (BoundedContext bcToBeDeleted)
                Fixtures.Builders.givenACollaborationBetween
                    bcToDomainCollaborationRecipient
                    (Domain referencedDomain)
                    (BoundedContext bcToBeDeleted) ]

        use! testEnvironment = Prepare.withGiven environment given

        let! result =
            testEnvironment
            |> When.deleting (sprintf "api/boundedContexts/%O" bcToBeDeleted)

        Then.Response.shouldBeSuccessful result

        Then.Events.arePublished result

        result |> Then.containsBoundedContextRemoved bcToBeDeleted
        result |> Then.containsNamespaceRemoved namespaceOfDeletedBc

        let collaborationEvents =
            result
            |> WhenResult.events(function
                | AllEvents.Collaboration e -> Some e
                | _ -> None)

        Then.NotEmpty collaborationEvents
        Then.Equal(collaborationEvents.Length, result.Changes.Length - 2 (* BC and namespace events *) )

        let deletedEvents =
            collaborationEvents
            |> List.choose (function
                | Collaboration.ConnectionRemoved e -> Some e.CollaborationId
                | _ -> None)

        Then.NotEmpty deletedEvents
        Then.Equal(deletedEvents.Length, collaborationEvents.Length)

        Then.Contains(bcToBcCollaborationInitiator, deletedEvents)
        Then.Contains(bcToDomainCollaborationInitiator, deletedEvents)
        Then.Contains(bcToBcCollaborationRecipient, deletedEvents)
        Then.Contains(bcToDomainCollaborationRecipient, deletedEvents)
    }

[<Fact>]
let ``When deleting a domain the bounded contexts, collaborations and namespaces are deleted with it`` () =
    task {
        let environment = FixedTimeEnvironment.FromSystemClock()
        let identifiers, events = scenario environment

        let domainToBeDeleted, bcOfDeletedDomain, namespaceOfDeletedBc =
            identifiers |> List.item 2

        let referencedDomain, referencedBc, _ = identifiers |> List.head

        let domainToBcCollaborationInitiator = PseudoRandom.guid environment
        let domainToDomainCollaborationInitiator = PseudoRandom.guid environment
        let domainToBcCollaborationRecipient = PseudoRandom.guid environment
        let domainToDomainCollaborationRecipient = PseudoRandom.guid environment

        let given =
            events
            @ [ Fixtures.Builders.givenACollaborationBetween
                    domainToBcCollaborationInitiator
                    (Domain domainToBeDeleted)
                    (BoundedContext referencedBc)
                Fixtures.Builders.givenACollaborationBetween
                    domainToDomainCollaborationInitiator
                    (Domain domainToBeDeleted)
                    (Domain referencedDomain)
                Fixtures.Builders.givenACollaborationBetween
                    domainToBcCollaborationRecipient
                    (BoundedContext referencedBc)
                    (Domain domainToBeDeleted)
                Fixtures.Builders.givenACollaborationBetween
                    domainToDomainCollaborationRecipient
                    (Domain referencedDomain)
                    (Domain domainToBeDeleted) ]

        use! testEnvironment = Prepare.withGiven environment given

        let! result = testEnvironment |> When.deleting (sprintf "api/domains/%O" domainToBeDeleted)

        Then.Response.shouldBeSuccessful result
        Then.Events.arePublished result

        result |> Then.containsDomainRemoved domainToBeDeleted
        result |> Then.containsBoundedContextRemoved bcOfDeletedDomain
        result |> Then.containsNamespaceRemoved namespaceOfDeletedBc

        let collaborationEvents =
            result
            |> WhenResult.events (function
                | AllEvents.Collaboration e -> Some e
                | _ -> None)

        Then.NotEmpty collaborationEvents
        Then.Equal(collaborationEvents.Length, result.Changes.Length - 3 (* domain, bc and namespace events *) )

        let deletedEvents =
            collaborationEvents
            |> List.choose (function
                | Collaboration.ConnectionRemoved e -> Some e.CollaborationId
                | _ -> None)

        Then.NotEmpty deletedEvents
        Then.Equal(deletedEvents.Length, collaborationEvents.Length)

        Then.Contains(domainToBcCollaborationInitiator, deletedEvents)
        Then.Contains(domainToDomainCollaborationInitiator, deletedEvents)
        Then.Contains(domainToBcCollaborationRecipient, deletedEvents)
        Then.Contains(domainToDomainCollaborationRecipient, deletedEvents)
    }

[<Fact>]
let ``When deleting a domain with subdomains then the subdomain, bounded contexts and namespaces are deleted with it``() =
    task {
        let environment = FixedTimeEnvironment.FromSystemClock()
        let identifiers, given = scenario environment

        let domainToBeDeleted, bcToBeDeleted, namespaceToBeDeleted =
            identifiers |> List.item 1

        let otherDomainToBeDeleted, otherBcToBeDeleted, otherNamespaceToBeDeleted =
            identifiers |> List.item 2

        use! testEnvironment = Prepare.withGiven environment given

        let! result = testEnvironment |> When.deleting (sprintf "api/domains/%O" domainToBeDeleted)

        Then.Response.shouldBeSuccessful result
        Then.Events.arePublished result

        result |> Then.containsDomainRemoved domainToBeDeleted
        result |> Then.containsDomainRemoved otherDomainToBeDeleted

        result |> Then.containsBoundedContextRemoved bcToBeDeleted
        result |> Then.containsBoundedContextRemoved otherBcToBeDeleted

        result |> Then.containsNamespaceRemoved namespaceToBeDeleted
        result |> Then.containsNamespaceRemoved otherNamespaceToBeDeleted
    }
