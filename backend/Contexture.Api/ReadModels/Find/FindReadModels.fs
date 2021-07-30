namespace Contexture.Api.ReadModels.Find
open Contexture.Api

open Contexture.Api.Infrastructure
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

    let findByKey keyPhrase items =
        let matchesKey (key: string) =
            let term = key |> SearchTerm.fromInput

            term
            |> Option.map (SearchPhrase.matches keyPhrase)
            |> Option.defaultValue false

        items
        |> Map.filter (fun k _ -> matchesKey k)
        |> Map.toList
        |> List.map snd

    let selectResults selectResult items =
        items
        |> List.map (Map.toList >> List.map selectResult >> Set.ofList)



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

    let byName (namespaces: NamespaceFinder) (name: SearchPhrase) =
        namespaces
        |> findByKey name
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

        ReadModels.readModel updateState Map.empty

module Labels =
    open Contexture.Api.Aggregates.BoundedContext
    open Contexture.Api.Aggregates.Namespace
    open Utils
    open ValueObjects

    type LabelAndNamespaceModel =
        { Value: string option
          NamespaceId: NamespaceId
          NamespaceTemplateId: NamespaceTemplateId option }

    type NamespacesByLabel =
        { Namespaces: Map<NamespaceId, BoundedContextId>
          ByLabelName: Map<string, NamespacesOfBoundedContext>
          ByLabelValue: Map<string, NamespacesOfBoundedContext> }
        static member Empty =
            { Namespaces = Map.empty
              ByLabelName = Map.empty
              ByLabelValue = Map.empty }

    and NamespacesOfBoundedContext = Map<BoundedContextId, Set<NamespaceId>>

    let private appendForBoundedContext boundedContext namespaces (key, value) =
        namespaces
        |> Map.change
            key
            (Option.orElse (Some Map.empty)
             >> Option.map (fun items -> appendToSet items (boundedContext, value)))

    let private projectLabelNameToNamespace state eventEnvelope =
        match eventEnvelope.Event with
        | NamespaceAdded n ->
            { state with
                  Namespaces =
                      state.Namespaces
                      |> Map.add n.NamespaceId n.BoundedContextId
                  ByLabelName =
                      n.Labels
                      |> List.map (fun l -> l.Name, n.NamespaceId)
                      |> List.fold (appendForBoundedContext n.BoundedContextId) state.ByLabelName
                  ByLabelValue =
                      n.Labels
                      |> List.choose
                          (fun l ->
                              l.Value
                              |> Option.map (fun value -> value, n.NamespaceId))
                      |> List.fold (appendForBoundedContext n.BoundedContextId) state.ByLabelValue }
        | NamespaceImported n ->
            { state with
                  Namespaces =
                      state.Namespaces
                      |> Map.add n.NamespaceId n.BoundedContextId
                  ByLabelName =
                      n.Labels
                      |> List.map (fun l -> l.Name, n.NamespaceId)
                      |> List.fold (appendForBoundedContext n.BoundedContextId) state.ByLabelName
                  ByLabelValue =
                      n.Labels
                      |> List.choose
                          (fun l ->
                              l.Value
                              |> Option.map (fun value -> value, n.NamespaceId))
                      |> List.fold (appendForBoundedContext n.BoundedContextId) state.ByLabelValue }
        | LabelAdded l ->
            let appendNamespaceToBoundedContext value namespaces =
                let associatedBoundedContextId =
                    state.Namespaces |> Map.find l.NamespaceId

                namespaces
                |> Map.change
                    value
                    (fun contexts ->
                        let namespaceToAdd = Set.singleton l.NamespaceId

                        match contexts with
                        | Some contexts ->
                            contexts
                            |> Map.change
                                associatedBoundedContextId
                                (Option.map (Set.union namespaceToAdd)
                                 >> Option.orElse (Some namespaceToAdd))
                            |> Some
                        | None ->
                            Map.ofSeq [ (associatedBoundedContextId, namespaceToAdd) ]
                            |> Some)

            { state with
                  ByLabelName = appendNamespaceToBoundedContext l.Name state.ByLabelName
                  ByLabelValue =
                      match l.Value with
                      | Some value -> appendNamespaceToBoundedContext value state.ByLabelValue
                      | None -> state.ByLabelValue }
        | LabelRemoved l ->
            { state with
                  ByLabelName =
                      state.ByLabelName
                      |> Map.map (fun _ values -> values |> removeFromSet id l.NamespaceId)
                  ByLabelValue =
                      state.ByLabelValue
                      |> Map.map (fun _ values -> values |> removeFromSet id l.NamespaceId) }
        | NamespaceRemoved n ->
            { state with
                  ByLabelName =
                      state.ByLabelName
                      |> Map.map (fun _ values -> values |> removeFromSet id n.NamespaceId)
                  ByLabelValue =
                      state.ByLabelValue
                      |> Map.map (fun _ values -> values |> removeFromSet id n.NamespaceId) }

    let byLabelName (namespaces: NamespacesByLabel) (phrase: SearchPhrase) =
        namespaces.ByLabelName
        |> findByKey phrase
        |> selectResults fst
        |> SearchPhraseResult.fromManyResults

    let byLabelValue (namespaces: NamespacesByLabel) (phrase: SearchPhrase) =
        namespaces.ByLabelValue
        |> findByKey phrase
        |> selectResults fst
        |> SearchPhraseResult.fromManyResults

    type ReadModel =
        ReadModels.ReadModel<Aggregates.Namespace.Event, NamespacesByLabel>

    let readModel () =
        let updateState state eventEnvelopes =
            let newState =
                eventEnvelopes
                |> List.fold projectLabelNameToNamespace state

            newState

        ReadModels.readModel updateState NamespacesByLabel.Empty

