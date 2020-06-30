module BoundedContext.BoundedContextId exposing (
  BoundedContextId,
  idToString, idFromString, idParser, idDecoder)

import Url.Parser exposing (Parser, custom)

import Json.Decode as Decode exposing (Decoder)

type BoundedContextId
  = BoundedContextId Int


idToString : BoundedContextId -> String
idToString bccId =
  case bccId of
    BoundedContextId bcId -> String.fromInt bcId

idParser : Parser (BoundedContextId -> a) a
idParser =
    custom "BCCID" <|
        \bccId ->
            Maybe.map BoundedContextId (String.toInt bccId)


idFromString : String -> Maybe BoundedContextId
idFromString value =
  value
  |> String.toInt
  |> Maybe.map BoundedContextId

idDecoder : Decoder BoundedContextId
idDecoder =
  Decode.map BoundedContextId Decode.int
