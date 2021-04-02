module BoundedContext.BoundedContextId exposing (
  BoundedContextId,
  idToString, idEncoder,
  idFromString, idParser, idDecoder,
  value)

import Url.Parser exposing (Parser, custom)

import Json.Decode as Decode exposing (Decoder)
import Json.Encode as Encode

type BoundedContextId
  = BoundedContextId String


value : BoundedContextId -> String
value (BoundedContextId id) =
  id


idToString : BoundedContextId -> String
idToString (BoundedContextId contextId) =
  contextId

idParser : Parser (BoundedContextId -> a) a
idParser =
    custom "BCCID" <|
        \bccId ->
            bccId |> idFromString


idFromString : String -> Maybe BoundedContextId
idFromString id =
  if String.isEmpty id
  then Nothing
  else id |> BoundedContextId |> Just

idDecoder : Decoder BoundedContextId
idDecoder =
  Decode.map BoundedContextId Decode.string

idEncoder : BoundedContextId -> Encode.Value
idEncoder (BoundedContextId id) =
  Encode.string id