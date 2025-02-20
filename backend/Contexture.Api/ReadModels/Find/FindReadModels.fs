namespace Contexture.Api.ReadModels.Find
open Contexture.Api

open Contexture.Api.Infrastructure
open Contexture.Api.ReadModels
module Utils =
    
    let appendToSet items (key, value) =
        items
        |> Map.change
            key
            (function
            | Some values -> values |> Set.add value |> Some
            | None -> value |> Set.singleton |> Some)

    let removeFromSet findValue value items =
        items
        |> Map.map
            (fun _ (values: Set<_>) ->
                values
                |> Set.filter (fun n -> findValue n <> value))

    let findByPhrase phrase selector items =
        let matchesKey (key: string) =
            let term = key |> SearchTerm.fromInput

            term
            |> Option.map (SearchPhrase.matches phrase)
            |> Option.defaultValue false

        items
        |> Seq.filter (selector >> matchesKey)

 module Namespaces =
    open Contexture.Api.Aggregates.Namespace
    open Utils
    open ValueObjects

    type NamespaceModel =
        { NamespaceId: NamespaceId
          NamespaceTemplateId: NamespaceTemplateId option }

    type NamespaceFinder = Map<string, Set<NamespaceModel>>

    let private projectNamespaceNameToNamespaceId state eventEnvelope =
        match eventEnvelope.Event with
        | NamespaceAdded n ->
            appendToSet
                state
                (n.Name,
                 { NamespaceId = n.NamespaceId
                   NamespaceTemplateId = n.NamespaceTemplateId })
        | NamespaceImported n ->
            appendToSet
                state
                (n.Name,
                 { NamespaceId = n.NamespaceId
                   NamespaceTemplateId = n.NamespaceTemplateId })
        | NamespaceRemoved n ->
            state
            |> removeFromSet (fun i -> i.NamespaceId) n.NamespaceId
        | LabelAdded l -> state
        | LabelRemoved l -> state
        | LabelUpdated _ -> state

    let byName (namespaces: NamespaceFinder) (name: SearchPhrase) =
        namespaces
        |> Map.toSeq
        |> findByPhrase name fst
        |> Seq.map snd
        |> Set.unionMany
        |> SearchPhraseResult.fromManyResults

    let byTemplate (namespaces: NamespaceFinder) (templateId: NamespaceTemplateId) =
        namespaces
        |> Map.toList
        |> List.map snd
        |> Set.unionMany
        |> Set.filter (fun m -> m.NamespaceTemplateId = Some templateId)
        |> SearchPhraseResult.fromResults

    type ReadModel =
        ReadModels.ReadModel<Aggregates.Namespace.Event, NamespaceFinder>

    let readModel () : ReadModel =
        let updateState state eventEnvelopes =
            let newState =
                eventEnvelopes
                |> List.fold projectNamespaceNameToNamespaceId state

            newState

        ReadModels.readModel updateState Map.empty Defaults.ReplyTimeout

