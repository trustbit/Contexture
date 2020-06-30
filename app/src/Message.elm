module Message exposing(
    Message, Command, Event, Query,
    Messages, MessageCollection,
    noMessages,
    messagesEncoder, messagesDecoder)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Set exposing (Set)

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


messagesEncoder : Messages -> Encode.Value
messagesEncoder messages =
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

messagesDecoder : Decoder Messages
messagesDecoder =
  Decode.succeed Messages
    |> JP.required "commandsHandled" setDecoder
    |> JP.required "commandsSent" setDecoder
    |> JP.required "eventsHandled" setDecoder
    |> JP.required "eventsPublished" setDecoder
    |> JP.required "queriesHandled" setDecoder
    |> JP.required "queriesInvoked" setDecoder
