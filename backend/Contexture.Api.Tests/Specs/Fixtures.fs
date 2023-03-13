namespace Contexture.Api.Tests

open System
open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.Namespace
open Contexture.Api.Tests.EnvironmentSimulation

module Fixtures =
    module Domain =
        open Domain

        [<Literal>]
        let Name = "domain"

        [<Literal>]
        let ShortName = "DO-1"

        let domainDefinition domainId : DomainCreated = { DomainId = domainId; Name = Name }

        let domainCreated definition =
            DomainCreated definition
            |> Utils.asEvent definition.DomainId

        let shortName domainId : ShortNameAssigned = { DomainId = domainId; ShortName = Some ShortName }

        let shortNameAssigned shortName =
            ShortNameAssigned shortName |> Utils.asEvent shortName.DomainId

    module BoundedContext =
        open BoundedContext

        [<Literal>]
        let Name = "bounded-context"

        [<Literal>]
        let ShortName = "BC-1"

        let definition domainId contextId =
            { BoundedContextId = contextId
              Name = Name
              DomainId = domainId }

        let boundedContextCreated definition =
            BoundedContextCreated definition
            |> Utils.asEvent definition.BoundedContextId

        let shortName domainId : ShortNameAssigned =
            { BoundedContextId = domainId
              ShortName = Some ShortName }

        let shortNameAssigned shortName =
            ShortNameAssigned shortName
            |> Utils.asEvent shortName.BoundedContextId

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
            |> Given.andEvents [
                 domainId
                 |> Domain.domainDefinition
                 |> Domain.domainCreated
                 domainId |> Domain.shortName |> Domain.shortNameAssigned
            ]
            |> Given.andEvents [
                contextId
                |> BoundedContext.definition domainId
                |> BoundedContext.boundedContextCreated
                contextId |> BoundedContext.shortName |> BoundedContext.shortNameAssigned
            ]

        let givenADomainWithOneBoundedContextAndOneNamespace domainId contextId namespaceId =
            givenADomainWithOneBoundedContext domainId contextId
            |> Given.andOneEvent (
                namespaceId
                |> Namespace.definition contextId
                |> Namespace.appendLabel (Label.newLabel (Guid.NewGuid()))
                |> Namespace.namespaceAdded
            )
