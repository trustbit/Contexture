module Bcc exposing (..)

import Url.Parser exposing (Parser, custom)

import Http
import Json.Decode exposing (Decoder, map2, field, string, int, at, nullable)

-- MODEL

type BoundedContextId 
  = BoundedContextId Int

type Classification
  = Core
  | Supporting
  | Generic
  | Other String

type alias BoundedContextCanvas = 
  { name: String
  , description: String
  , classification : Maybe Classification
  }

-- UPDATE

type Msg
  = SetName String
  | SetDescription String
  | SetClassification Classification

update: Msg -> BoundedContextCanvas -> BoundedContextCanvas
update msg canvas =
  case msg of
    SetName name ->
      { canvas | name = name}
      
    SetDescription description ->
      { canvas | description = description}

    SetClassification class ->
      { canvas | classification = Just class}
   
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

classificationToString: Classification -> String
classificationToString classification =
    case classification of
        Other value -> value
        Generic -> "Generic"
        Supporting -> "Supporting"
        Core -> "Core"

classificationParser: String -> Maybe Classification
classificationParser classification =
    case classification of
        "Generic" -> Just Generic
        "Supporting" -> Just Supporting
        "Core" -> Just Core
        "" -> Nothing
        value -> Just (Other value)