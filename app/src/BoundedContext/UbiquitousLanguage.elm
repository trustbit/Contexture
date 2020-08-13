module BoundedContext.UbiquitousLanguage exposing (..)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Dict exposing (Dict)

type alias UbiquitousLanguageId = String

type alias DomainTerm = String
type alias Description = String
type LanguageTerm =
  LanguageTerm DomainTerm (Maybe Description)
 
type UbiquitousLanguage =
  UbiquitousLanguage (Dict DomainTerm (Maybe Description))

type Problem
  = TermDefinitionEmpty
  | TermAlreadyAdded

addLanguageTerm : UbiquitousLanguage -> String -> String -> Result Problem UbiquitousLanguage
addLanguageTerm (UbiquitousLanguage terms) term desc =
  if String.isEmpty term then
    Err TermDefinitionEmpty
  else 
    case terms |> Dict.get term of
      Just _ ->
        Err TermAlreadyAdded
      Nothing ->
        terms
        |> Dict.insert term (if String.isEmpty desc then Nothing else Just desc)
        |> UbiquitousLanguage
        |> Ok

removeLanguageTerm : UbiquitousLanguage -> DomainTerm -> UbiquitousLanguage
removeLanguageTerm (UbiquitousLanguage terms) term =
  terms |> Dict.remove term |> UbiquitousLanguage

noLanguageTerms :  UbiquitousLanguage
noLanguageTerms =
  UbiquitousLanguage Dict.empty

languageTerms : UbiquitousLanguage -> List LanguageTerm
languageTerms (UbiquitousLanguage terms) =
  terms
  |> Dict.toList
  |> List.map (\(key,value) -> LanguageTerm key value)

domainTerm : LanguageTerm -> String
domainTerm (LanguageTerm term _) =
  term

description : LanguageTerm -> Maybe String
description (LanguageTerm _ desc) =
  desc

modelEncoder : UbiquitousLanguage -> Encode.Value
modelEncoder (UbiquitousLanguage terms) =
  terms |>
    Encode.dict 
      (\v -> v)
      (\desc -> 
        case desc of
          Just v ->
            Encode.string v
          Nothing ->
            Encode.null
      ) 

modelDecoder : Decode.Decoder UbiquitousLanguage
modelDecoder =
    Decode.dict (Decode.nullable Decode.string)
    |> Decode.map UbiquitousLanguage