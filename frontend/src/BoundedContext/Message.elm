module BoundedContext.Message exposing(
    Message, Command, Event, Query,
    Messages, MessageCollection,
    noMessages,
    updateMessages,
    optionalMessagesDecoder)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Set exposing (Set)

import Api
import Http
import Url

import BoundedContext.BoundedContextId exposing(BoundedContextId)

type alias Message = String
type alias Command = Message
type alias Event = Message
type alias Query = Message

type alias MessageCollection = Set Message

-- TODO: should this be part of the BCC or part of message?
type alias Messages =
  { commandsHandled : Set Command
  , commandsSent : Set Command
  , eventsHandled : Set Event
  , eventsPublished : Set Event
  , queriesHandled : Set Query
  , queriesInvoked : Set Query
  }

noMessages : Messages
noMessages =
  { commandsHandled = Set.empty
  , commandsSent = Set.empty
  , eventsHandled = Set.empty
  , eventsPublished = Set.empty
  , queriesHandled = Set.empty
  , queriesInvoked = Set.empty
  }



updateMessages : Api.Configuration -> BoundedContextId -> Messages -> (Api.ApiResult Messages msg)
updateMessages configuration contextId messages =
  let
    api = Api.boundedContext contextId
    request toMsg =
      Http.request
        { method = "POST"
        , url = api |> Api.url configuration |> Url.toString |> (\c -> c ++ "/messages")
        , body = Http.jsonBody <|
            Encode.object [ messagesEncoder messages ]
        , expect = Http.expectJson toMsg messagesDecoder
        , timeout = Nothing
        , tracker = Nothing
        , headers = []
        }
  in
    request




messagesEncoder language = ("messages", encoder language)

messagesDecoder : Decode.Decoder Messages
messagesDecoder = Decode.at [ "messages"] decoder

optionalMessagesDecoder : Decode.Decoder (Messages -> b) -> Decode.Decoder b
optionalMessagesDecoder =
    JP.optional "messages" decoder noMessages


encoder : Messages -> Encode.Value
encoder messages =
  Encode.object
    [ ("commandsHandled", Encode.set Encode.string messages.commandsHandled)
    , ("commandsSent", Encode.set Encode.string messages.commandsSent)
    , ("eventsHandled", Encode.set Encode.string messages.eventsHandled)
    , ("eventsPublished", Encode.set Encode.string messages.eventsPublished)
    , ("queriesHandled", Encode.set Encode.string messages.queriesHandled)
    , ("queriesInvoked" , Encode.set Encode.string messages.queriesInvoked)
    ]


setDecoder : Decoder (Set.Set String)
setDecoder =
  Decode.map Set.fromList (Decode.list Decode.string)

decoder : Decoder Messages
decoder =
  Decode.succeed Messages
    |> JP.required "commandsHandled" setDecoder
    |> JP.required "commandsSent" setDecoder
    |> JP.required "eventsHandled" setDecoder
    |> JP.required "eventsPublished" setDecoder
    |> JP.required "queriesHandled" setDecoder
    |> JP.required "queriesInvoked" setDecoder
