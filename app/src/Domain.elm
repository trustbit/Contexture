module Domain exposing (
  DomainId(..),
  idToString, idParser, idEncoder, idDecoder
  )

import Json.Decode as Decode
import Json.Encode as Encode
import Url.Parser exposing (Parser, custom)

-- MODEL

type DomainId
  = DomainId Int

type alias Domain =
  { id: DomainId
  , name: String
  , description: String }

init : () -> Domain
init _ =
    { id = DomainId 0
    , name = ""
    , description = ""}

-- UPDATE


-- VIEW


-- CONVERSIONS

extractInt : DomainId -> Int
extractInt value =
  case value of
    DomainId v -> v

idToString : DomainId -> String
idToString domainId =
  case domainId of
    DomainId id -> String.fromInt id

idParser : Parser (DomainId -> a) a
idParser =
    custom "DOMAINID" <|
        \domainId ->
            Maybe.map DomainId (String.toInt domainId)

idDecoder : Decode.Decoder DomainId
idDecoder =
  Decode.map DomainId Decode.int


idEncoder : DomainId -> Encode.Value
idEncoder value =
  Encode.int (extractInt value)
