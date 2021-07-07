module Contexture.Api.Aggregates.NamespaceTemplate

open System
open Contexture.Api.Entities

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

    static member evolve (Templates templates) (event: Event) =
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

let decide (command: Command) (state: State) =
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
           