namespace Contexture.Api.Aggregates

module Domain =
    open System
    open FsToolkit.ErrorHandling

    module ValueObjects =
        type DomainId = Guid
        
    open ValueObjects
    type Command =
        | CreateDomain of DomainId * CreateDomain
        | CreateSubdomain of DomainId * subdomainOf: DomainId * CreateDomain
        | RenameDomain of DomainId * RenameDomain
        | MoveDomain of DomainId * MoveDomain
        | RefineVision of DomainId * RefineVision
        | AssignShortName of DomainId * AssignShortName
        | RemoveDomain of DomainId

    and CreateDomain = { Name: string; Vision: string option; ShortName: string option }

    and RenameDomain = { Name: string }

    and MoveDomain = { ParentDomainId: DomainId option }

    and RefineVision = { Vision: string }

    and AssignShortName = { ShortName: string }

    type Event =
        | DomainImported of DomainImported
        | DomainCreated of DomainCreated
        | SubDomainCreated of SubDomainCreated
        | DomainRenamed of DomainRenamed
        | CategorizedAsSubdomain of CategorizedAsSubdomain
        | PromotedToDomain of PromotedToDomain
        | VisionRefined of VisionRefined
        | DomainRemoved of DomainRemoved
        | ShortNameAssigned of ShortNameAssigned

    and DomainImported =
        { DomainId: DomainId
          ParentDomainId: DomainId option
          ShortName: string option
          Name: string
          Vision: string option }

    and DomainCreated = { DomainId: DomainId; Name: String; }

    and SubDomainCreated =
        { DomainId: DomainId
          ParentDomainId: DomainId
          Name: String }

    and DomainRenamed = { DomainId: DomainId; Name: String; OldName: string }

    and CategorizedAsSubdomain =
        { DomainId: DomainId
          ParentDomainId: DomainId
          OldParentDomainId: DomainId option }

    and PromotedToDomain = {
        DomainId: DomainId
        OldParentDomain: DomainId
    }

    and VisionRefined =
        { DomainId: DomainId
          Vision: String option }

    and DomainRemoved = {
        DomainId: DomainId
        OldParentDomain: DomainId option
    }

    and ShortNameAssigned =
        { DomainId: DomainId
          ShortName: string option }

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
        | AssignShortName (domainId, _) -> domainId
        | MoveDomain (domainId, _) -> domainId
        | RemoveDomain (domainId) -> domainId

    let name id = id

    type Domain =
        { Id: DomainId
          ParentDomainId: DomainId option
          ShortName: string option
          Name: string
          Vision: string option
        }

    type State =
        | Initial
        | Existing of Domain
        | Deleted
    
    module State =
        let evolve (state: State) (event: Event) =
            match state, event with
            | _, DomainRemoved _ -> Deleted
            | Initial, DomainImported c ->
                Existing
                    { Id = c.DomainId
                      Vision = c.Vision
                      ParentDomainId = c.ParentDomainId
                      ShortName = c.ShortName
                      Name = c.Name }
            |  Initial, DomainCreated c ->
                Existing
                    { Id = c.DomainId
                      Vision = None
                      ParentDomainId = None
                      Name = c.Name
                      ShortName = None
                    }
            | Initial, SubDomainCreated c ->
                Existing
                    { Id = c.DomainId
                      Vision = None
                      ParentDomainId = Some c.ParentDomainId
                      Name = c.Name
                      ShortName = None }
            | Existing domain, CategorizedAsSubdomain c ->
                Existing
                    { domain with
                        ParentDomainId = Some c.ParentDomainId }
            | Existing domain, PromotedToDomain c ->
                Existing { domain with ParentDomainId = None }
            | Existing domain, DomainRenamed c ->
                Existing { domain with Name = c.Name }
            | Existing domain, VisionRefined c ->
                Existing { domain with Vision = c.Vision }
            | Existing domain, ShortNameAssigned c ->
                Existing { domain with ShortName = c.ShortName }
            | _ -> state

    let assignShortNameToDomain shortName domainId =
        ShortNameAssigned
            { DomainId = domainId
              ShortName =
                  shortName
                  |> Option.filter (String.IsNullOrWhiteSpace >> not) }
        |> Ok

    let refineVisionOfDomain vision domainId =
        VisionRefined
            { DomainId = domainId
              Vision =
                  vision
                  |> Option.filter (String.IsNullOrWhiteSpace >> not) }
        |> Ok

    let newDomain id name shortName vision parentDomain = result {
        let! created = 
            name
            |> nameValidation
            |> Result.map (fun name ->
                match parentDomain with
                | Some parent ->
                    SubDomainCreated
                        { 
                            DomainId = id
                            ParentDomainId = parent
                            Name = name 
                        }
                | None -> DomainCreated { DomainId = id; Name = name })

        let! shortNameEvent = assignShortNameToDomain shortName id
        let! visionEvent = refineVisionOfDomain vision id
        
        return [created; shortNameEvent; visionEvent]
    }

    let moveDomain (state:State) parent domainId =
        match state with
        | Existing domain ->
            match parent with
            | Some parentDomain ->
                [ CategorizedAsSubdomain
                    { DomainId = domainId
                      ParentDomainId = parentDomain
                      OldParentDomainId = domain.ParentDomainId }
                ]
            | None ->
                match domain.ParentDomainId with
                | Some currentParent ->
                    [ PromotedToDomain { DomainId = domainId; OldParentDomain = currentParent }] 
                | None ->
                    []
            |> Ok
        | _ ->
            Ok []

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
       
    let removeDomain state domainId =
        match state with
        | Existing domain ->
            Ok [
                DomainRemoved {
                    DomainId = domainId
                    OldParentDomain = domain.ParentDomainId 
                }
            ]
        | _ -> Ok []

    let private asList item = item |> Result.map List.singleton
    let decide (command: Command) (state: State) =
        match command with
        | CreateDomain (domainId, createDomain) -> newDomain domainId createDomain.Name createDomain.ShortName createDomain.Vision None
        | CreateSubdomain (domainId, subdomainId, createDomain) ->
            newDomain domainId createDomain.Name createDomain.ShortName createDomain.Vision (Some subdomainId)
        | RemoveDomain domainId -> removeDomain state domainId
        | MoveDomain (domainId, move) -> moveDomain state move.ParentDomainId domainId
        | RenameDomain (domainId, rename) -> renameDomain rename.Name domainId state |> asList
        | RefineVision (domainId, refineVision) -> refineVisionOfDomain (refineVision.Vision |> Option.ofObj) domainId |> asList
        | AssignShortName (domainId, assignShortName) -> assignShortNameToDomain (assignShortName.ShortName |> Option.ofObj) domainId |> asList
        

   
