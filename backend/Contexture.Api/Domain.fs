namespace Contexture.Api

open System

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
        with
            static member Unknown =
                {
                    DomainType = None
                    BusinessModel = []
                    Evolution = None
                }

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
        with
        static member Empty =
            {
                CommandsHandled = []
                CommandsSent = []
                EventsHandled = []
                EventsPublished = []
                QueriesHandled = []
                QueriesInvoked = []
            }

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
        { Tools: Lifecycle option
          Deployment: Deployment option }

    type DomainId = int
    
    type BoundedContext =
        { Id: int
          DomainId: DomainId
          Key: string option
          Name: string
          Description: string option
          Classification: StrategicClassification
          BusinessDecisions: BusinessDecision list
          UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
          Messages: Messages
          DomainRoles: DomainRole list
          TechnicalDescription: TechnicalDescription option }
    
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


module Aggregates =
    
    open Domain
    
    module Domain =
                    
        type Command =
            | CreateDomain of CreateDomain
            | RenameDomain of DomainId * RenameDomain
            | MoveDomain of DomainId * MoveDomain
            | RefineVision of DomainId * RefineVision
            | AssignKey of DomainId * AssignKey
            | RemoveDomain of DomainId

        and CreateDomain = { Name: string }
        and RenameDomain = { Name: string }
        and MoveDomain = { ParentDomainId: int option }
        and RefineVision = { Vision: string }
        and AssignKey = { Key: string }
                    
        type Errors = | EmptyName

        let nameValidation name =
            if String.IsNullOrWhiteSpace name then Error EmptyName else Ok name
            
        let newDomain name =
            name
            |> nameValidation
            |> Result.map (fun name ->
                fun id ->
                    { Id = id
                      Key = None
                      ParentDomainId = None
                      Name = name
                      Vision = None }
            )

        let moveDomain parent (domain: Domain) = Ok { domain with ParentDomainId = parent }

        let refineVisionOfDomain vision (domain: Domain) =
            Ok
                { domain with
                      Vision =
                          vision
                          |> Option.ofObj
                          |> Option.filter (String.IsNullOrWhiteSpace >> not) }

        let renameDomain potentialName (domain: Domain) =
            potentialName
            |> nameValidation
            |> Result.map (fun name -> { domain with Name = name })

        let assignKeyToDomain key (domain: Domain) =
            Ok
                { domain with
                      Key =
                          key
                          |> Option.ofObj
                          |> Option.filter (String.IsNullOrWhiteSpace >> not) }


    module BoundedContext =
        module Commands =
            type CreateBoundedContext = { Name: string }

        type Errors = | EmptyName

        let nameValidation name =
            if String.IsNullOrWhiteSpace name then Error EmptyName else Ok name
            
        let newBoundedContext domain name =
            name
            |> nameValidation
            |> Result.map (fun name ->
                fun id ->
                    { Id = id
                      Key = None
                      DomainId = domain
                      Name = name
                      Description = None
                      Classification = StrategicClassification.Unknown
                      BusinessDecisions = []
                      UbiquitousLanguage = Map.empty
                      Messages = Messages.Empty
                      DomainRoles = []
                      TechnicalDescription = None }
            )


