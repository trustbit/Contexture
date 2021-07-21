namespace Contexture.Api.ReadModels

open System
open Contexture.Api

open Contexture.Api.Infrastructure

module Find =
    type Operator =
        | Equals
        | StartsWith
        | Contains
        | EndsWith

    type SearchPhrase = private | SearchPhrase of Operator * string

    type SearchTerm = private SearchTerm of string

    type SearchArgument<'v> =
        private
        | Arguments of 'v list
        | Unused

    type SearchPhraseResult<'T when 'T: comparison> =
        | Results of Set<'T>
        | Nothing

    type SearchResult<'T when 'T: comparison> =
        | Results of Set<'T>
        | NoResult
        | NotUsed

    module SearchTerm =
        let fromInput (term: string) =
            term
            |> Option.ofObj
            |> Option.filter (not << String.IsNullOrWhiteSpace)
            |> Option.map (fun s -> s.Trim())
            |> Option.map SearchTerm

        let value (SearchTerm term) = term

    module SearchPhrase =
        let private operatorAndPhrase (phrase: string) =
            match phrase.StartsWith "*", phrase.EndsWith "*" with
            | true, true -> // *phrase*
                Contains, phrase.Trim '*'
            | true, false -> // *phrase
                EndsWith, phrase.TrimStart '*'
            | false, true -> // phrase*
                StartsWith, phrase.TrimEnd '*'
            | false, false -> // phrase
                Equals, phrase

        let fromInput (phrase: string) =
            phrase
            |> Option.ofObj
            |> Option.filter (not << String.IsNullOrWhiteSpace)
            |> Option.map (fun s -> s.Trim())
            |> Option.map operatorAndPhrase
            |> Option.map SearchPhrase

        let matches (SearchPhrase (operator, phrase)) (SearchTerm value) =
            match operator with
            | Equals -> String.Equals(phrase, value, StringComparison.OrdinalIgnoreCase)
            | StartsWith -> value.StartsWith(phrase, StringComparison.OrdinalIgnoreCase)
            | EndsWith -> value.EndsWith(phrase, StringComparison.OrdinalIgnoreCase)
            | Contains -> value.Contains(phrase, StringComparison.OrdinalIgnoreCase)

    module SearchPhraseResult =
        let fromResults results =
            if Seq.isEmpty results then
                SearchPhraseResult.Nothing
            else
                results |> Set.ofSeq |> SearchPhraseResult.Results

        let fromManyResults results =
            results
            |> Seq.map Set.ofSeq
            |> Set.unionMany
            |> fromResults

        let combineResultsWithAnd (searchResults: SearchPhraseResult<_> seq) =
            searchResults
            |> Seq.fold
                (fun state results ->
                    match state, results with
                    | Some (SearchPhraseResult.Results existing), SearchPhraseResult.Results r ->
                        Set.intersect r existing |> fromResults |> Some
                    | None, SearchPhraseResult.Results results -> results |> fromResults |> Some
                    | _, _ -> Some SearchPhraseResult.Nothing)
                None
            |> Option.defaultValue Nothing

    module SearchResult =
        let value =
            function
            | SearchResult.Results results -> Some results
            | SearchResult.NoResult -> Some Set.empty
            | SearchResult.NotUsed -> None

        let fromResults results =
            if Seq.isEmpty results then
                SearchResult.NoResult
            else
                results |> Set.ofSeq |> SearchResult.Results

        let takeAllResults results =
            results
            |> Seq.map Set.ofSeq
            |> Set.unionMany
            |> fromResults

        let fromOption result =
            result |> Option.defaultValue SearchResult.NotUsed

        let fromSearchPhrases (searchResults: SearchPhraseResult<_> seq) =
            searchResults
            |> Seq.fold
                (fun state results ->
                    match state, results with
                    | SearchResult.Results existing, SearchPhraseResult.Results r ->
                        Set.intersect r existing |> fromResults
                    | SearchResult.NotUsed, SearchPhraseResult.Results r -> fromResults r
                    | _, SearchPhraseResult.Nothing -> SearchResult.NoResult
                    | SearchResult.NoResult, _ -> SearchResult.NoResult)
                SearchResult.NotUsed

        let combineSearchResultsWithAnd ids results =
            match ids, results with
            | SearchResult.Results existing, SearchResult.Results r -> Set.intersect r existing |> fromResults
            | SearchResult.NotUsed, SearchResult.Results r -> fromResults r
            | SearchResult.Results existing, SearchResult.NotUsed -> fromResults existing
            | SearchResult.NotUsed, SearchResult.NotUsed -> SearchResult.NotUsed
            | SearchResult.NoResult, _ -> SearchResult.NoResult
            | _, SearchResult.NoResult -> SearchResult.NoResult

        let combineResults (searchResults: SearchResult<_> seq) =
            searchResults
            |> Seq.fold combineSearchResultsWithAnd SearchResult.NotUsed

        let map<'a, 'b when 'a: comparison and 'b: comparison> (mapper: 'a -> 'b) result : SearchResult<'b> =
            match result with
            | SearchResult.Results r -> r |> Set.map mapper |> fromResults
            | SearchResult.NoResult -> SearchResult.NoResult
            | SearchResult.NotUsed -> SearchResult.NotUsed

        let bind<'a, 'b when 'a: comparison and 'b: comparison>
            (mapper: Set<'a> -> SearchResult<'b>)
            result
            : SearchResult<'b> =
            match result with
            | SearchResult.Results r -> mapper r
            | SearchResult.NoResult -> SearchResult.NoResult
            | SearchResult.NotUsed -> SearchResult.NotUsed


    module SearchArgument =
        let fromValues (values: _ seq) =
            let valueList = values |> Seq.toList

            if List.isEmpty valueList then
                SearchArgument.Unused
            else
                SearchArgument.Arguments valueList

        let fromInputs (phraseInputs: string seq) =
            let searchPhrases =
                phraseInputs |> Seq.choose SearchPhrase.fromInput

            fromValues searchPhrases

        let executeSearch search argument =
            match argument with
            | Arguments phrases ->
                phrases
                |> Seq.map search
                |> SearchResult.fromSearchPhrases
            | SearchArgument.Unused -> SearchResult.NotUsed

    let private appendToSet items (key, value) =
        items
        |> Map.change
            key
            (function
            | Some values -> values |> Set.add value |> Some
            | None -> value |> Set.singleton |> Some)

    let private removeFromSet findValue value items =
        items
        |> Map.map
            (fun _ (values: Set<_>) ->
                values
                |> Set.filter (fun n -> findValue n <> value))

    let private findByKey keyPhrase items =
        let matchesKey (key: string) =
            let term = key |> SearchTerm.fromInput

            term
            |> Option.map (SearchPhrase.matches keyPhrase)
            |> Option.defaultValue false

        items
        |> Map.filter (fun k _ -> matchesKey k)
        |> Map.toList
        |> List.map snd

    let private selectResults selectResult items =
        items
        |> List.map (Map.toList >> List.map selectResult >> Set.ofList)

    module Namespaces =
        open Contexture.Api.Aggregates.Namespace
        open ValueObjects

        type NamespaceModel =
            { NamespaceId: NamespaceId
              NamespaceTemplateId: NamespaceTemplateId option }

        type NamespaceFinder = Map<string, Set<NamespaceModel>>

        let projectNamespaceNameToNamespaceId state eventEnvelope =
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
            |> Seq.map SearchResult.fromResults
            |> SearchResult.combineResultsWithAnd

        let byTemplate (namespaces: NamespaceFinder) (templateId: NamespaceTemplateId) =
            namespaces
            |> Map.toList
            |> List.map snd
            |> Set.unionMany
            |> Set.filter (fun m -> m.NamespaceTemplateId = Some templateId)
            |> SearchPhraseResult.fromResults

    let namespaces (eventStore: EventStore) : Async<Namespaces.NamespaceFinder> =
        async {
            let! allStreams = eventStore.AllStreams<Aggregates.Namespace.Event>()

            return
                allStreams
                |> List.fold Namespaces.projectNamespaceNameToNamespaceId Map.empty
        }

    module Labels =
        open Contexture.Api.Aggregates.BoundedContext
        open Contexture.Api.Aggregates.Namespace
        open ValueObjects

        type LabelAndNamespaceModel =
            { Value: string option
              NamespaceId: NamespaceId
              NamespaceTemplateId: NamespaceTemplateId option }

        type NamespacesByLabel =
            { Namespaces: Map<NamespaceId, BoundedContextId>
              ByLabelName: Map<String, NamespacesOfBoundedContext>
              ByLabelValue: Map<String, NamespacesOfBoundedContext> }
            static member Initial =
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

        let projectLabelNameToNamespace state eventEnvelope =
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

    let labels (eventStore: EventStore) : Async<Labels.NamespacesByLabel> =
        async {
            let! allStreams = eventStore.AllStreams<Aggregates.Namespace.Event>()

            return
                allStreams
                |> List.fold Labels.projectLabelNameToNamespace Labels.NamespacesByLabel.Initial
        }

    module Domains =
        open Contexture.Api.Aggregates.Domain
        open ValueObjects

        type DomainByKeyAndNameModel =
            { ByKey: Map<string, DomainId>
              ByName: Map<string, Set<DomainId>> }
            static member Empty =
                { ByKey = Map.empty
                  ByName = Map.empty }

        let projectToDomain state eventEnvelope =
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
            |> Seq.map SearchPhraseResult.fromResults
            |> SearchPhraseResult.combineResultsWithAnd

        let byKey (model: DomainByKeyAndNameModel) (phrase: SearchPhrase) =
            model.ByKey
            |> findByKey phrase
            |> SearchPhraseResult.fromResults

    let domains (eventStore: EventStore) : Async<Domains.DomainByKeyAndNameModel> =
        async {
            let! allStreams = eventStore.AllStreams<Aggregates.Domain.Event>()

            return
                allStreams
                |> List.fold Domains.projectToDomain Domains.DomainByKeyAndNameModel.Empty
        }

    module BoundedContexts =
        open Contexture.Api.Aggregates.BoundedContext
        open ValueObjects

        type BoundedContextByKeyAndNameModel =
            { ByKey: Map<string, BoundedContextId>
              ByName: Map<string, Set<BoundedContextId>> }
            static member Empty =
                { ByKey = Map.empty
                  ByName = Map.empty }

        let projectToBoundedContext state eventEnvelope =
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
            |> Seq.map SearchPhraseResult.fromResults
            |> SearchPhraseResult.combineResultsWithAnd

        let byKey (model: BoundedContextByKeyAndNameModel) (phrase: SearchPhrase) =
            model.ByKey
            |> findByKey phrase
            |> SearchPhraseResult.fromResults

    let boundedContexts (eventStore: EventStore) : Async<BoundedContexts.BoundedContextByKeyAndNameModel> =
        async {
            let! allStreams = eventStore.AllStreams<Aggregates.BoundedContext.Event>()

            return
                allStreams
                |> List.fold
                    BoundedContexts.projectToBoundedContext
                    BoundedContexts.BoundedContextByKeyAndNameModel.Empty
        }
