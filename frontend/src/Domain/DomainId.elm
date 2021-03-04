module Domain.DomainId exposing (
    DomainId(..),
    idToString, idFromString, idParser, idEncoder, idDecoder)

import Json.Decode as Decode exposing(Decoder)
import Json.Encode as Encode

import Url.Parser exposing (Parser, custom)

type DomainId
  = DomainId Int

extractInt : DomainId -> Int
extractInt value =
  case value of
    DomainId v -> v

idToString : DomainId -> String
idToString domainId =
  case domainId of
    DomainId id -> String.fromInt id

idFromString : String -> Maybe DomainId
idFromString value =
  value
  |> String.toInt
  |> Maybe.map DomainId

idFromStringSuccess : String -> Decoder DomainId
idFromStringSuccess value =
  case idFromString value of
    Just id -> Decode.succeed id
    Nothing -> Decode.fail ("Could not decode into DomainId " ++ value)

idParser : Parser (DomainId -> a) a
idParser =
    custom "DOMAINID" <|
        \domainId ->
            Maybe.map DomainId (String.toInt domainId)

idDecoder : Decoder DomainId
idDecoder =
  Decode.oneOf
    [ Decode.map DomainId Decode.int
    , Decode.string |> Decode.andThen idFromStringSuccess]


idEncoder : DomainId -> Encode.Value
idEncoder value =
  Encode.int (extractInt value)
