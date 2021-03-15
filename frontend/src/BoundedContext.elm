module BoundedContext exposing (
  BoundedContext, Problem, Name,
  changeName, name, isName,
  domain, id, key, changeKey,
  move, remove, assignKey,
  newBoundedContext,
  idFieldDecoder, nameFieldDecoder, modelDecoder)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Http
import Url
import Api exposing (ApiResult)

import Key exposing (Key)
import Domain
import Domain.DomainId as Domain exposing (DomainId, idEncoder)
import BoundedContext.BoundedContextId exposing (BoundedContextId, idDecoder)

-- MODEL

type Name
  = Name String

type Problem
  = NameInvalid

type BoundedContext
  = BoundedContext Internals

type alias Internals =
  { id : BoundedContextId
  , domain : DomainId
  , name : String
  , key : Maybe Key
  }

isName : String -> Result Problem Name
isName couldBeName =
  if String.length couldBeName > 0
  then Ok (Name couldBeName)
  else Err NameInvalid


changeKey : Maybe Key -> BoundedContext -> BoundedContext
changeKey aKey (BoundedContext context) =
  BoundedContext { context | key = aKey }

id : BoundedContext -> BoundedContextId
id (BoundedContext context) =
  context.id


name : BoundedContext -> String
name (BoundedContext context) =
  context.name


domain : BoundedContext -> DomainId
domain (BoundedContext context) =
  context.domain


key : BoundedContext -> Maybe Key
key (BoundedContext context) =
  context.key

idFieldDecoder : Decoder BoundedContextId
idFieldDecoder =
  Decode.field "id" idDecoder

nameFieldDecoder : Decoder String
nameFieldDecoder =
  Decode.field "name" Decode.string


parentDomainIdFieldDecoder : Decoder DomainId
parentDomainIdFieldDecoder =
  Decode.field "parentDomainId" Domain.idDecoder


parentDomainIdEncoder domainId =
  ("parentDomainId", idEncoder domainId)

nameFieldEncoder bcName =
   ("name", Encode.string bcName)

modelDecoder : Decoder BoundedContext
modelDecoder =
  ( Decode.succeed Internals
    |> JP.custom idFieldDecoder
    |> JP.custom parentDomainIdFieldDecoder
    |> JP.custom nameFieldDecoder
    |> JP.optional "key" (Decode.maybe Key.keyDecoder) Nothing
  ) |> Decode.map BoundedContext


remove : Api.Configuration -> BoundedContextId -> ApiResult () msg
remove base contextId =
  let
    request toMsg =
      Http.request
      { method = "DELETE"
      , headers = []
      , url = contextId |> Api.boundedContext |> Api.url base |> Url.toString
      , body = Http.emptyBody
      , expect = Http.expectWhatever toMsg
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request

move : Api.Configuration -> BoundedContextId -> DomainId -> ApiResult () msg
move base contextId targetDomain =
  let
    request toMsg =
      Http.request
      { method = "POST"
      , headers = []
      , url = contextId |> Api.boundedContext |> Api.url base |> Url.toString |> (\c -> c ++ "/move")
      , body = Http.jsonBody <| Encode.object[ parentDomainIdEncoder targetDomain ]
      , expect = Http.expectWhatever toMsg
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request

assignKey : Api.Configuration -> BoundedContextId -> Maybe Key -> ApiResult BoundedContext msg
assignKey base contextId contextKey =
  let
    encodedKey =
      case contextKey of
        Just v -> Key.keyEncoder v
        Nothing -> Encode.null
    request toMsg =
      Http.request
      { method = "POST"
      , headers = []
      , url = contextId |> Api.boundedContext |> Api.url base |> Url.toString |> (\c -> c ++ "/key")
      , body = Http.jsonBody <| Encode.object[ ("key", encodedKey) ]
      , expect = Http.expectJson toMsg modelDecoder
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request

  
newBoundedContext : Api.Configuration -> DomainId -> String -> ApiResult BoundedContext msg
newBoundedContext config domainId contextName =
  let
    request toMsg =
      Http.post
        { url = Api.boundedContexts domainId |> Api.url config |> Url.toString
        , body = Http.jsonBody <| Encode.object [ nameFieldEncoder contextName ]
        , expect = Http.expectJson toMsg modelDecoder
        }
  in
    request


changeName : Api.Configuration -> BoundedContextId -> Name -> ApiResult BoundedContext msg
changeName config contextId (Name contextName) =
  let
    request toMsg =
      Http.request
      { method = "POST"
      , headers = []
      , url = contextId |> Api.boundedContext |> Api.url config |> Url.toString |> (\c -> c ++ "/rename")
      , body = Http.jsonBody <| Encode.object [  nameFieldEncoder contextName ]
      , expect = Http.expectJson toMsg modelDecoder
      , timeout = Nothing
      , tracker = Nothing
      }
  in
    request
