module Bcc exposing (..)

import Url.Parser exposing (Parser, custom)

import Http
import Json.Decode exposing (Decoder, map2, field, string, int, at, nullable)

-- MODEL

type BoundedContextId 
  = BoundedContextId Int

type alias BoundedContextCanvas = 
  { name: String
  , description: String
  }

-- UPDATE

type Msg
  = SetName String
  | SetDescription String

update: Msg -> BoundedContextCanvas -> BoundedContextCanvas
update msg canvas =
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