module Labels =
    open Contexture.Api.Aggregates.BoundedContext
    open Contexture.Api.Aggregates.Namespace
    open Utils
    open ValueObjects

    type LabelAndNamespaceModel =
        { Value: string option
          NamespaceId: NamespaceId
          NamespaceTemplateId: NamespaceTemplateId option }

    type LabelModel = {
        Id: LabelId
        NamespaceId: NamespaceId
        BoundedContextId: BoundedContextId
        Name: string
        Value: string option
    }
    type State = {
        Namespaces: Map<NamespaceId, BoundedContextId>
        Labels: Map<LabelId, LabelModel> 
    }
    with
        static member Empty = { 
            Namespaces = Map.empty
            Labels = Map.empty
        }

    let private appendForBoundedContext boundedContext namespaces (key, value) =
        namespaces
        |> Map.change
            key
            (Option.orElse (Some Map.empty)
             >> Option.map (fun items -> appendToSet items (boundedContext, value)))

    let appendNamespaceToBoundedContext boundedContextId namespaceId value namespaces =
        namespaces
        |> Map.change
            value
            (fun contexts ->
                let namespaceToAdd = Set.singleton namespaceId
                match contexts with
                | Some contexts ->
                    contexts
                    |> Map.change
                        boundedContextId
                        (Option.map (Set.union namespaceToAdd)
                            >> Option.orElse (Some namespaceToAdd))
                    |> Some
                | None ->
                    Map.ofSeq [ (boundedContextId, namespaceToAdd) ]
                    |> Some)

    let private projectLabelNameToNamespace (state: State) eventEnvelope =
        match eventEnvelope.Event with
        | NamespaceAdded n ->
            let namespaceLabels = 
                n.Labels 
                |> Seq.map (fun l -> 
                    l.LabelId, 
                    {
                        Id = l.LabelId
                        NamespaceId = n.NamespaceId
                        BoundedContextId = n.BoundedContextId
                        Name = l.Name
                        Value = l.Value;
                    }
                )
            { state with 
                Namespaces = state.Namespaces |> Map.add n.NamespaceId n.BoundedContextId
                Labels = [state.Labels |> Map.toSeq; namespaceLabels] |> Seq.concat |> Map.ofSeq 
            }
        | NamespaceImported n ->
            let namespaceLabels = 
                    n.Labels 
                    |> Seq.map (fun l -> 
                        l.LabelId, 
                        {
                            Id = l.LabelId
                            NamespaceId = n.NamespaceId
                            BoundedContextId = n.BoundedContextId
                            Name = l.Name
                            Value = l.Value;
                        }
                    )
            { state with 
                Namespaces = state.Namespaces |> Map.add n.NamespaceId n.BoundedContextId
                Labels = [state.Labels |> Map.toSeq; namespaceLabels] |> Seq.concat |> Map.ofSeq 
            }
        | LabelAdded l ->
            { state with 
                Labels = state.Labels |> Map.add l.LabelId 
                    {
                        Id = l.LabelId
                        NamespaceId = l.NamespaceId
                        BoundedContextId = state.Namespaces |> Map.find l.NamespaceId
                        Name = l.Name
                        Value = l.Value;
                    }
            }
        | LabelRemoved l ->
            { state with 
                Labels = state.Labels |> Map.remove l.LabelId 
            }
        | LabelUpdated l->
            { state with 
                Labels = state.Labels |> Map.change l.LabelId
                    (fun _old -> Some {
                        Id = l.LabelId
                        NamespaceId = l.NamespaceId
                        BoundedContextId = state.Namespaces |> Map.find l.NamespaceId
                        Name = l.Name
                        Value = l.Value
                    }) 
            }
        | NamespaceRemoved n ->
            { state with 
                Namespaces = state.Namespaces |> Map.remove n.NamespaceId
                Labels = state.Labels |> Map.filter(fun _key value -> value.NamespaceId <> n.NamespaceId)
            }


    let byLabelName (state: State) (phrase: SearchPhrase) =
        state.Labels
        |> Map.toSeq
        |> findByPhrase phrase (fun (_, label) -> label.Name)
        |> Seq.map(fun (_, label) -> label.BoundedContextId)
        |> SearchPhraseResult.fromManyResults

    let byLabelValue (state: State) (phrase: SearchPhrase) =
        state.Labels
        |> Map.toSeq
        |> findByPhrase phrase (fun (_, label) -> label.Value |> Option.defaultValue "")
        |> Seq.map(fun (_, label) -> label.BoundedContextId)
        |> SearchPhraseResult.fromManyResults

    type ReadModel =
        ReadModels.ReadModel<Aggregates.Namespace.Event, State>

    let readModel () =
        let updateState state eventEnvelopes =
            let newState =
                eventEnvelopes
                |> List.fold projectLabelNameToNamespace state

            newState

        ReadModels.readModel updateState State.Empty Defaults.ReplyTimeout

