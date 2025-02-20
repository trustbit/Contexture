namespace Contexture.Api.ReadModels.Find
open System

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
    | NoResult

type SearchResult<'T when 'T: comparison> =
    | FoundResults of Set<'T>
    | Nothing
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
            SearchPhraseResult.NoResult
        else
            results |> Set.ofSeq |> SearchPhraseResult.Results

    let fromManyResults results =
        results
        |> fromResults

    let combineResultsWithAnd (searchResults: SearchPhraseResult<_> seq) =
        searchResults
        |> Seq.fold
            (fun state results ->
                match state, results with
                | Some (SearchPhraseResult.Results existing), SearchPhraseResult.Results r ->
                    Set.intersect r existing |> fromResults |> Some
                | None, SearchPhraseResult.Results results -> results |> fromResults |> Some
                | _, _ -> Some SearchPhraseResult.NoResult)
            None
        |> Option.defaultValue NoResult

module SearchResult =
    let value =
        function
        | SearchResult.FoundResults results -> Some results
        | SearchResult.Nothing -> Some Set.empty
        | SearchResult.NotUsed -> None

    let fromResults results =
        if Seq.isEmpty results then
            SearchResult.Nothing
        else
            results |> Set.ofSeq |> SearchResult.FoundResults

    let fromManyResults results =
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
                | SearchResult.FoundResults existing, SearchPhraseResult.Results r ->
                    Set.intersect r existing |> fromResults
                | SearchResult.NotUsed, SearchPhraseResult.Results r -> fromResults r
                | _, SearchPhraseResult.NoResult -> SearchResult.Nothing
                | SearchResult.Nothing, _ -> SearchResult.Nothing)
            SearchResult.NotUsed

    let private combineSearchResultsWithAnd ids results =
        match ids, results with
        | SearchResult.FoundResults existing, SearchResult.FoundResults r -> Set.intersect r existing |> fromResults
        | SearchResult.NotUsed, SearchResult.FoundResults r -> fromResults r
        | SearchResult.FoundResults existing, SearchResult.NotUsed -> fromResults existing
        | SearchResult.NotUsed, SearchResult.NotUsed -> SearchResult.NotUsed
        | SearchResult.Nothing, _ -> SearchResult.Nothing
        | _, SearchResult.Nothing -> SearchResult.Nothing

    let combineResults (searchResults: SearchResult<_> seq) =
        searchResults
        |> Seq.fold combineSearchResultsWithAnd SearchResult.NotUsed

    let map<'a, 'b when 'a: comparison and 'b: comparison> (mapper: 'a -> 'b) result : SearchResult<'b> =
        match result with
        | SearchResult.FoundResults r -> r |> Set.map mapper |> fromResults
        | SearchResult.Nothing -> SearchResult.Nothing
        | SearchResult.NotUsed -> SearchResult.NotUsed

    let bind<'a, 'b when 'a: comparison and 'b: comparison>
        (mapper: Set<'a> -> SearchResult<'b>)
        result
        : SearchResult<'b> =
        match result with
        | SearchResult.FoundResults r -> mapper r
        | SearchResult.Nothing -> SearchResult.Nothing
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
