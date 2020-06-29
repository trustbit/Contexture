module BoundedContext exposing (
  BoundedContextId, BoundedContext, Problem, 
  changeName, name, isNameValid,
  domain,
  id, idToString, idFromString,idParser,
  idFieldDecoder, nameFieldDecoder, modelDecoder)

import Url.Parser exposing (Parser, custom)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Domain

-- MODEL

type BoundedContextId
  = BoundedContextId Int

type Problem
  = NameInvalid

type BoundedContext
  = BoundedContext Internals
    
type alias Internals =
  { id : BoundedContextId
  , domain : Domain.DomainId
  , name : String
  }

isNameValid : String -> Bool
isNameValid couldBeName =
  String.length couldBeName > 0

changeName : String -> BoundedContext -> Result Problem BoundedContext
changeName couldBeName (BoundedContext context) =
  if isNameValid couldBeName
  then 
    { context | name = couldBeName }
    |> BoundedContext
    |> Ok
  else 
    Err NameInvalid

id : BoundedContext -> BoundedContextId
id (BoundedContext context) =
  context.id

name : BoundedContext -> String
name (BoundedContext context) =
  context.name

domain : BoundedContext -> Domain.DomainId
domain (BoundedContext context) =
  context.domain

idToString : BoundedContextId -> String
idToString bccId =
  case bccId of
    BoundedContextId bcId -> String.fromInt bcId

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
  ( Decode.succeed Internals
    |> JP.custom idFieldDecoder
    |> JP.custom domainIdFieldDecoder
    |> JP.custom nameFieldDecoder
  ) |> Decode.map BoundedContext



modelEncoder : BoundedContext -> Encode.Value
modelEncoder (BoundedContext canvas) =
  Encode.object
    [ ("domainId", Domain.idEncoder canvas.domain)
    , ("name", Encode.string canvas.name)
    ]