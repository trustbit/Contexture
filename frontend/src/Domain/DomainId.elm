module Domain.DomainId exposing (
    DomainId(..),
    idToString, idFromString, idParser, idEncoder, idDecoder)

import Json.Decode as Decode exposing(Decoder)
import Json.Encode as Encode

import Url.Parser exposing (Parser, custom)

type DomainId
  = DomainId String

extractValue : DomainId -> String
extractValue value =
  case value of
    DomainId v -> v

idToString : DomainId -> String
idToString domainId =
  case domainId of
    DomainId id -> id

idFromString : String -> Maybe DomainId
idFromString value =
  if String.isEmpty value
  then Nothing
  else value |> DomainId |> Just

idFromStringSuccess : String -> Decoder DomainId
idFromStringSuccess value =
  case idFromString value of
    Just id -> Decode.succeed id
    Nothing -> Decode.fail ("Could not decode into DomainId " ++ value)

idParser : Parser (DomainId -> a) a
idParser =
    custom "DOMAINID" <|
        \domainId ->
            idFromString domainId

idDecoder : Decoder DomainId
idDecoder =
  Decode.oneOf
    [ Decode.map DomainId Decode.string
    , Decode.string |> Decode.andThen idFromStringSuccess]


idEncoder : DomainId -> Encode.Value
idEncoder value =
  Encode.string (extractValue value)
