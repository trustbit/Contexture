module Bcc exposing (..)

import Url.Parser exposing (Parser, custom)

import Set exposing(Set)
import Json.Encode as Encode
import Json.Decode exposing (Decoder, map2, field, string, int, at, nullable, list)
import Json.Decode.Pipeline as JP

-- MODEL

type BoundedContextId 
  = BoundedContextId Int

type Classification
  = Core
  | Supporting
  | Generic
  | OtherClassification String

type BusinessModel 
  = Revenue
  | Engagement
  | Compliance
  | CostReduction
  | OtherBusinessModel String

type Evolution
  = Genesis
  | CustomBuilt
  | Product
  | Commodity

type alias BusinessDecisions = String
type alias UbiquitousLanguage = String
type alias ModelTraits = String

type alias Message = String
type alias Command = Message
type alias Event = Message
type alias Query = Message

type alias Messages =
    { commandsHandled : Set Command
    , commandsSent : Set Command
    , eventsHandled : Set Event
    , eventsPublished : Set Event
    , queriesHandled : Set Query
    , queriesInvoked : Set Query
    }
type alias BoundedContextCanvas = 
  { name: String
  , description: String
  , classification : Maybe Classification
  , businessModel: Maybe BusinessModel
  , evolution: Maybe Evolution
  , businessDecisions: BusinessDecisions
  , ubiquitousLanguage: UbiquitousLanguage
  , modelTraits: ModelTraits
  , messages: Messages
  }


initMessages : () -> Messages
initMessages _ =
  { commandsHandled = Set.empty
  , commandsSent = Set.empty
  , eventsHandled = Set.empty
  , eventsPublished = Set.empty
  , queriesHandled = Set.empty
  , queriesInvoked = Set.empty
  }

init: () -> BoundedContextCanvas
init _ = 
  { name = ""
  , description = ""
  , classification = Nothing
  , businessModel = Nothing
  , evolution = Nothing
  , businessDecisions = ""
  , ubiquitousLanguage = ""
  , modelTraits = ""
  , messages = initMessages ()
  }

-- UPDATE

type MessageAction
  = Add Message
  | Remove Message

type MessageMsg
  = CommandHandled MessageAction
  | CommandSent MessageAction
  | EventsHandled MessageAction
  | EventsPublished MessageAction
  | QueriesHandled MessageAction
  | QueriesInvoked MessageAction

type Msg
  = SetName String
  | SetDescription String
  | SetClassification Classification
  | SetBusinessModel BusinessModel
  | SetEvolution Evolution
  | SetBusinessDecisions BusinessDecisions
  | SetUbiquitousLanguage UbiquitousLanguage
  | SetModelTraits ModelTraits
  | ChangeMessages MessageMsg

updateMessageAction : MessageAction -> Set Message -> Set Message
updateMessageAction action messages =
  case action of
    Add m ->
      Set.insert m messages
    Remove m ->
      Set.remove m messages

updateMessages : MessageMsg -> Messages -> Messages
updateMessages msg model =
  case msg of
    CommandHandled cmd ->
      { model | commandsHandled = updateMessageAction cmd model.commandsHandled }
    CommandSent cmd ->
      { model | commandsSent = updateMessageAction cmd model.commandsSent }
    EventsHandled event ->
      { model | eventsHandled = updateMessageAction event model.eventsHandled }
    EventsPublished event ->
      { model | eventsPublished = updateMessageAction event model.eventsPublished }
    QueriesHandled event ->
      { model | queriesHandled = updateMessageAction event model.queriesHandled }
    QueriesInvoked event ->
      { model | queriesInvoked = updateMessageAction event model.queriesInvoked }
    
