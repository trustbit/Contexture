module BoundedContext exposing (..)

import Url.Parser exposing (Parser, custom)

import Set exposing(Set)
import Set as Set
import Dict exposing(Dict)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Domain

-- MODEL

type BoundedContextId
  = BoundedContextId Int

type alias BoundedContext = 
    { id : BoundedContextId
    , domain : Domain.DomainId
    , name : String
    }


type Msg
  = SetName String

update: Msg -> BoundedContext -> BoundedContext
update msg canvas =
  case msg of
    SetName name ->
      { canvas | name = name}

idToString : BoundedContextId -> String
idToString bccId =
  case bccId of
    BoundedContextId id -> String.fromInt id

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


idFieldDecoder : Decoder BoundedContextId
idFieldDecoder =
  Decode.field "id" idDecoder


nameFieldDecoder : Decoder String
nameFieldDecoder =
  Decode.field "name" Decode.string


domainIdFieldDecoder : Decoder Domain.DomainId
domainIdFieldDecoder =
  Decode.field "domainId" Domain.idDecoder


modelDecoder : Decoder BoundedContext
modelDecoder =
  Decode.succeed BoundedContext
    |> JP.custom idFieldDecoder
    |> JP.custom domainIdFieldDecoder
    |> JP.custom nameFieldDecoder


modelEncoder : BoundedContext -> Encode.Value
modelEncoder canvas =
  Encode.object
    [ ("domainId", Domain.idEncoder canvas.domain)
    , ("name", Encode.string canvas.name)
    ]