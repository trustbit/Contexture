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

module Aggregates =

    module Domain =
        open Entities

        type Command =
            | CreateDomain of DomainId * CreateDomain
            | CreateSubdomain of DomainId * subdomainOf: DomainId * CreateDomain
            | RenameDomain of DomainId * RenameDomain
            | MoveDomain of DomainId * MoveDomain
            | RefineVision of DomainId * RefineVision
            | AssignKey of DomainId * AssignKey
            | RemoveDomain of DomainId

        and CreateDomain = { Name: string }

        and RenameDomain = { Name: string }

        and MoveDomain = { ParentDomainId: DomainId option }

        and RefineVision = { Vision: string }

        and AssignKey = { Key: string }

        type Event =
            | DomainImported of DomainImported
            | DomainCreated of DomainCreated
            | SubDomainCreated of SubDomainCreated
            | DomainRenamed of DomainRenamed
            | CategorizedAsSubdomain of CategorizedAsSubdomain
            | PromotedToDomain of PromotedToDomain
            | VisionRefined of VisionRefined
            | DomainRemoved of DomainRemoved
            | KeyAssigned of KeyAssigned

        and DomainImported =
            { DomainId: DomainId
              ParentDomainId: DomainId option
              Key: string option
              Name: string
              Vision: string option }

        and DomainCreated = { DomainId: DomainId; Name: String }

        and SubDomainCreated =
            { DomainId: DomainId
              ParentDomainId: DomainId
              Name: String }

        and DomainRenamed = { DomainId: DomainId; Name: String; OldName: string }

        and CategorizedAsSubdomain =
            { DomainId: DomainId
              ParentDomainId: DomainId }

        and PromotedToDomain = { DomainId: DomainId }

        and VisionRefined =
            { DomainId: DomainId
              Vision: String option }

        and DomainRemoved = { DomainId: DomainId }

        and KeyAssigned =
            { DomainId: DomainId
              Key: string option }

        type Errors =
            | EmptyName
            | DomainAlreadyDeleted

        let nameValidation name =
            if String.IsNullOrWhiteSpace name then Error EmptyName else Ok name

        let identify =
            function
            | CreateDomain (domainId, _) -> domainId
            | CreateSubdomain (domainId, _, _) -> domainId
            | RenameDomain (domainId, _) -> domainId
            | RefineVision (domainId, _) -> domainId
            | AssignKey (domainId, _) -> domainId
            | MoveDomain (domainId, _) -> domainId
            | RemoveDomain (domainId) -> domainId

        let name id = id

        type State =
            | Initial
            | Existing of name: string
            | Deleted
            static member Fold (state: State) (event: Event) =
                match event with
                | DomainRemoved _ -> Deleted
                | DomainImported n -> Existing n.Name
                | DomainCreated n -> Existing n.Name
                | SubDomainCreated n -> Existing n.Name
                | _ -> state

        let newDomain id name parentDomain =
            name
            |> nameValidation
            |> Result.map (fun name ->
                match parentDomain with
                | Some parent ->
                    SubDomainCreated
                        { DomainId = id
                          ParentDomainId = parent
                          Name = name }
                | None -> DomainCreated { DomainId = id; Name = name })

        let moveDomain parent domainId =
            match parent with
            | Some parentDomain ->
                CategorizedAsSubdomain
                    { DomainId = domainId
                      ParentDomainId = parentDomain }
            | None -> PromotedToDomain { DomainId = domainId }
            |> Ok

        let refineVisionOfDomain vision domainId =
            VisionRefined
                { DomainId = domainId
                  Vision =
                      vision
                      |> Option.ofObj
                      |> Option.filter (String.IsNullOrWhiteSpace >> not) }
            |> Ok

        let renameDomain potentialName domainId state =
            match state with
            | Existing name ->
                potentialName
                |> nameValidation
                |> Result.map (fun name ->
                    DomainRenamed {
                        DomainId = domainId
                        Name = name
                        OldName = name
                    })
            | _ ->
                Error DomainAlreadyDeleted 

        let assignKeyToDomain key domainId =
            KeyAssigned
                { DomainId = domainId
                  Key =
                      key
                      |> Option.ofObj
                      |> Option.filter (String.IsNullOrWhiteSpace >> not) }
            |> Ok

        let handle (state: State) (command: Command) =
            match command with
            | CreateDomain (domainId, createDomain) -> newDomain domainId createDomain.Name None
            | CreateSubdomain (domainId, subdomainId, createDomain) ->
                newDomain domainId createDomain.Name (Some subdomainId)
            | RemoveDomain domainId -> Ok <| DomainRemoved { DomainId = domainId }
            | MoveDomain (domainId, move) -> moveDomain move.ParentDomainId domainId
            | RenameDomain (domainId, rename) -> renameDomain rename.Name domainId state
            | RefineVision (domainId, refineVision) -> refineVisionOfDomain refineVision.Vision domainId
            | AssignKey (domainId, assignKey) -> assignKeyToDomain assignKey.Key domainId
            |> Result.map List.singleton

        module Projections =
            let asDomain domain event =
                match event with
                | DomainImported c ->
                    Some
                        { Id = c.DomainId
                          Vision = c.Vision
                          ParentDomainId = c.ParentDomainId
                          Key = c.Key
                          Name = c.Name }
                | DomainCreated c ->
                    Some
                        { Id = c.DomainId
                          Vision = None
                          ParentDomainId = None
                          Name = c.Name
                          Key = None }
                | SubDomainCreated c ->
                    Some
                        { Id = c.DomainId
                          Vision = None
                          ParentDomainId = Some c.ParentDomainId
                          Name = c.Name
                          Key = None }
                | CategorizedAsSubdomain c ->
                    domain
                    |> Option.map (fun o ->
                        { o with
                              ParentDomainId = Some c.ParentDomainId })
                | PromotedToDomain c ->
                    domain
                    |> Option.map (fun o -> { o with ParentDomainId = None })
                | DomainRemoved _ -> None
                | DomainRenamed c ->
                    domain
                    |> Option.map (fun o -> { o with Name = c.Name })
                | VisionRefined c ->
                    domain
                    |> Option.map (fun o -> { o with Vision = c.Vision })
                | KeyAssigned c ->
                    domain
                    |> Option.map (fun o -> { o with Key = c.Key })

    module BoundedContext =
        open Entities

        type Command =
            | CreateBoundedContext of BoundedContextId * DomainId * CreateBoundedContext
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

        type Event =
            | BoundedContextImported of BoundedContextImported
            | BoundedContextCreated of BoundedContextCreated
            | BoundedContextRenamed of BoundedContextRenamed
            | KeyAssigned of KeyAssigned
            | BoundedContextRemoved of BoundedContextRemoved
            | BoundedContextMovedToDomain of BoundedContextMovedToDomain
            | BoundedContextReclassified of BoundedContextReclassified
            | DescriptionChanged of DescriptionChanged
            // TODO: replace with add/remove instead of updateing all
            | BusinessDecisionsUpdated of BusinessDecisionsUpdated
            | UbiquitousLanguageUpdated of UbiquitousLanguageUpdated
            | DomainRolesUpdated of DomainRolesUpdated
            | MessagesUpdated of MessagesUpdated

        and BoundedContextImported =
            { BoundedContextId: BoundedContextId
              DomainId: DomainId
              Key: string option
              Name: string
              Description: string option
              Classification: StrategicClassification
              BusinessDecisions: BusinessDecision list
              UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
              Messages: Messages
              DomainRoles: DomainRole list }

        and BoundedContextCreated =
            { BoundedContextId: BoundedContextId
              DomainId: DomainId
              Name: string }

        and BoundedContextRenamed =
            { BoundedContextId: BoundedContextId
              Name: string }

        and BoundedContextRemoved = { BoundedContextId: BoundedContextId }

        and BoundedContextMovedToDomain =
            { BoundedContextId: BoundedContextId
              DomainId: DomainId }

        and DescriptionChanged =
            { BoundedContextId: BoundedContextId
              Description: string option }

        and KeyAssigned =
            { BoundedContextId: BoundedContextId
              Key: string option }

        and BoundedContextReclassified =
            { BoundedContextId: BoundedContextId
              Classification: StrategicClassification }

        and BusinessDecisionsUpdated =
            { BoundedContextId: BoundedContextId
              BusinessDecisions: BusinessDecision list }

        and UbiquitousLanguageUpdated =
            { BoundedContextId: BoundedContextId
              UbiquitousLanguage: Map<string, UbiquitousLanguageTerm> }

        and DomainRolesUpdated =
            { BoundedContextId: BoundedContextId
              DomainRoles: DomainRole list }

        and MessagesUpdated =
            { BoundedContextId: BoundedContextId
              Messages: Messages }

        type Errors = | EmptyName

        let identify =
            function
            | CreateBoundedContext (contextId, _, _) -> contextId
            | RenameBoundedContext (contextId, _) -> contextId
            | ChangeDescription (contextId, _) -> contextId
            | RemoveBoundedContext contextId -> contextId
            | UpdateDomainRoles (contextId, _) -> contextId
            | UpdateUbiquitousLanguage (contextId, _) -> contextId
            | UpdateMessages (contextId, _) -> contextId
            | UpdateBusinessDecisions (contextId, _) -> contextId
            | ReclassifyBoundedContext (contextId, _) -> contextId
            | AssignKey (contextId, _) -> contextId
            | MoveBoundedContextToDomain (contextId, _) -> contextId

        let name identity = identity

        type State =
            | Initial
            | Existing
            | Deleted
            static member Fold (state: State) (event: Event) =
                match event with
                | BoundedContextRemoved _ -> Deleted
                | _ -> Existing

        let nameValidation name =
            if String.IsNullOrWhiteSpace name then Error EmptyName else Ok name

        let newBoundedContext id domainId name =
            name
            |> nameValidation
            |> Result.map (fun name ->
                BoundedContextCreated
                    { BoundedContextId = id
                      DomainId = domainId
                      Name = name })

        let renameBoundedContext potentialName boundedContextId =
            potentialName
            |> nameValidation
            |> Result.map (fun name ->
                BoundedContextRenamed
                    { Name = name
                      BoundedContextId = boundedContextId })

        let assignKeyToBoundedContext key boundedContextId =
            KeyAssigned
                { BoundedContextId = boundedContextId
                  Key =
                      key
                      |> Option.ofObj
                      |> Option.filter (String.IsNullOrWhiteSpace >> not) }
            |> Ok


        let handle state (command: Command) =
            match command with
            | CreateBoundedContext (id, domainId, createBc) -> newBoundedContext id domainId createBc.Name
            | RenameBoundedContext (contextId, rename) -> renameBoundedContext rename.Name contextId
            | AssignKey (contextId, key) -> assignKeyToBoundedContext key.Key contextId
            | RemoveBoundedContext contextId ->
                BoundedContextRemoved { BoundedContextId = contextId }
                |> Ok
            | MoveBoundedContextToDomain (contextId, move) ->
                BoundedContextMovedToDomain
                    { DomainId = move.ParentDomainId
                      BoundedContextId = contextId }
                |> Ok
            | ReclassifyBoundedContext (contextId, classification) ->
                BoundedContextReclassified
                    { Classification = classification.Classification
                      BoundedContextId = contextId }
                |> Ok
            | ChangeDescription (contextId, descriptionText) ->
                DescriptionChanged
                    { Description = descriptionText.Description
                      BoundedContextId = contextId }
                |> Ok
            | UpdateBusinessDecisions (contextId, decisions) ->
                BusinessDecisionsUpdated
                    { BusinessDecisions = decisions.BusinessDecisions
                      BoundedContextId = contextId }
                |> Ok
            | UpdateUbiquitousLanguage (contextId, language) ->
                UbiquitousLanguageUpdated
                    { UbiquitousLanguage = language.UbiquitousLanguage
                      BoundedContextId = contextId }
                |> Ok
            | UpdateDomainRoles (contextId, roles) ->
                DomainRolesUpdated
                    { DomainRoles = roles.DomainRoles
                      BoundedContextId = contextId }
                |> Ok
            | UpdateMessages (contextId, roles) ->
                MessagesUpdated
                    { Messages = roles.Messages
                      BoundedContextId = contextId }
                |> Ok
            |> Result.map List.singleton

        module Projections =
            let asBoundedContext state event =
                match event with
                | BoundedContextImported c ->
                    match state with
                    | Some s ->
                        Some
                            { s with
                                  Id = c.BoundedContextId
                                  DomainId = c.DomainId
                                  Description = c.Description
                                  Messages = c.Messages
                                  Classification = c.Classification
                                  DomainRoles = c.DomainRoles
                                  UbiquitousLanguage = c.UbiquitousLanguage
                                  BusinessDecisions = c.BusinessDecisions
                                  Key = c.Key
                                  Name = c.Name }
                    | None ->
                        Some
                            { Id = c.BoundedContextId
                              DomainId = c.DomainId
                              Description = c.Description
                              Messages = c.Messages
                              Classification = c.Classification
                              DomainRoles = c.DomainRoles
                              UbiquitousLanguage = c.UbiquitousLanguage
                              BusinessDecisions = c.BusinessDecisions
                              Key = c.Key
                              Name = c.Name
                              Namespaces = [] }
                | BoundedContextCreated c ->
                    Some
                        { Id = c.BoundedContextId
                          DomainId = c.DomainId
                          Description = None
                          Name = c.Name
                          Key = None
                          Messages = Messages.Empty
                          Classification = StrategicClassification.Unknown
                          DomainRoles = []
                          BusinessDecisions = []
                          UbiquitousLanguage = Map.empty
                          Namespaces = [] }
                | BoundedContextRemoved c -> None
                | BoundedContextRenamed c ->
                    state
                    |> Option.map (fun o -> { o with Name = c.Name })
                | BoundedContextMovedToDomain c ->
                    state
                    |> Option.map (fun o -> { o with DomainId = c.DomainId })
                | BoundedContextReclassified c ->
                    state
                    |> Option.map (fun o ->
                        { o with
                              Classification = c.Classification })
                | BusinessDecisionsUpdated c ->
                    state
                    |> Option.map (fun o ->
                        { o with
                              BusinessDecisions = c.BusinessDecisions })
                | DomainRolesUpdated c ->
                    state
                    |> Option.map (fun o -> { o with DomainRoles = c.DomainRoles })
                | MessagesUpdated c ->
                    state
                    |> Option.map (fun o -> { o with Messages = c.Messages })
                | DescriptionChanged c ->
                    state
                    |> Option.map (fun o -> { o with Description = c.Description })
                | KeyAssigned c ->
                    state
                    |> Option.map (fun o -> { o with Key = c.Key })
                | UbiquitousLanguageUpdated c ->
                    state
                    |> Option.map (fun o ->
                        { o with
                              UbiquitousLanguage = c.UbiquitousLanguage })

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
                | ConnectionRemoved _ -> Deleted
                | _ -> Existing

        let identify =
            function
            | DefineInboundConnection (collaborationId, _) -> collaborationId
            | DefineOutboundConnection (collaborationId, _) -> collaborationId
            | DefineRelationship (collaborationId, _) -> collaborationId
            | RemoveConnection (collaborationId) -> collaborationId

        let name identity = identity

        let handle (state: State) (command: Command) =
            match state, command with
            | Existing, DefineRelationship (collaborationId, relationship) ->
                match relationship.RelationshipType with
                | Some r ->
                    Ok [ RelationshipDefined
                             { CollaborationId = collaborationId
                               RelationshipType = r } ]
                | None -> Ok [ RelationshipUnknown { CollaborationId = collaborationId } ]
            | Initial, DefineInboundConnection (collaborationId, connection) ->
                Ok [ CollaboratorsConnected
                         { CollaborationId = collaborationId
                           Description = connection.Description
                           Initiator = connection.Initiator
                           Recipient = connection.Recipient } ]
            | Initial, DefineOutboundConnection (collaborationId, connection) ->
                Ok [ CollaboratorsConnected
                         { CollaborationId = collaborationId
                           Description = connection.Description
                           Initiator = connection.Initiator
                           Recipient = connection.Recipient } ]
            | _, RemoveConnection collaborationId -> Ok [ ConnectionRemoved { CollaborationId = collaborationId } ]
            | _, _ -> Ok []

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

    module Namespace =
        open Entities

        type Errors =
            | EmptyName
            | NamespaceNameNotUnique

        type Command =
            | NewNamespace of BoundedContextId * NamespaceDefinition
            | RemoveNamespace of BoundedContextId * NamespaceId
            | RemoveLabel of BoundedContextId * RemoveLabel
            | AddLabel of BoundedContextId * NamespaceId * NewLabelDefinition

        and NamespaceDefinition =
            { Name: string
              Template: NamespaceTemplateId option
              Labels: NewLabelDefinition list }

        and NewLabelDefinition = { Name: string; Value: string; Template: TemplateLabelId option }

        and RemoveLabel =
            { Namespace: NamespaceId
              Label: LabelId }

        type Event =
            | NamespaceImported of NamespaceImported
            | NamespaceAdded of NamespaceAdded
            | NamespaceRemoved of NamespaceRemoved
            | LabelRemoved of LabelRemoved
            | LabelAdded of LabelAdded

        and NamespaceImported =
            { NamespaceId: NamespaceId
              BoundedContextId: BoundedContextId
              NamespaceTemplateId: NamespaceTemplateId option
              Name: string
              Labels: LabelDefinition list }

        and NamespaceAdded =
            { NamespaceId: NamespaceId
              BoundedContextId: BoundedContextId
              NamespaceTemplateId: NamespaceTemplateId option
              Name: string
              Labels: LabelDefinition list }

        and NamespaceRemoved = { NamespaceId: NamespaceId }

        and LabelDefinition =
            { LabelId: LabelId
              Name: string
              Value: string option
              Template: TemplateLabelId option }

        and LabelRemoved =
            { NamespaceId: NamespaceId
              LabelId: LabelId }

        and LabelAdded =
            { LabelId: LabelId
              NamespaceId: NamespaceId
              Name: string
              Value: string option }

        type State =
            | Namespaces of Map<NamespaceId, string>
            static member Initial = Namespaces Map.empty

            static member Fold (Namespaces namespaces) (event: Event) =
                match event with
                | NamespaceRemoved e ->
                    namespaces
                    |> Map.remove e.NamespaceId
                    |> Namespaces
                | NamespaceAdded e ->
                    namespaces
                    |> Map.add e.NamespaceId e.Name
                    |> Namespaces
                | NamespaceImported e ->
                    namespaces
                    |> Map.add e.NamespaceId e.Name
                    |> Namespaces
                | _ -> Namespaces namespaces

        module LabelDefinition =
            let create name (value: string) template: LabelDefinition option =
                if String.IsNullOrWhiteSpace name then
                    None
                else
                    Some
                        { LabelId = Guid.NewGuid()
                          Name = name.Trim()
                          Value = if not (isNull value) then value.Trim() |> Some else None
                          Template = template }

        let addNewNamespace boundedContextId name templateId (labels: NewLabelDefinition list) (Namespaces namespaces) =
            if namespaces
               |> Map.exists (fun _ existingName -> String.Equals (existingName, name,StringComparison.OrdinalIgnoreCase)) then
                Error NamespaceNameNotUnique
            else
                let newLabels =
                    labels
                    |> List.choose (fun label -> LabelDefinition.create label.Name label.Value label.Template)

                let newNamespace =
                    NamespaceAdded
                        { NamespaceId = Guid.NewGuid()
                          BoundedContextId = boundedContextId
                          NamespaceTemplateId = templateId
                          Name = name
                          Labels = newLabels }

                Ok newNamespace

        let addLabel namespaceId labelName value =
            match LabelDefinition.create labelName value None with
            | Some label ->
                Ok
                <| LabelAdded
                    { NamespaceId = namespaceId
                      Name = label.Name
                      Value = label.Value
                      LabelId = label.LabelId }
            | None -> Error EmptyName

        let identify =
            function
            | NewNamespace (boundedContextId, _) -> boundedContextId
            | RemoveNamespace (boundedContextId, _) -> boundedContextId
            | AddLabel (boundedContextId, _, _) -> boundedContextId
            | RemoveLabel (boundedContextId, _) -> boundedContextId

        let name identity = identity

        let handle (state: State) (command: Command) =
            match command with
            | NewNamespace (boundedContextId, namespaceCommand) ->
                addNewNamespace boundedContextId namespaceCommand.Name namespaceCommand.Template namespaceCommand.Labels state
            | RemoveNamespace (_, namespaceId) ->
                Ok
                <| NamespaceRemoved { NamespaceId = namespaceId }
            | AddLabel (_, namespaceId, labelCommand) -> addLabel namespaceId labelCommand.Name labelCommand.Value
            | RemoveLabel (_, labelCommand) ->
                Ok
                <| LabelRemoved
                    { NamespaceId = labelCommand.Namespace
                      LabelId = labelCommand.Label }
            |> Result.map List.singleton


        module Projections =
            let convertLabels (labels: LabelDefinition list): Label list =
                labels
                |> List.map (fun l ->
                    { Name = l.Name
                      Id = l.LabelId
                      Value = l.Value |> Option.defaultValue null
                      Template = l.Template })
                
            let asNamespace namespaceOption event =
                match event with
                | NamespaceImported c ->
                    Some {
                      Id = c.NamespaceId
                      Template = c.NamespaceTemplateId
                      Name = c.Name
                      Labels = c.Labels |> convertLabels }
                | NamespaceAdded c ->
                    Some {
                      Id = c.NamespaceId
                      Template = None
                      Name = c.Name
                      Labels = c.Labels |> convertLabels }
                | NamespaceRemoved c ->
                    None
                | LabelAdded c ->
                    namespaceOption
                    |> Option.map (fun n ->
                        { n with
                              Labels =
                                  { Id = c.LabelId
                                    Name = c.Name
                                    Value = c.Value |> Option.defaultValue null
                                    Template = None }
                                  :: n.Labels }
                    )
                | LabelRemoved c ->
                    namespaceOption
                    |> Option.map (fun n ->
                        { n with
                              Labels =
                                  n.Labels
                                  |> List.filter (fun l -> l.Id <> c.LabelId) }
                    )

            let asNamespaces namespaces event =
                match event with
                | NamespaceImported c ->
                    { Id = c.NamespaceId
                      Template = c.NamespaceTemplateId
                      Name = c.Name
                      Labels = c.Labels |> convertLabels }
                    :: namespaces
                | NamespaceAdded c ->
                    { Id = c.NamespaceId
                      Template = None
                      Name = c.Name
                      Labels = c.Labels |> convertLabels }
                    :: namespaces
                | NamespaceRemoved c ->
                    namespaces
                    |> List.filter (fun n -> n.Id <> c.NamespaceId)
                | LabelAdded c ->
                    namespaces
                    |> List.map (fun n ->
                        if n.Id = c.NamespaceId then
                            { n with
                                  Labels =
                                      { Id = c.LabelId
                                        Name = c.Name
                                        Value = c.Value |> Option.defaultValue null
                                        Template = None }
                                      :: n.Labels }
                        else
                            n)
                | LabelRemoved c ->
                    namespaces
                    |> List.map (fun n ->
                        if n.Id = c.NamespaceId then
                            { n with
                                  Labels =
                                      n.Labels
                                      |> List.filter (fun l -> l.Id <> c.LabelId) }
                        else
                            n)
                    
                    
            let asNamespaceWithBoundedContext boundedContextOption event =
                boundedContextOption
                |> Option.map (fun boundedContext ->
                    { boundedContext with Namespaces = asNamespaces boundedContext.Namespaces event })

    module NamespaceTemplate =
        open Entities
        
        type Errors =
            | EmptyName
            | NamespaceNameNotUnique

        type Command =
            | NewNamespaceTemplate of NamespaceTemplateId * NamespaceDefinition
            | RemoveTemplate of NamespaceTemplateId
            | RemoveTemplateLabel of NamespaceTemplateId * RemoveLabel
            | AddTemplateLabel of NamespaceTemplateId * AddTemplateLabel

        and NamespaceDefinition =
            { Name: string
              Description: string option
              Labels: AddTemplateLabel list }

        and AddTemplateLabel= { Name: string; Description: string; Placeholder: string }

        and RemoveLabel =
            { Label: TemplateLabelId }

        type Event =
            | NamespaceTemplateImported of NamespaceTemplateImported
            | NamespaceTemplateAdded of NamespaceTemplatedAdded
            | NamespaceTemplateRemoved of NamespaceTemplateRemoved
            | TemplateLabelRemoved of TemplateLabelRemoved
            | TemplateLabelAdded of TemplateLabelAdded

        and NamespaceTemplateImported =
            { NamespaceTemplateId: NamespaceTemplateId
              Name: string
              Description: string option
              Labels: TemplateLabelDefinition list }

        and NamespaceTemplatedAdded =
            { NamespaceTemplateId: NamespaceTemplateId
              Name: string
              Description: string option
              Labels: TemplateLabelDefinition list }

        and NamespaceTemplateRemoved = { NamespaceTemplateId: NamespaceTemplateId }

        and TemplateLabelDefinition =
            { TemplateLabelId: TemplateLabelId
              Name: string
              Description: string option
              Placeholder: string option }

        and TemplateLabelRemoved =
            { NamespaceTemplateId: NamespaceTemplateId
              TemplateLabelId: TemplateLabelId }

        and TemplateLabelAdded =
            { TemplateLabelId: TemplateLabelId
              NamespaceTemplateId: NamespaceTemplateId
              Name: string
              Description: string option
              Placeholder: string option }

        type State =
            | Templates of Map<NamespaceTemplateId, string>
            static member Initial = Templates Map.empty

            static member Fold (Templates templates) (event: Event) =
                match event with
                | NamespaceTemplateRemoved e ->
                    templates
                    |> Map.remove e.NamespaceTemplateId
                    |> Templates
                | NamespaceTemplateAdded e ->
                    templates
                    |> Map.add e.NamespaceTemplateId e.Name
                    |> Templates
                | NamespaceTemplateImported e ->
                    templates
                    |> Map.add e.NamespaceTemplateId e.Name
                    |> Templates
                | _ -> Templates templates

        module TemplateLabelDefinition =
            let create name (description: string) (placeholder: string): TemplateLabelDefinition option =
                if String.IsNullOrWhiteSpace name then
                    None
                else
                    let trim (v:string) = if not (isNull v) then v.Trim() |> Some else None
                    Some
                        { TemplateLabelId = Guid.NewGuid()
                          Name = name.Trim()
                          Description = trim description 
                          Placeholder = trim placeholder
                        }

        let addNewTemplate id name description (labels: AddTemplateLabel list) (Templates templates) =
            if templates
               |> Map.exists (fun _ name -> name = name) then
                Error NamespaceNameNotUnique
            else
                let newLabels =
                    labels
                    |> List.choose (fun label -> TemplateLabelDefinition.create label.Name label.Description label.Placeholder)

                let newNamespace =
                    NamespaceTemplateAdded
                        { NamespaceTemplateId = id
                          Name = name
                          Description = description
                          Labels = newLabels }

                Ok newNamespace

        let addLabel namespaceId labelName description placeholder =
            match TemplateLabelDefinition.create labelName description placeholder with
            | Some label ->
                Ok <| TemplateLabelAdded
                        { NamespaceTemplateId = namespaceId
                          Name = label.Name
                          Description = label.Description
                          Placeholder = label.Placeholder
                          TemplateLabelId = label.TemplateLabelId }
            | None -> Error EmptyName

        let identify =
            function
            | NewNamespaceTemplate (id, _) -> id
            | RemoveTemplate (id) -> id
            | AddTemplateLabel(id, _) -> id
            | RemoveTemplateLabel (id, _) -> id

        let name identity = identity

        let handle (state: State) (command: Command) =
            match command with
            | NewNamespaceTemplate (id, cmd) ->
                addNewTemplate id cmd.Name cmd.Description cmd.Labels state
            | RemoveTemplate (id) ->
                Ok
                <| NamespaceTemplateRemoved { NamespaceTemplateId = id }
            | AddTemplateLabel (id, cmd) -> addLabel id cmd.Name cmd.Description cmd.Placeholder
            | RemoveTemplateLabel (id, cmd) ->
                Ok
                <| TemplateLabelRemoved
                    { TemplateLabelId = cmd.Label
                      NamespaceTemplateId = id }
            |> Result.map List.singleton
            
            
        module Projections =
            type LabelTemplate = { Name: string; Description: string; Placeholder:string; Id: TemplateLabelId }

            type NamespaceTemplate =
                { Id: NamespaceTemplateId
                  Name: string
                  Description: string
                  Template: LabelTemplate list }                
                
            let convertLabels (labels: TemplateLabelDefinition list): LabelTemplate list =
                labels
                |> List.map (fun l ->
                    { Name = l.Name
                      Id = l.TemplateLabelId
                      Description = l.Description |> Option.defaultValue null
                      Placeholder = l.Placeholder |> Option.defaultValue null })

            let asTemplate template event =
                match event with
                | NamespaceTemplateImported c ->
                    Some {
                      Id = c.NamespaceTemplateId
                      Name = c.Name
                      Description = c.Description |> Option.defaultValue null
                      Template = c.Labels |> convertLabels
                    }
                | NamespaceTemplateAdded c ->
                    Some {
                      Id = c.NamespaceTemplateId
                      Name = c.Name
                      Description = c.Description |> Option.defaultValue null
                      Template = c.Labels |> convertLabels
                    }
                | NamespaceTemplateRemoved c ->
                    None
                | TemplateLabelAdded c ->
                    template
                    |> Option.map (fun n ->
                        { n with
                              Template =
                                  { Id = c.TemplateLabelId
                                    Name = c.Name
                                    Description = c.Description |> Option.defaultValue null 
                                    Placeholder = c.Placeholder |> Option.defaultValue null 
                                  }
                                  :: n.Template }
                        )
                | TemplateLabelRemoved c ->
                    template
                    |> Option.map (fun n ->
                        { n with
                              Template =
                                  n.Template
                                  |> List.filter (fun l -> l.Id <> c.TemplateLabelId) }
                        )
                   