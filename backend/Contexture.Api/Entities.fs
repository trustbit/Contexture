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


    type NamespaceTemplateId = int
    type LabelTemplate = { Name: string }

    type NamespaceTemplate =
        { Id: NamespaceTemplateId
          Name: string
          Template: LabelTemplate list }

    type LabelId = Guid

    type Label =
        { Id: LabelId
          Name: string
          Value: string }

    type NamespaceId = Guid

    type Namespace =
        { Id: NamespaceId
          Template: NamespaceTemplateId option
          Name: string
          Labels: Label list }


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
          TechnicalDescription: TechnicalDescription option
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

module Aggregates =

    module Domain =
        open Entities

        type Command =
            | CreateDomain of CreateDomain
            | CreateSubdomain of DomainId * CreateDomain
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

        let newDomain name parentDomain =
            name
            |> nameValidation
            |> Result.map (fun name id ->
                { Id = id
                  Key = None
                  ParentDomainId = parentDomain
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
        open Entities

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
            | UpdateMessages of BoundedContextId * UpdateMessages

        and CreateBoundedContext = { Name: string }

        and UpdateTechnicalInformation = TechnicalDescription

        and RenameBoundedContext = { Name: string }

        and AssignKey = { Key: string }

        and MoveBoundedContextToDomain = { ParentDomainId: DomainId }

        and ReclassifyBoundedContext =
            { Classification: StrategicClassification }

        and ChangeDescription = { Description: string option }

        and UpdateBusinessDecisions =
            { BusinessDecisions: BusinessDecision list }

        and UpdateUbiquitousLanguage =
            { UbiquitousLanguage: Map<string, UbiquitousLanguageTerm> }

        and UpdateMessages = { Messages: Messages }

        and UpdateDomainRoles = { DomainRoles: DomainRole list }

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
                  TechnicalDescription = None
                  Namespaces = [] })

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

        let updateMessages messages (boundedContext: BoundedContext) =
            Ok
                { boundedContext with
                      Messages = messages }

    module Collaboration =
        open Entities

        type Command =
            | DefineRelationship of CollaborationId * DefineRelationship
            | DefineOutboundConnection of CollaborationId * DefineConnection
            | DefineInboundConnection of CollaborationId * DefineConnection
            | RemoveConnection of CollaborationId

        and DefineRelationship =
            { RelationshipType: RelationshipType option }

        and DefineConnection =
            { Description: string option
              Initiator: Collaborator
              Recipient: Collaborator }

        type Event =
            | CollaborationImported of CollaborationImported
            | RelationshipDefined of RelationshipDefined
            | RelationshipUnknown of RelationshipUnknown
            | CollaboratorsConnected of CollaboratorsConnected
            | ConnectionRemoved of ConnectionRemoved

        and CollaborationImported =
            { CollaborationId: CollaborationId
              Description: string option
              Initiator: Collaborator
              Recipient: Collaborator
              RelationshipType: RelationshipType option }

        and RelationshipDefined =
            { CollaborationId: CollaborationId
              RelationshipType: RelationshipType }

        and RelationshipUnknown = { CollaborationId: CollaborationId }

        and CollaboratorsConnected =
            { CollaborationId: CollaborationId
              Description: string option
              Initiator: Collaborator
              Recipient: Collaborator }

        and ConnectionRemoved = { CollaborationId: CollaborationId }
        
        type State =
            | Initial
            | Existing
            | Deleted
            static member Fold (state: State) (event: Event) =
                match event with
                | ConnectionRemoved _ ->
                    Deleted
                | _ -> Existing
                
        let identify =
            function
            | DefineInboundConnection (collaborationId, _) -> collaborationId
            | DefineOutboundConnection (collaborationId, _) -> collaborationId
            | DefineRelationship (collaborationId, _) -> collaborationId
            | RemoveConnection (collaborationId) -> collaborationId

        let name identity =
            identity
                
        let handle (state: State) (command: Command) =
            match state,command with
            | Existing, DefineRelationship (collaborationId, relationship)->
                match relationship.RelationshipType with
                | Some r ->
                    Ok [ RelationshipDefined { CollaborationId = collaborationId; RelationshipType = r }]
                | None ->
                    Ok [ RelationshipUnknown { CollaborationId = collaborationId } ]
            | Initial, DefineInboundConnection (collaborationId,connection) ->
                Ok [ CollaboratorsConnected { CollaborationId = collaborationId; Description = connection.Description; Initiator = connection.Initiator; Recipient = connection.Recipient } ]
            | Initial, DefineOutboundConnection (collaborationId,connection) ->
                Ok [ CollaboratorsConnected { CollaborationId = collaborationId; Description = connection.Description; Initiator = connection.Initiator; Recipient = connection.Recipient } ]
            | _ , RemoveConnection collaborationId ->
                Ok [ ConnectionRemoved { CollaborationId = collaborationId } ]
            | _,_ ->
                Ok []
                
        module Projections =
            let asCollaboration collaboration event =
                match event with
                | CollaborationImported c ->
                    Some
                        { Id = c.CollaborationId
                          Description = c.Description
                          Initiator = c.Initiator
                          Recipient = c.Recipient
                          RelationshipType = c.RelationshipType }
                | CollaboratorsConnected c ->
                    Some
                        { Id = c.CollaborationId
                          Description = c.Description
                          Initiator = c.Initiator
                          Recipient = c.Recipient
                          RelationshipType = None }
                | RelationshipDefined c ->
                    collaboration
                    |> Option.map (fun o ->
                        { o with
                              RelationshipType = Some c.RelationshipType })
                | RelationshipUnknown c ->
                    collaboration
                    |> Option.map (fun o -> { o with RelationshipType = None })
                | ConnectionRemoved _ -> None

    module Namespaces =
        open Entities
        type Errors = | EmptyName

        type Command =
            | NewNamespace of BoundedContextId * NamespaceDefinition
            | RemoveNamespace of BoundedContextId * NamespaceId
            | RemoveLabel of BoundedContextId * RemoveLabel
            | AddLabel of BoundedContextId * NamespaceId * LabelDefinition

        and NamespaceDefinition =
            { Name: string
              Labels: LabelDefinition list }

        and LabelDefinition = { Name: string; Value: string }

        and RemoveLabel =
            { Namespace: NamespaceId
              Label: LabelId }

        module Label =
            let create name (value: string) =
                if String.IsNullOrWhiteSpace name then
                    None
                else
                    Some
                        { Id = Guid.NewGuid()
                          Name = name.Trim()
                          Value = if not (isNull value) then value.Trim() else null }

        let addNewNamespace name labels namespaces =
            let newLabels =
                labels
                |> List.choose (fun label -> Label.create label.Name label.Value)

            let newNamespace =
                { Id = Guid.NewGuid()
                  Template = None
                  Name = name
                  Labels = newLabels }

            Ok(namespaces @ [ newNamespace ])

        let removeNamespace (namespaceId: NamespaceId) (namespaces: Namespace list) =
            namespaces
            |> List.filter (fun n -> n.Id <> namespaceId)
            |> Ok

        let removeLabel namespaceId labelId (namespaces: Namespace list) =
            namespaces
            |> List.map (fun n ->
                if n.Id = namespaceId then
                    { n with
                          Labels = n.Labels |> List.filter (fun l -> l.Id <> labelId) }
                else
                    n)
            |> Ok

        let addLabel namespaceId labelName value (namespaces: Namespace list) =
            match Label.create labelName value with
            | Some label ->
                namespaces
                |> List.map (fun n -> if n.Id = namespaceId then { n with Labels = n.Labels @ [ label ] } else n)
                |> Ok
            | None -> Error EmptyName