module Domains =
    open Contexture.Api.Aggregates.Domain
    open ValueObjects
    open Utils

    type DomainByKeyAndNameModel =
        { ByKey: Map<string, DomainId>
          ByName: Map<string, Set<DomainId>> }
        static member Empty =
            { ByKey = Map.empty
              ByName = Map.empty }

    let private projectToDomain state eventEnvelope =
        let addKey canBeKey domain byKey =
            match canBeKey with
            | Some key -> byKey |> Map.add key domain
            | None -> byKey

        let append key value items = appendToSet items (key, value)

        match eventEnvelope.Event with
        | SubDomainCreated n ->
            { state with
                  ByName = state.ByName |> append n.Name n.DomainId }
        | DomainCreated n ->
            { state with
                  ByName = state.ByName |> append n.Name n.DomainId }
        | KeyAssigned k ->
            { state with
                  ByKey =
                      state.ByKey
                      |> Map.filter (fun _ v -> v <> k.DomainId)
                      |> addKey k.Key k.DomainId }
        | DomainImported n ->
            { state with
                  ByName = appendToSet state.ByName (n.Name, n.DomainId)
                  ByKey = state.ByKey |> addKey n.Key n.DomainId }
        | DomainRenamed l ->
            { state with
                  ByName =
                      state.ByName
                      |> removeFromSet id l.DomainId
                      |> append l.Name l.DomainId }
        | DomainRemoved l ->
            { state with
                  ByName = state.ByName |> removeFromSet id l.DomainId
                  ByKey =
                      state.ByKey
                      |> Map.filter (fun _ v -> v <> l.DomainId) }
        | CategorizedAsSubdomain _
        | PromotedToDomain _
        | VisionRefined _ -> state

    let byName (model: DomainByKeyAndNameModel) (phrase: SearchPhrase) =
        model.ByName
        |> findByKey phrase
        |> SearchPhraseResult.fromManyResults

    let byKey (model: DomainByKeyAndNameModel) (phrase: SearchPhrase) =
        model.ByKey
        |> findByKey phrase
        |> SearchPhraseResult.fromResults

    type ReadModel =
        ReadModels.ReadModel<Aggregates.Domain.Event, DomainByKeyAndNameModel>

    let readModel () =
        let updateState state eventEnvelopes =
            let newState =
                eventEnvelopes
                |> List.fold projectToDomain state

            newState

        ReadModels.readModel updateState DomainByKeyAndNameModel.Empty

module BoundedContexts =
    open Contexture.Api.Aggregates.BoundedContext
    open ValueObjects
    open Utils
    
    type BoundedContextByKeyAndNameModel =
        { ByKey: Map<string, BoundedContextId>
          ByName: Map<string, Set<BoundedContextId>> }
        static member Empty =
            { ByKey = Map.empty
              ByName = Map.empty }

    let private projectToBoundedContext state eventEnvelope =
        let addKey canBeKey domain byKey =
            match canBeKey with
            | Some key -> byKey |> Map.add key domain
            | None -> byKey

        let append key value items = appendToSet items (key, value)

        match eventEnvelope.Event with
        | BoundedContextCreated n ->
            { state with
                  ByName = state.ByName |> append n.Name n.BoundedContextId }
        | KeyAssigned k ->
            { state with
                  ByKey =
                      state.ByKey
                      |> Map.filter (fun _ v -> v <> k.BoundedContextId)
                      |> addKey k.Key k.BoundedContextId }
        | BoundedContextImported n ->
            { state with
                  ByName = appendToSet state.ByName (n.Name, n.BoundedContextId)
                  ByKey = state.ByKey |> addKey n.Key n.BoundedContextId }
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
                  ByKey =
                      state.ByKey
                      |> Map.filter (fun _ v -> v <> l.BoundedContextId) }
        | _ -> state

    let byName (model: BoundedContextByKeyAndNameModel) (phrase: SearchPhrase) =
        model.ByName
        |> findByKey phrase
        |> SearchPhraseResult.fromManyResults

    let byKey (model: BoundedContextByKeyAndNameModel) (phrase: SearchPhrase) =
        model.ByKey
        |> findByKey phrase
        |> SearchPhraseResult.fromResults

    type ReadModel =
        ReadModels.ReadModel<Aggregates.BoundedContext.Event, BoundedContextByKeyAndNameModel>

    let readModel () =
        let updateState state eventEnvelopes =
            let newState =
                eventEnvelopes
                |> List.fold projectToBoundedContext state

            newState

        ReadModels.readModel updateState BoundedContextByKeyAndNameModel.Empty
