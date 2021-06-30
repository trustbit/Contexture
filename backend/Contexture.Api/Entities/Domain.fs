namespace Contexture.Api.Aggregates

module Domain =
    open System
    open Contexture.Api.Entities

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

    type Domain =
        { Id: DomainId
          ParentDomainId: DomainId option
          Key: string option
          Name: string
          Vision: string option }

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
                      Key = c.Key
                      Name = c.Name }
            |  Initial, DomainCreated c ->
                Existing
                    { Id = c.DomainId
                      Vision = None
                      ParentDomainId = None
                      Name = c.Name
                      Key = None }
            | Initial, SubDomainCreated c ->
                Existing
                    { Id = c.DomainId
                      Vision = None
                      ParentDomainId = Some c.ParentDomainId
                      Name = c.Name
                      Key = None }
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
            | Existing domain, KeyAssigned c ->
                Existing { domain with Key = c.Key }
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

   
