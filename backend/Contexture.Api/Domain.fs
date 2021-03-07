﻿namespace Contexture.Api

open System
open System.Text.Json.Serialization

module Domain =

    type DomainType =
        | Core
        | Supporting
        | Generic
        | OtherDomainType of string

    type BusinessModel =
        | Revenue
        | Engagement
        | Compliance
        | CostReduction
        | OtherBusinessModel of string

    type Evolution =
        | Genesis
        | CustomBuilt
        | Product
        | Commodity

    type StrategicClassification =
        { DomainType: DomainType option
          BusinessModel: BusinessModel list
          Evolution: Evolution option }

    type BusinessDecision = { Name: string; Description: string }

    type UbiquitousLanguageTerm =
        { Term: string
          Description: string option }

    type Message = string
    type Command = Message
    type Event = Message
    type Query = Message

    type Messages =
        { CommandsHandled: Command list
          CommandsSent: Command list
          EventsHandled: Event list
          EventsPublished: Event list
          QueriesHandled: Query list
          QueriesInvoked: Query list }

    type DomainRole =
        { Name: string
          Description: string option }

    type Lifecycle =
        { IssueTracker: Uri option
          Wiki: Uri option
          Repository: Uri option }

    type Deployment =
        { HealthCheck: Uri option
          Artifacts: Uri option }

    type TechnicalDescription =
        { Tools: Lifecycle
          Deployment: Deployment }

    type BoundedContext =
        { Id: int
          DomainId: int
          Key: string
          Name: string
          Description: string
          Classification: StrategicClassification
          BusinessDecisions: BusinessDecision list
          UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
          ModelTraits: string
          Messages: Messages
          DomainRoles: DomainRole list
          TechnicalDescription: TechnicalDescription }

    type Domain =
        { Id: int
          ParentDomain: int option
          Key: string option
          Name: string
          Vision: string
          Subdomains: Domain list
          BoundedContexts: BoundedContext list }

    type SymmetricRelationship =
        | SharedKernel
        | Partnership
        | SeparateWays
        | BigBallOfMud

    type UpstreamRelationship =
        | Upstream
        | PublishedLanguage
        | OpenHost

    type DownstreamRelationship =
        | Downstream
        | AntiCorruptionLayer
        | Conformist

    type InitiatorCustomerSupplierRole =
        | SupplierRole
        | CustomerRole

    type InitiatorUpstreamDownstreamRole =
        | Upstream
        | Downstream

    type UpstreamDownstreamRelationship =
        | CustomerSupplierRelationship of initiatorRole: InitiatorCustomerSupplierRole
        | UpstreamDownstreamRelationship of
            initiatorRole: InitiatorUpstreamDownstreamRole *
            upstreamType: UpstreamRelationship *
            downstreamType: DownstreamRelationship

    type RelationshipType =
        | Symmetric of Symmetric: SymmetricRelationship
        | UpstreamDownstream of UpstreamDownstream: UpstreamDownstreamRelationship
        | Unknown

    type Collaborator =
        | BoundedContext of BoundedContext: int
        | Domain of Domain: int
        | ExternalSystem of ExternalSystem: string
        | Frontend of Frontend: string
        | UserInteraction of UserInteraction: string

    type Collaboration =
        { Id: int
          Description: string option
          Initiator: Collaborator
          Recipient: Collaborator
          RelationshipType: RelationshipType option }

    type Root =
        { Domains: Domain list
          BoundedContexts: BoundedContext list
          BusinessDecisions: BusinessDecision list
          Collaborations: Collaboration list }