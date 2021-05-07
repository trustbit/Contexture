namespace Contexture.Api.Tests

open System
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.BoundedContext
open Contexture.Api.Aggregates.Domain
open Contexture.Api.Aggregates.Namespace
open Contexture.Api.Entities
open Contexture.Api.Infrastructure
open Contexture.Api.Tests.EnvironmentSimulation

module Fixtures =
    module Domain =
        [<Literal>]
        let Name = "domain"

        [<Literal>]
        let Key = "DO-1"

        let domainDefinition domainId : DomainCreated = { DomainId = domainId; Name = Name }

        let domainCreated definition =
            DomainCreated definition
            |> Utils.asEvent definition.DomainId

        let key domainId : KeyAssigned = { DomainId = domainId; Key = Some Key }

        let keyAssigned key =
            KeyAssigned key |> Utils.asEvent key.DomainId

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


    module Builders =
        let givenARandomDomainWithBoundedContextAndNamespace environment =
            let namespaceId = environment |> PseudoRandom.guid
            let contextId = environment |> PseudoRandom.guid
            let domainId = environment |> PseudoRandom.guid

            Given.noEvents
            |> Given.andOneEvent (
                { Domain.domainDefinition domainId with
                      Name =
                          environment
                          |> PseudoRandom.nameWithGuid "random-domain-name" }
                |> Domain.domainCreated
            )
            |> Given.andOneEvent (
                { BoundedContext.definition domainId contextId with
                      Name =
                          environment
                          |> PseudoRandom.nameWithGuid "random-context-name" }
                |> BoundedContext.boundedContextCreated
            )
            |> Given.andOneEvent (
                { Namespace.definition contextId namespaceId with
                      Name =
                          environment
                          |> PseudoRandom.nameWithGuid "random-namespace-name"
                      Labels =
                          [ { LabelId = environment |> PseudoRandom.guid
                              Name =
                                  environment
                                  |> PseudoRandom.nameWithGuid "random-label-name"
                              Value =
                                  environment
                                  |> PseudoRandom.nameWithGuid "random-label-value"
                                  |> Some
                              Template = None } ] }
                |> Namespace.namespaceAdded
            )

        let givenADomainWithOneBoundedContext domainId contextId =
            Given.noEvents
            |> Given.andEvents [ domainId
                                 |> Domain.domainDefinition
                                 |> Domain.domainCreated
                                 domainId |> Domain.key |> Domain.keyAssigned ]
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
