namespace Contexture.Api

open System

module Entities =

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
        static member Unknown =
            { DomainType = None
              BusinessModel = []
              Evolution = None }

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
        static member Empty =
            { CommandsHandled = []
              CommandsSent = []
              EventsHandled = []
              EventsPublished = []
              QueriesHandled = []
              QueriesInvoked = [] }

    type DomainRole =
        { Name: string
          Description: string option }

    type NamespaceTemplateId = Guid
    type TemplateLabelId = Guid
    type LabelId = Guid

    type Label =
        { Id: LabelId
          Name: string
          Value: string
          Template: TemplateLabelId option }

    type NamespaceId = Guid

    type Namespace =
        { Id: NamespaceId
          Template: NamespaceTemplateId option
          Name: string
          Labels: Label list }


    type DomainId = Guid
    type BoundedContextId = Guid

    type BoundedContext =
        { Id: BoundedContextId
          DomainId: DomainId
          Key: string option
          Name: string
          Description: string option
          Classification: StrategicClassification
          BusinessDecisions: BusinessDecision list
          UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
          Messages: Messages
          DomainRoles: DomainRole list
          Namespaces: Namespace list }

    type Domain =
        { Id: DomainId
          ParentDomainId: DomainId option
          Key: string option
          Name: string
          Vision: string option }

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
        | Supplier
        | Customer

    type InitiatorUpstreamDownstreamRole =
        | Upstream
        | Downstream

    type UpstreamDownstreamRelationship =
        | CustomerSupplierRelationship of role: InitiatorCustomerSupplierRole
        | UpstreamDownstreamRelationship of
            initiatorRole: InitiatorUpstreamDownstreamRole *
            upstreamType: UpstreamRelationship *
            downstreamType: DownstreamRelationship

    type RelationshipType =
        | Symmetric of symmetric: SymmetricRelationship
        | UpstreamDownstream of upstreamDownstream: UpstreamDownstreamRelationship
        | Unknown

    type Collaborator =
        | BoundedContext of BoundedContext: BoundedContextId
        | Domain of Domain: DomainId
        | ExternalSystem of ExternalSystem: string
        | Frontend of Frontend: string
        | UserInteraction of UserInteraction: string

    type CollaborationId = Guid

    type Collaboration =
        { Id: CollaborationId
          Description: string option
          Initiator: Collaborator
          Recipient: Collaborator
          RelationshipType: RelationshipType option }
