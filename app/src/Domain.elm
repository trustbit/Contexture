module Domain exposing (
  DomainId(..), Domain, Model, init,
  Msg(..),update,ifNameValid,
  idToString, idParser, idEncoder, idDecoder,
  domainDecoder, domainsDecoder, modelEncoder, idFieldDecoder, nameFieldDecoder
  )

import Json.Decode as Decode exposing(Decoder)
import Json.Decode.Pipeline as JP
import Json.Encode as Encode
import Url.Parser exposing (Parser, custom)

-- MODEL

type DomainId
  = DomainId Int

type alias Domain =
  { id : DomainId
  , name: String
  , vision: String
  , parentDomain: Maybe DomainId
  }

type alias Model = Domain

init : () -> Domain
init _ =
    { id = DomainId(-1)
    , name = ""
    , vision = "" 
    , parentDomain = Nothing}

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

ifValid : (model -> Bool) -> (model -> result) -> (model -> result) -> model -> result
ifValid predicate trueRenderer falseRenderer model =
  if predicate model then
    trueRenderer model
  else
    falseRenderer model

ifNameValid =
  ifValid (\name -> String.length name <= 0)


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

nameFieldDecoder : Decoder String
nameFieldDecoder =
  Decode.field "name" Decode.string

idFieldDecoder : Decoder DomainId
idFieldDecoder =
  Decode.field "id" idDecoder

domainDecoder: Decoder Domain
domainDecoder =
  Decode.succeed Domain
    |> JP.custom idFieldDecoder
    |> JP.custom nameFieldDecoder
    |> JP.optional "vision" Decode.string ""
    |> JP.optional "domainId" (Decode.maybe idDecoder) Nothing

domainsDecoder: Decode.Decoder (List Domain)
domainsDecoder =
  Decode.list domainDecoder

modelEncoder : Domain -> Encode.Value
modelEncoder model =
    Encode.object
        [ ("name", Encode.string model.name)
        , ("vision", Encode.string model.vision)
        ]