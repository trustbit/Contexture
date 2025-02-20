module Contexture.Api.Aggregates.Namespace

open System
open Contexture.Api.Aggregates.Collaboration
open BoundedContext.ValueObjects
open FsToolkit.ErrorHandling

module ValueObjects =
    type NamespaceTemplateId = Guid
    type TemplateLabelId = Guid
    type LabelId = Guid
    type NamespaceId = Guid
    
        
open ValueObjects

type Errors =
    | EmptyName
    | NamespaceNameNotUnique

type Command =
    | NewNamespace of BoundedContextId * NamespaceDefinition
    | RemoveNamespace of BoundedContextId * NamespaceId
    | RemoveLabel of BoundedContextId * RemoveLabel
    | AddLabel of BoundedContextId * NamespaceId * NewLabelDefinition
    | UpdateLabel of BoundedContextId * UpdateLabel

and NamespaceDefinition =
    { Name: string
      Template: NamespaceTemplateId option
      Labels: NewLabelDefinition list }

and NewLabelDefinition = { Name: string; Value: string; Template: TemplateLabelId option }

and RemoveLabel =
    { Namespace: NamespaceId
      Label: LabelId }
and UpdateLabel = {
    Label: LabelId
    Namespace: NamespaceId
    Name: string
    Value: string
}

type Event =
    | NamespaceImported of NamespaceImported
    | NamespaceAdded of NamespaceAdded
    | NamespaceRemoved of NamespaceRemoved
    | LabelRemoved of LabelRemoved
    | LabelAdded of LabelAdded
    | LabelUpdated of LabelUpdated

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

and NamespaceRemoved = {
    NamespaceId: NamespaceId
    BoundedContextId: BoundedContextId
}

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

and LabelUpdated = 
    { LabelId: LabelId
      NamespaceId: NamespaceId
      Name: string
      Value: string option }

type State = {
    Namespaces : Map<NamespaceId, string>
    Labels : Set<LabelId>
}
with
    static member Initial = {
        Namespaces = Map.empty
        Labels = Set.empty
    }

    static member evolve state (event: Event) =
        match event with
        | NamespaceRemoved e ->
            {state with Namespaces = state.Namespaces |> Map.remove e.NamespaceId }
        | NamespaceAdded e ->
            {state with Namespaces = state.Namespaces |> Map.add e.NamespaceId e.Name}
        | NamespaceImported e ->
            {state with Namespaces = state.Namespaces |> Map.add e.NamespaceId e.Name }
        | LabelAdded e ->
            {state with Labels = state.Labels |> Set.add e.LabelId }
        | LabelRemoved e ->
            {state with Labels = state.Labels |> Set.remove e.LabelId }
        | _ -> state
    

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

let addNewNamespace boundedContextId name templateId (labels: NewLabelDefinition list) state =
    if state.Namespaces
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

let updateLabel (cmd: UpdateLabel) =
    LabelDefinition.create cmd.Name cmd.Value None
    |> Result.requireSome EmptyName
    |> Result.map(fun label ->
        LabelUpdated { 
            Name = label.Name
            Value = label.Value
            LabelId = cmd.Label
            NamespaceId = cmd.Namespace
        }
    )

let identify =
    function
    | NewNamespace (boundedContextId, _) -> boundedContextId
    | RemoveNamespace (boundedContextId, _) -> boundedContextId
    | AddLabel (boundedContextId, _, _) -> boundedContextId
    | RemoveLabel (boundedContextId, _) -> boundedContextId
    | UpdateLabel (boundedContextId, _) -> boundedContextId

let name identity = identity

let decide (command: Command) (state: State) =
    match command with
    | NewNamespace (boundedContextId, namespaceCommand) ->
        addNewNamespace boundedContextId namespaceCommand.Name namespaceCommand.Template namespaceCommand.Labels state
    | RemoveNamespace (boundedContextId, namespaceId) ->
        Ok
        <| NamespaceRemoved {
            NamespaceId = namespaceId
            BoundedContextId = boundedContextId
        }
    | AddLabel (_, namespaceId, labelCommand) -> addLabel namespaceId labelCommand.Name labelCommand.Value
    | RemoveLabel (_, labelCommand) ->
        Ok
        <| LabelRemoved
            { NamespaceId = labelCommand.Namespace
              LabelId = labelCommand.Label }
    | UpdateLabel (_, labelCommand) -> updateLabel labelCommand
    |> Result.map List.singleton

module Projections =
    type Label =
        { Id: LabelId
          Name: string
          Value: string
          Template: TemplateLabelId option }
    type Namespace =
        { Id: NamespaceId
          Template: NamespaceTemplateId option
          Name: string
          Labels: Label list }
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
              Template = c.NamespaceTemplateId
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
        | LabelUpdated c ->
            namespaceOption
            |> Option.map(fun n ->
                {n with Labels = n.Labels |> List.map(fun label ->
                    if label.Id = c.LabelId then
                        {label with Name = c.Name; Value = c.Value |> Option.defaultValue null}
                    else 
                        label
                    )
                }
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
              Template = c.NamespaceTemplateId
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
        | LabelUpdated c ->
            namespaces
            |> List.map(fun n ->
                if n.Id = c.NamespaceId then
                    {n with Labels = n.Labels |> List.map(fun label ->
                        if label.Id = c.LabelId then
                            {label with Name = c.Name; Value = c.Value |> Option.defaultValue null}
                        else 
                            label
                        )
                    }
                else
                    n)
            