module ContextMapping.CollaborationId exposing (
  CollaborationId,
  idToString, idEncoder,
  idFromString, idParser, idDecoder)

import Url.Parser exposing (Parser, custom)

import Json.Decode as Decode exposing (Decoder)
import Json.Encode as Encode

type CollaborationId = 
  CollaborationId Int


idToString : CollaborationId -> String
idToString (CollaborationId contextId) =
  String.fromInt contextId

idParser : Parser (CollaborationId -> a) a
idParser =
    custom "COLLID" <|
        \collaborationId ->
            Maybe.map CollaborationId (String.toInt collaborationId)


idFromString : String -> Maybe CollaborationId
idFromString value =
  value
  |> String.toInt
  |> Maybe.map CollaborationId

idDecoder : Decoder CollaborationId
idDecoder =
  Decode.map CollaborationId Decode.int

idEncoder : CollaborationId -> Encode.Value
idEncoder (CollaborationId value) =
  Encode.int value