update: Msg -> BoundedContextCanvas -> BoundedContextCanvas
update msg canvas =
  case msg of
    SetName name ->
      { canvas | name = name}
      
    SetDescription description ->
      { canvas | description = description}

    SetClassification class ->
      { canvas | classification = Just class}
    SetBusinessModel business ->
      { canvas | businessModel = Just business}
    SetEvolution evo ->
      { canvas | evolution = Just evo}

    SetBusinessDecisions decisions ->
      { canvas | businessDecisions = decisions}
    SetUbiquitousLanguage language ->
      { canvas | ubiquitousLanguage = language}

    SetModelTraits traits ->
      { canvas | modelTraits = traits}

    ChangeMessages m ->
      { canvas | messages = updateMessages m canvas.messages }
   
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
      OtherClassification value -> value
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
      value -> Just (OtherClassification value)


businessModelToString: BusinessModel -> String
businessModelToString businessModel =
  case businessModel of
      OtherBusinessModel value -> value
      Revenue -> "Revenue"
      Engagement -> "Engagement"
      Compliance -> "Compliance"
      CostReduction -> "CostReduction"

businessModelParser: String -> Maybe BusinessModel
businessModelParser businessModel =
  case businessModel of
      "Revenue" -> Just Revenue
      "Engagement" -> Just Engagement
      "Compliance" -> Just Compliance
      "CostReduction" -> Just CostReduction
      "" -> Nothing
      value -> Just (OtherBusinessModel value)


evolutionToString: Evolution -> String
evolutionToString evolution =
  case evolution of
      Genesis -> "Genesis"
      CustomBuilt -> "CustomBuilt"
      Product -> "Product"
      Commodity -> "Commodity"
  
evolutionParser: String -> Maybe Evolution
evolutionParser evolution =
  case evolution of
      "Genesis" -> Just Genesis
      "CustomBuilt" -> Just CustomBuilt
      "Product" -> Just Product
      "Commodity" -> Just Commodity
      _ -> Nothing

-- encoders
        
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

modelEncoder : BoundedContextCanvas -> Encode.Value
modelEncoder canvas = 
  Encode.object
    [ ("name", Encode.string canvas.name)
    , ("description", Encode.string canvas.description)
    , ("classification", maybeStringEncoder classificationToString canvas.classification)
    , ("businessModel", maybeStringEncoder businessModelToString canvas.businessModel)
    , ("evolution", maybeStringEncoder evolutionToString canvas.evolution)
    , ("businessDecisions", Encode.string canvas.businessDecisions)
    , ("ubiquitousLanguage", Encode.string canvas.ubiquitousLanguage)
    , ("modelTraits", Encode.string canvas.modelTraits)
    , ("messages", messagesEncoder canvas.messages)
    ]

maybeStringEncoder : (t -> String) -> Maybe t -> Encode.Value
maybeStringEncoder encoder value =
  case value of
    Just v -> Encode.string (encoder v)
    Nothing -> Encode.null

maybeStringDecoder : (String -> Maybe v) -> Decoder (Maybe v)
maybeStringDecoder parser =
  Json.Decode.map parser string

setDecoder : Decoder (Set.Set String)
setDecoder =
  Json.Decode.map Set.fromList (Json.Decode.list string) 

messagesDecoder : Decoder Messages
messagesDecoder =
  Json.Decode.succeed Messages
    |> JP.required "commandsHandled" setDecoder
    |> JP.required "commandsSent" setDecoder
    |> JP.required "eventsHandled" setDecoder
    |> JP.required "eventsPublished" setDecoder
    |> JP.required "queriesHandled" setDecoder
    |> JP.required "queriesInvoked" setDecoder

modelDecoder : Decoder BoundedContextCanvas
modelDecoder =
  Json.Decode.succeed BoundedContextCanvas
    |> JP.required "name" string
    |> JP.optional "description" string ""
    |> JP.optional "classification" (maybeStringDecoder classificationParser) Nothing
    |> JP.optional "businessModel" (maybeStringDecoder businessModelParser) Nothing
    |> JP.optional "evolution" (maybeStringDecoder evolutionParser) Nothing
    |> JP.optional "businessDecisions" string ""
    |> JP.optional "ubiquitousLanguage" string ""
    |> JP.optional "modelTraits" string ""
    |> JP.optional "messages" messagesDecoder (initMessages ())
