module Domain exposing (
  Domain, DomainRelation(..),
  isNameValid, isSubDomain,
  domainDecoder, domainsDecoder, modelEncoder, idFieldDecoder, nameFieldDecoder,
  update, newDomain, moveDomain)

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

newDomain : Api.Configuration -> String ->  ApiResult Domain msg
newDomain url name toMsg =
  let
    body =
      Encode.object
      [ ("name", Encode.string name) ]
  in
    Http.post
      { url = Api.domains [] |> Api.url url |> Url.toString
      , body = Http.jsonBody body
      , expect = Http.expectJson toMsg domainDecoder
      }

moveDomain : Api.Configuration -> DomainId -> DomainRelation -> ApiResult () msg
moveDomain baseUrl domain target =
  let
    value =
      case target of
        Subdomain id -> Domain.DomainId.idEncoder id
        Root -> Encode.null
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


update : Url.Url -> Domain -> (Result Http.Error () -> msg) -> Cmd msg
update url domain toMsg =
  Http.request
    { method = "PUT"
    , headers = []
    , url = Url.toString url
    , body = Http.jsonBody <| modelEncoder domain
    , expect = Http.expectWhatever toMsg
    , timeout = Nothing
    , tracker = Nothing
    }