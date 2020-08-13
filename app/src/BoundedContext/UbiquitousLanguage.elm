module BoundedContext.UbiquitousLanguage exposing (
  LanguageTerm, DomainTermId, UbiquitousLanguage, Problem(..),
  defineLanguageTerm, addLanguageTerm, removeLanguageTerm,
  noLanguageTerms, languageTerms,
  id,domainTerm,domainDescription,
  modelDecoder, modelEncoder)

import Json.Encode as Encode
import Json.Decode as Decode

import Dict exposing (Dict)

type DomainTermId =
  DomainTermId String
type alias DomainTerm = String
type alias Description = String

type LanguageTerm =
  LanguageTerm DomainTermId DomainTerm (Maybe Description)

type UbiquitousLanguage =
  UbiquitousLanguage InternalUbiquitousLanguage

type Problem
  = TermDefinitionEmpty
  | TermAlreadyAdded

type alias InternalUbiquitousLanguage = Dict String InternalDictionaryValue
type alias InternalDictionaryValue =
  { term : DomainTerm
  , description : Maybe Description
  }


isTermUnique : String ->  InternalUbiquitousLanguage -> Bool
isTermUnique term terms  =
  terms
  |> Dict.keys
  |> List.map String.toLower
  |> List.member (term |> String.toLower)
  |> not


defineLanguageTerm : UbiquitousLanguage -> String -> String -> Result Problem LanguageTerm
defineLanguageTerm (UbiquitousLanguage terms) term desc =
  if String.isEmpty term then
    Err TermDefinitionEmpty
  else
    if terms |> isTermUnique term
    then Ok <| LanguageTerm (term |> String.toLower |> DomainTermId) term (if String.isEmpty desc then Nothing else Just desc)
    else Err TermAlreadyAdded


addLanguageTerm : UbiquitousLanguage -> LanguageTerm -> Result Problem UbiquitousLanguage
addLanguageTerm (UbiquitousLanguage terms) (LanguageTerm (DomainTermId termId) term desc) =
  -- recheck if term was added in the meanwhile
  if terms |> isTermUnique term then
    terms
      |> Dict.insert termId { term = term, description = desc}
      |> UbiquitousLanguage
      |> Ok
  else
      Err TermAlreadyAdded


removeLanguageTerm : UbiquitousLanguage -> DomainTermId -> UbiquitousLanguage
removeLanguageTerm (UbiquitousLanguage terms) (DomainTermId termId) =
  terms |> Dict.remove termId |> UbiquitousLanguage


noLanguageTerms :  UbiquitousLanguage
noLanguageTerms =
  UbiquitousLanguage Dict.empty


languageTerms : UbiquitousLanguage -> List LanguageTerm
languageTerms (UbiquitousLanguage terms) =
  terms
  |> Dict.toList
  |> List.map (\(key, { term, description }) -> LanguageTerm (DomainTermId key) term description)


id : LanguageTerm -> DomainTermId
id (LanguageTerm idValue _ _) =
  idValue


domainTerm : LanguageTerm -> String
domainTerm (LanguageTerm _ term _) =
  term


domainDescription : LanguageTerm -> Maybe String
domainDescription (LanguageTerm _ _ desc) =
  desc

modelEncoder : UbiquitousLanguage -> Encode.Value
modelEncoder (UbiquitousLanguage terms) =
  let
    valueEncoder { term, description } =
        Encode.object
          [ ("term", Encode.string term)
          , ("description"
            , case description of
                Just v ->
                  Encode.string v
                Nothing ->
                  Encode.null
            )
          ]
  in
    terms |> Encode.dict (\v -> v) valueEncoder


modelDecoder : Decode.Decoder UbiquitousLanguage
modelDecoder =
  let
    valueDecoder =
      Decode.map2 InternalDictionaryValue
        (Decode.field "term" Decode.string)
        (Decode.maybe (Decode.field "description" Decode.string))
  in
    Decode.dict valueDecoder
    |> Decode.map UbiquitousLanguage