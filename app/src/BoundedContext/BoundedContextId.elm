module BoundedContext.BoundedContextId exposing (
  BoundedContextId,
  idToString, idEncoder,
  idFromString, idParser, idDecoder)

import Url.Parser exposing (Parser, custom)

import Json.Decode as Decode exposing (Decoder)
import Json.Encode as Encode

type BoundedContextId
  = BoundedContextId Int


idToString : BoundedContextId -> String
idToString (BoundedContextId contextId) =
  String.fromInt contextId

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

idEncoder : BoundedContextId -> Encode.Value
idEncoder (BoundedContextId value) =
  Encode.int value