module Domains =
    open Contexture.Api.Aggregates.Domain
    open ValueObjects
    open Utils

    type DomainByShortNameAndNameModel =
        { ByShortName: Map<string, DomainId>
          ByName: Map<string, Set<DomainId>> }
        static member Empty =
            { ByShortName = Map.empty
              ByName = Map.empty }

    let private projectToDomain state eventEnvelope =
        let addShortName canBeShortName domain byShortName =
            match canBeShortName with
            | Some shortName -> byShortName |> Map.add shortName domain
            | None -> byShortName

        let append key value items = appendToSet items (key, value)

        match eventEnvelope.Event with
        | SubDomainCreated n ->
            { state with
                  ByName = state.ByName |> append n.Name n.DomainId }
        | DomainCreated n ->
            { state with
                  ByName = state.ByName |> append n.Name n.DomainId }
        | ShortNameAssigned k ->
            { state with
                  ByShortName =
                      state.ByShortName
                      |> Map.filter (fun _ v -> v <> k.DomainId)
                      |> addShortName k.ShortName k.DomainId }
        | DomainImported n ->
            { state with
                  ByName = appendToSet state.ByName (n.Name, n.DomainId)
                  ByShortName = state.ByShortName |> addShortName n.ShortName n.DomainId }
        | DomainRenamed l ->
            { state with
                  ByName =
                      state.ByName
                      |> removeFromSet id l.DomainId
                      |> append l.Name l.DomainId }
        | DomainRemoved l ->
            { state with
                  ByName = state.ByName |> removeFromSet id l.DomainId
                  ByShortName =
                      state.ByShortName
                      |> Map.filter (fun _ v -> v <> l.DomainId) }
        | CategorizedAsSubdomain _
        | PromotedToDomain _
        | VisionRefined _ -> state

    let byName (model: DomainByShortNameAndNameModel) (phrase: SearchPhrase) =
        model.ByName
        |> Map.toSeq
        |> findByPhrase phrase fst
        |> Seq.map snd
        |> Set.unionMany
        |> SearchPhraseResult.fromManyResults

    let byShortName (model: DomainByShortNameAndNameModel) (phrase: SearchPhrase) =
        model.ByShortName
        |> Map.toSeq
        |> findByPhrase phrase fst
        |> Seq.map snd
        |> SearchPhraseResult.fromResults

    type ReadModel =
        ReadModels.ReadModel<Aggregates.Domain.Event, DomainByShortNameAndNameModel>

    let readModel () =
        let updateState state eventEnvelopes =
            let newState =
                eventEnvelopes
                |> List.fold projectToDomain state

            newState

        ReadModels.readModel updateState DomainByShortNameAndNameModel.Empty Defaults.ReplyTimeout

module BoundedContexts =
    open Contexture.Api.Aggregates.BoundedContext
    open ValueObjects
    open Utils
    
    type BoundedContextByShortNameAndNameModel =
        { ByShortName: Map<string, BoundedContextId>
          ByName: Map<string, Set<BoundedContextId>> }
        static member Empty =
            { ByShortName = Map.empty
              ByName = Map.empty }

    let private projectToBoundedContext state eventEnvelope =
        let addShortName canBeShortName domain byShortName =
            match canBeShortName with
            | Some shortName -> byShortName |> Map.add shortName domain
            | None -> byShortName

        let append key value items = appendToSet items (key, value)

        match eventEnvelope.Event with
        | BoundedContextCreated n ->
            { state with
                  ByName = state.ByName |> append n.Name n.BoundedContextId }
        | ShortNameAssigned k ->
            { state with
                  ByShortName =
                      state.ByShortName
                      |> Map.filter (fun _ v -> v <> k.BoundedContextId)
                      |> addShortName k.ShortName k.BoundedContextId }
        | BoundedContextImported n ->
            { state with
                  ByName = appendToSet state.ByName (n.Name, n.BoundedContextId)
                  ByShortName = state.ByShortName |> addShortName n.ShortName n.BoundedContextId }
        | BoundedContextRenamed l ->
            { state with
                  ByName =
                      state.ByName
                      |> removeFromSet id l.BoundedContextId
                      |> append l.Name l.BoundedContextId }
        | BoundedContextRemoved l ->
            { state with
                  ByName =
                      state.ByName
                      |> removeFromSet id l.BoundedContextId
                  ByShortName =
                      state.ByShortName
                      |> Map.filter (fun _ v -> v <> l.BoundedContextId) }
        | _ -> state

    let byName (model: BoundedContextByShortNameAndNameModel) (phrase: SearchPhrase) =
        model.ByName
        |> Map.toSeq
        |> findByPhrase phrase fst
        |> Seq.map snd
        |> Set.unionMany
        |> SearchPhraseResult.fromManyResults

    let byShortName (model: BoundedContextByShortNameAndNameModel) (phrase: SearchPhrase) =
        model.ByShortName
        |> Map.toSeq
        |> findByPhrase phrase fst
        |> Seq.map snd
        |> SearchPhraseResult.fromResults

    type ReadModel =
        ReadModels.ReadModel<Aggregates.BoundedContext.Event, BoundedContextByShortNameAndNameModel>

    let readModel () =
        let updateState state eventEnvelopes =
            let newState =
                eventEnvelopes
                |> List.fold projectToBoundedContext state

            newState

        ReadModels.readModel updateState BoundedContextByShortNameAndNameModel.Empty Defaults.ReplyTimeout
