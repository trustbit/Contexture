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
    type BoundedContextId = int

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
        | BoundedContext of BoundedContext: BoundedContextId
        | Domain of Domain: DomainId
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

    module Domain =
        open Domain

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
            |> Result.map (fun name id ->
                { Id = id
                  Key = None
                  ParentDomainId = None
                  Name = name
                  Vision = None })

        let moveDomain parent (domain: Domain) =
            Ok { domain with ParentDomainId = parent }

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
        open Domain

        type Command =
            | CreateBoundedContext of DomainId * CreateBoundedContext
            | UpdateTechnicalInformation of BoundedContextId * UpdateTechnicalInformation
            | RenameBoundedContext of BoundedContextId * RenameBoundedContext
            | AssignKey of BoundedContextId * AssignKey
            | RemoveBoundedContext of BoundedContextId
            | MoveBoundedContextToDomain of BoundedContextId * MoveBoundedContextToDomain
            | ReclassifyBoundedContext of BoundedContextId * ReclassifyBoundedContext
            | ChangeDescription of BoundedContextId * ChangeDescription
            // TODO: replace with add/remove instead of updateing all
            | UpdateBusinessDecisions of BoundedContextId * UpdateBusinessDecisions
            | UpdateUbiquitousLanguage of BoundedContextId * UpdateUbiquitousLanguage
            | UpdateDomainRoles of BoundedContextId * UpdateDomainRoles

        and CreateBoundedContext = { Name: string }

        and UpdateTechnicalInformation = TechnicalDescription

        and RenameBoundedContext = { Name: string }

        and MoveBoundedContextToDomain = { ParentDomainId: DomainId }

        and ReclassifyBoundedContext =
            { Classification: StrategicClassification }
        and ChangeDescription =
            { Description: string option }
        and UpdateBusinessDecisions =
            { BusinessDecisions: BusinessDecision list }
        and UpdateUbiquitousLanguage =
            { UbiquitousLanguage : Map<string, UbiquitousLanguageTerm> }

        and UpdateDomainRoles =
            { DomainRoles : DomainRole list }
        type Errors = | EmptyName

        let nameValidation name =
            if String.IsNullOrWhiteSpace name then Error EmptyName else Ok name

        let newBoundedContext domainId name =
            name
            |> nameValidation
            |> Result.map (fun name id ->
                { Id = id
                  Key = None
                  DomainId = domainId
                  Name = name
                  Description = None
                  Classification = StrategicClassification.Unknown
                  BusinessDecisions = []
                  UbiquitousLanguage = Map.empty
                  Messages = Messages.Empty
                  DomainRoles = []
                  TechnicalDescription = None })

        let updateTechnicalDescription description context =
            Ok
                { context with
                      TechnicalDescription = Some description }

        let renameBoundedContext potentialName (context: BoundedContext) =
            potentialName
            |> nameValidation
            |> Result.map (fun name -> { context with Name = name })

        let assignKeyToBoundedContext key (boundedContext: BoundedContext) =
            Ok
                { boundedContext with
                      Key =
                          key
                          |> Option.ofObj
                          |> Option.filter (String.IsNullOrWhiteSpace >> not) }

        let moveBoundedContext parent (boundedContext: BoundedContext) =
            Ok
                { boundedContext with
                      DomainId = parent }

        let reclassify classification (boundedContext: BoundedContext) =
            Ok
                { boundedContext with
                      Classification = classification }
                
        let description description (boundedContext: BoundedContext) =
            Ok
                { boundedContext with
                      Description = description }

        let updateBusinessDecisions decisions (boundedContext: BoundedContext) =
            Ok
                { boundedContext with
                      BusinessDecisions = decisions }
        
        let updateUbiquitousLanguage language (boundedContext: BoundedContext) =
            Ok
                { boundedContext with
                      UbiquitousLanguage = language }

        let updateDomainRoles roles (boundedContext: BoundedContext) =
            Ok
                { boundedContext with
                      DomainRoles = roles }