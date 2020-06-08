module Domain exposing (
  DomainId(..), Domain, Model, init,
  Msg(..),update,
  idToString, idParser, idEncoder, idDecoder
  )

import Json.Decode as Decode
import Json.Encode as Encode
import Url.Parser exposing (Parser, custom)

-- MODEL

type DomainId
  = DomainId Int

type alias Domain =
  { name: String
  , vision: String }

type alias Model = Domain

init : () -> Domain
init _ =
    { name = ""
    , vision = "" }

-- UPDATE

type Msg
  = SetName String
  | SetVision String

update : Msg -> Model -> Model
update msg model =
  case msg of
    SetName name ->
      { model | name = name}
    SetVision vision->
      { model | vision = vision}

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

idFromString : String -> Maybe DomainId
idFromString value =
  value
  |> String.toInt
  |> Maybe.map DomainId

idFromStringSuccess : String -> Decode.Decoder DomainId
idFromStringSuccess value =
  case idFromString value of
    Just id -> Decode.succeed id
    Nothing -> Decode.fail ("Could not decode into DomainId " ++ value)

idParser : Parser (DomainId -> a) a
idParser =
    custom "DOMAINID" <|
        \domainId ->
            Maybe.map DomainId (String.toInt domainId)

idDecoder : Decode.Decoder DomainId
idDecoder =
  Decode.oneOf
    [ Decode.map DomainId Decode.int
    , Decode.string |> Decode.andThen idFromStringSuccess]


idEncoder : DomainId -> Encode.Value
idEncoder value =
  Encode.int (extractInt value)
