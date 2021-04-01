module ContextMapping.CollaborationId exposing (
  CollaborationId,
  idToString, idEncoder,
  idFromString, idParser, idDecoder)

import Url.Parser exposing (Parser, custom)

import Json.Decode as Decode exposing (Decoder)
import Json.Encode as Encode

type CollaborationId = 
  CollaborationId String


idToString : CollaborationId -> String
idToString (CollaborationId contextId) =
  contextId

idParser : Parser (CollaborationId -> a) a
idParser =
    custom "COLLID" <|
        \collaborationId ->
            idFromString collaborationId


idFromString : String -> Maybe CollaborationId
idFromString value =
  if String.isEmpty value
  then Nothing
  else Just (CollaborationId value)

idDecoder : Decoder CollaborationId
idDecoder =
  Decode.map CollaborationId Decode.string

idEncoder : CollaborationId -> Encode.Value
idEncoder (CollaborationId value) =
  Encode.string value