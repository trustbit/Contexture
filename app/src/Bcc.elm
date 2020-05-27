module Bcc exposing (BoundedContextCanvas, BoundedContextId, idToString, idDecoder, idParser)

import Url
import Url.Parser exposing (Parser, custom)

import Http
import Json.Encode as Encode
import Json.Decode exposing (Decoder, map2, field, string, int, at, nullable)
import Json.Decode.Pipeline as JP

-- MODEL

type BoundedContextId 
  = BoundedContextId Int

type alias BoundedContextCanvas = 
  { name: String
  , description: String
  }

-- UPDATE

type FieldMsg
  = SetName String
  | SetDescription String

updateFields: FieldMsg -> BoundedContextCanvas -> BoundedContextCanvas
updateFields msg canvas =
  case msg of
    SetName name ->
      { canvas | name = name}
      
    SetDescription description ->
      { canvas | description = description}
   
idToString : BoundedContextId -> String
idToString bccId =
  case bccId of
    BoundedContextId id -> String.fromInt id

idParser : Parser (BoundedContextId -> a) a
idParser =
    custom "BCCID" <|
        \bccId ->
            Maybe.map BoundedContextId (String.toInt bccId)

idDecoder : Decoder BoundedContextId
idDecoder =
  Json.Decode.map BoundedContextId int
