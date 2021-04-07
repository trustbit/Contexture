module BoundedContext.UbiquitousLanguage exposing (
  LanguageTerm, DomainTermId, UbiquitousLanguage, Problem(..),
  defineLanguageTerm, removeLanguageTerm, addLanguageTerm, getUbiquitousLanguage,
  noLanguageTerms, languageTerms,
  id,domainTerm,domainDescription,
  optionalUbiquitousLanguageDecoder)

import Json.Encode as Encode
import Json.Decode as Decode
import Json.Decode.Pipeline as JP

import Http
import Url
import Api as Api
import BoundedContext.BoundedContextId exposing (BoundedContextId)


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


addTermToLanguage : UbiquitousLanguage -> LanguageTerm -> Result Problem UbiquitousLanguage
addTermToLanguage (UbiquitousLanguage terms) (LanguageTerm (DomainTermId termId) term desc) =
  -- recheck if term was added in the meanwhile
  if terms |> isTermUnique term then
    terms
      |> Dict.insert termId { term = term, description = desc}
      |> UbiquitousLanguage
      |> Ok
  else
      Err TermAlreadyAdded


addLanguageTerm : Api.Configuration -> BoundedContextId -> UbiquitousLanguage -> LanguageTerm -> Result Problem (Api.ApiResult UbiquitousLanguage msg)
addLanguageTerm configuration contextId language term =
  case addTermToLanguage language term of
    Ok updatedLanguage ->
      let
        api = Api.boundedContext contextId
        request toMsg =
          Http.request
            { method = "POST"
            , url = api |> Api.url configuration  |> (\c -> c ++ "/ubiquitousLanguage")
            , body = Http.jsonBody <|
                Encode.object [ ubiquitousLanguageEncoder updatedLanguage ]
            , expect = Http.expectJson toMsg ubiquitousLanguageDecoder
            , timeout = Nothing
            , tracker = Nothing
            , headers = []
            }
      in
        Ok request
    Err problem ->
      problem |> Err


getUbiquitousLanguage : Api.Configuration -> BoundedContextId -> Api.ApiResult UbiquitousLanguage msg
getUbiquitousLanguage configuration contextId =
  let
    api = Api.boundedContext contextId
    request toMsg =
      Http.get
        { url = api |> Api.url configuration 
        , expect = Http.expectJson toMsg ubiquitousLanguageDecoder
        }
  in
    request


removeTermFromLanguage : UbiquitousLanguage -> DomainTermId -> UbiquitousLanguage
removeTermFromLanguage (UbiquitousLanguage terms) (DomainTermId termId) =
  terms |> Dict.remove termId |> UbiquitousLanguage


removeLanguageTerm : Api.Configuration -> BoundedContextId -> UbiquitousLanguage -> DomainTermId -> Api.ApiResult UbiquitousLanguage msg
removeLanguageTerm configuration contextId language term =
  let
    api = Api.boundedContext contextId
    removedRoles = removeTermFromLanguage language term
    request toMsg =
      Http.request
        { method = "POST"
        , url = api |> Api.url configuration  |> (\c -> c ++ "/ubiquitousLanguage")
        , body = Http.jsonBody <|
            Encode.object [ ubiquitousLanguageEncoder removedRoles ]
        , expect = Http.expectJson toMsg ubiquitousLanguageDecoder
        , timeout = Nothing
        , tracker = Nothing
        , headers = []
        }
  in
    request

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

ubiquitousLanguageEncoder language = ("ubiquitousLanguage", modelEncoder language)

ubiquitousLanguageDecoder : Decode.Decoder UbiquitousLanguage
ubiquitousLanguageDecoder = Decode.at [ "ubiquitousLanguage"] modelDecoder

optionalUbiquitousLanguageDecoder : Decode.Decoder (UbiquitousLanguage -> b) -> Decode.Decoder b
optionalUbiquitousLanguageDecoder =
    JP.optional "ubiquitousLanguage" modelDecoder noLanguageTerms

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