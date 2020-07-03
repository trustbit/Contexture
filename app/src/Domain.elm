module Domain exposing (
  Domain, DomainRelation(..),
  isNameValid, isSubDomain,
  domainDecoder, domainsDecoder, modelEncoder, idFieldDecoder, nameFieldDecoder,
  newDomain, moveDomain, remove)

import Json.Decode as Decode exposing(Decoder)
import Json.Decode.Pipeline as JP
import Json.Encode as Encode

import Url
import Http

import Domain.DomainId exposing(DomainId(..), idDecoder)
import Api exposing(ApiResult)

-- MODEL

type alias Domain =
  { id : DomainId
  , name: String
  , vision: String
  , parentDomain: Maybe DomainId
  }

type DomainRelation
  = Subdomain DomainId
  | Root

-- actions

isNameValid : String -> Bool
isNameValid couldBeName =
  String.length couldBeName > 0

isSubDomain : Domain -> Bool
isSubDomain domain =
  case domain.parentDomain of
    Nothing -> False
    Just _ -> True

-- CONVERSIONS

nameFieldDecoder : Decoder String
nameFieldDecoder =
  Decode.field "name" Decode.string

idFieldDecoder : Decoder DomainId
idFieldDecoder =
  Decode.field "id" idDecoder

domainDecoder: Decoder Domain
domainDecoder =
  Decode.succeed Domain
    |> JP.custom idFieldDecoder
    |> JP.custom nameFieldDecoder
    |> JP.optional "vision" Decode.string ""
    |> JP.optional "domainId" (Decode.maybe idDecoder) Nothing

domainsDecoder: Decode.Decoder (List Domain)
domainsDecoder =
  Decode.list domainDecoder

modelEncoder : Domain -> Encode.Value
modelEncoder model =
    Encode.object
        [ ("name", Encode.string model.name)
        , ("vision", Encode.string model.vision)
        ]

newDomain : Api.Configuration -> DomainRelation -> String ->  ApiResult Domain msg
newDomain url relation name toMsg =
  let
    body =
      Encode.object
      [ ("name", Encode.string name) ]
    api =
      case relation of
        Root -> Api.domains []
        Subdomain id -> Api.subDomains [] id
  in
    Http.post
      { url = api |> Api.url url |> Url.toString
      , body = Http.jsonBody body
      , expect = Http.expectJson toMsg domainDecoder
      }

moveDomain : Api.Configuration -> DomainId -> DomainRelation -> ApiResult () msg
moveDomain baseUrl domain target =
  let
    value =
      case target of
        Subdomain id -> Domain.DomainId.idEncoder id
        Root ->
          -- we can't encode the root domain as 'null' otherwise json-server will fail
          Encode.string ""
    request toMsg =
      Http.request
      { method = "PATCH"
      , headers = []
      , url = domain |> Api.domain [] |> Api.url baseUrl |> Url.toString
      , body = Http.jsonBody <| Encode.object[ ("domainId", value) ]
      , expect = Http.expectWhatever toMsg
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request

remove: Api.Configuration -> DomainId -> ApiResult () msg
remove base id =
  let
    request toMsg =
      Http.request
      { method = "DELETE"
      , headers = []
      , url = id |> Api.domain [] |> Api.url base |> Url.toString
      , body = Http.emptyBody
      , expect = Http.expectWhatever toMsg
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request