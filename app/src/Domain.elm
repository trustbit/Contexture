module Domain exposing (
  Domain,  
  isNameValid,
  domainDecoder, domainsDecoder, modelEncoder, idFieldDecoder, nameFieldDecoder,
  update, newDomain
  )

import Json.Decode as Decode exposing(Decoder)
import Json.Decode.Pipeline as JP
import Json.Encode as Encode

import Url
import Http

import Domain.DomainId exposing(DomainId(..), idDecoder)

-- MODEL

type alias Domain =
  { id : DomainId
  , name: String
  , vision: String
  , parentDomain: Maybe DomainId
  }

-- actions

isNameValid : String -> Bool
isNameValid couldBeName =
  String.length couldBeName > 0

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

newDomain : Url.Url -> String -> (Result Http.Error Domain -> msg) -> Cmd msg
newDomain url name toMsg =
  let
    body =
      Encode.object
      [ ("name", Encode.string name) ]
  in
    Http.post
      { url = {url | path = url.path ++ "/domains" } |> Url.toString
      , body = Http.jsonBody body
      , expect = Http.expectJson toMsg domainDecoder
      }

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