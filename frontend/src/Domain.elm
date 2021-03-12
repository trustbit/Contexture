module Domain exposing (
  Domain, DomainRelation(..), Problem(..), Name,
  domainRelation, asName, isNameValid, name, id, vision, key,
  domainDecoder, domainsDecoder,
  newDomain, moveDomain, remove, renameDomain, updateVision, assignKey)

import Json.Decode as Decode exposing(Decoder)
import Json.Decode.Pipeline as JP
import Json.Encode as Encode

import Url
import Http

import Key exposing (Key)
import Domain.DomainId exposing(DomainId(..), idDecoder)
import Api exposing(ApiResult)

-- MODEL

type Name
  = InternalName String

type Domain
  = Domain Internal

type alias Internal =
  { id : DomainId
  , name : String
  , vision : String
  , key : Maybe Key
  , parentDomain : Maybe DomainId
  }

type DomainRelation
  = Subdomain DomainId
  | Root

type Problem
  = NameInvalid

-- actions

isNameValid : String -> Bool
isNameValid couldBeName =
  String.length couldBeName > 0

asName : String -> Result Problem Name
asName couldBeName =
  if isNameValid couldBeName
  then couldBeName |> InternalName |> Ok
  else Err NameInvalid

name : Domain -> String
name (Domain domain) =
  domain.name

id : Domain -> DomainId
id (Domain domain) =
  domain.id

vision : Domain -> Maybe String
vision (Domain domain) =
  if String.isEmpty domain.vision
  then Nothing
  else Just domain.vision

domainRelation : Domain -> DomainRelation
domainRelation (Domain domain) =
  case domain.parentDomain of
    Nothing -> Root
    Just subId -> Subdomain subId

key : Domain -> Maybe Key
key (Domain domain) =
  domain.key

-- CONVERSIONS

nameFieldDecoder : Decoder String
nameFieldDecoder =
  Decode.field "name" Decode.string

nameFieldEncoder : String -> (String, Encode.Value)
nameFieldEncoder theName =
  ("name", Encode.string theName)

idFieldDecoder : Decoder DomainId
idFieldDecoder =
  Decode.field "id" idDecoder

domainDecoder: Decoder Domain
domainDecoder =
  ( Decode.succeed Internal
    |> JP.custom idFieldDecoder
    |> JP.custom nameFieldDecoder
    |> JP.optional "vision" Decode.string ""
    |> JP.optional "key" (Decode.maybe Key.keyDecoder) Nothing
    |> JP.optional "domainId" (Decode.maybe idDecoder) Nothing
  ) |> Decode.map Domain


domainsDecoder: Decode.Decoder (List Domain)
domainsDecoder =
  Decode.list domainDecoder

newDomain : Api.Configuration -> DomainRelation -> Name -> ApiResult Domain msg
newDomain url relation (InternalName theName) toMsg =
  let
    api =
      case relation of
        Root -> Api.domains []
        Subdomain subDomainId -> Api.subDomains [] subDomainId
  in
    Http.post
      { url = api |> Api.url url |> Url.toString
      , body = Http.jsonBody <| Encode.object [ nameFieldEncoder theName ]
      , expect = Http.expectJson toMsg domainDecoder
      }

renameDomain : Api.Configuration -> DomainId -> Name -> ApiResult Domain msg
renameDomain baseUrl domain (InternalName newName) =
  let
    request toMsg =
      Http.request
      { method = "POST"
      , headers = []
      , url = domain |> Api.domain [] |> Api.url baseUrl |> Url.toString |> (\u -> u ++ "/rename")
      , body = Http.jsonBody <|  Encode.object [ nameFieldEncoder newName ]
      , expect = Http.expectJson toMsg domainDecoder
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request

updateVision : Api.Configuration -> DomainId -> Maybe String -> ApiResult Domain msg
updateVision baseUrl domain theVision =
  let
    encodedVision =
      case theVision of
        Just v -> Encode.string v
        Nothing -> Encode.null
    request toMsg =
      Http.request
      { method = "POST"
      , headers = []
      , url = domain |> Api.domain [] |> Api.url baseUrl |> Url.toString |> (\u -> u ++ "/vision")
      , body = Http.jsonBody <|  Encode.object [ ("vision", encodedVision) ]
      , expect = Http.expectJson toMsg domainDecoder
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request

moveDomain : Api.Configuration -> DomainId -> DomainRelation -> ApiResult () msg
moveDomain baseUrl domain target =
  let
    value =
      case target of
        Subdomain subDomainId -> Domain.DomainId.idEncoder subDomainId
        Root ->
          Encode.null
    request toMsg =
      Http.request
      { method = "POST"
      , headers = []
      , url = domain |> Api.domain [] |> Api.url baseUrl |> Url.toString |> (\u -> u ++ "/move")
      , body = Http.jsonBody <| Encode.object[ ("parentDomain", value) ]
      , expect = Http.expectWhatever toMsg
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request

remove : Api.Configuration -> DomainId -> ApiResult () msg
remove base domainId =
  let
    request toMsg =
      Http.request
      { method = "DELETE"
      , headers = []
      , url = domainId |> Api.domain [] |> Api.url base |> Url.toString
      , body = Http.emptyBody
      , expect = Http.expectWhatever toMsg
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request

assignKey : Api.Configuration -> DomainId -> Maybe Key -> ApiResult Domain msg
assignKey base domainId domainKey =
  let
    encodedKey =
      case domainKey of
        Just v -> Key.keyEncoder v
        Nothing -> Encode.null
    request toMsg =
      Http.request
      { method = "PATCH"
      , headers = []
      , url = domainId |> Api.domain [] |> Api.url base |> Url.toString
      , body = Http.jsonBody <| Encode.object[ ("key", encodedKey) ]
      , expect = Http.expectJson toMsg domainDecoder
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request
