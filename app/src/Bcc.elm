module Bcc exposing (..)

import Url.Parser exposing (Parser, custom)

import Set exposing(Set)
import Dict exposing(Dict)

import Json.Encode as Encode
import Json.Decode as Decode
import Json.Decode exposing (Decoder)
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

type alias MessageCollection = Set Message

type alias Messages =
  { commandsHandled : Set Command
  , commandsSent : Set Command
  , eventsHandled : Set Event
  , eventsPublished : Set Event
  , queriesHandled : Set Query
  , queriesInvoked : Set Query
  }

type Relationship
  = AntiCorruptionLayer
  | OpenHostService
  | PublishedLanguage
  | SharedKernel
  | UpstreamDownstream
  | Conformist
  | Octopus
  | Partnership
  | CustomerSupplier

type alias System = String

type alias Dependency = (System, Maybe Relationship)

type alias DependencyMap = Dict System (Maybe Relationship)

type alias Dependencies = 
  { suppliers: DependencyMap
  , consumers: DependencyMap
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
  , dependencies: Dependencies
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

initDependencies : () -> Dependencies
initDependencies _ = 
  { suppliers = Dict.empty
  , consumers = Dict.empty
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
  , dependencies = initDependencies ()
  }

-- UPDATE

type Action t
  = Add t
  | Remove t

type alias DependencyAction = Action Dependency

type DependenciesMsg
  = Supplier DependencyAction
  | Consumer DependencyAction

type alias DependencyType = DependencyAction -> DependenciesMsg

type alias MessageAction = Action Message

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
  | ChangeDependencies DependenciesMsg

updateMessageAction : Action Message -> Set Message -> Set Message
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
    
updateDependencyAction : Action Dependency -> DependencyMap -> DependencyMap
updateDependencyAction action dependencies =
  case action of
    Add (system, relationship) ->
      Dict.insert system relationship dependencies
    Remove (system, _) ->
      Dict.remove system dependencies

updateDependencies : DependenciesMsg -> Dependencies -> Dependencies
updateDependencies msg model =
  case msg of
    Supplier dependency ->
      { model | suppliers = updateDependencyAction dependency model.suppliers }
    Consumer dependency ->
      { model | consumers = updateDependencyAction dependency model.consumers }

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

    ChangeDependencies m ->
      { canvas | dependencies = updateDependencies m canvas.dependencies }
   
-- conversions

idToString : BoundedContextId -> String
idToString bccId =
  case bccId of
    BoundedContextId id -> String.fromInt id

idParser : Parser (BoundedContextId -> a) a
idParser =
    custom "BCCID" <|
        \bccId ->
            Maybe.map BoundedContextId (String.toInt bccId)

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

relationshipToString: Relationship -> String
relationshipToString relationship =
  case relationship of
    AntiCorruptionLayer -> "AntiCorruptionLayer"
    OpenHostService -> "OpenHostService"
    PublishedLanguage -> "PublishedLanguage"
    SharedKernel -> "SharedKernel"
    UpstreamDownstream -> "UpstreamDownstream"
    Conformist -> "Conformist"
    Octopus -> "Octopus"
    Partnership -> "Partnership"
    CustomerSupplier -> "CustomerSupplier"
  
relationshipParser: String -> Maybe Relationship
relationshipParser relationship =
  case relationship of
    "AntiCorruptionLayer" -> Just AntiCorruptionLayer
    "OpenHostService" -> Just OpenHostService
    "PublishedLanguage" -> Just PublishedLanguage
    "SharedKernel" -> Just SharedKernel
    "UpstreamDownstream" -> Just UpstreamDownstream
    "Conformist" -> Just Conformist
    "Octopus" -> Just Octopus
    "Partnership" -> Just Partnership
    "CustomerSupplier" -> Just CustomerSupplier
    _ -> Nothing

-- encoders

idDecoder : Decoder BoundedContextId
idDecoder =
  Decode.map BoundedContextId Decode.int

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

dependenciesEncoder : Dependencies -> Encode.Value
dependenciesEncoder dependencies =
  Encode.object
    [ ("suppliers", Encode.dict identity (maybeStringEncoder relationshipToString) dependencies.suppliers )
    , ("consumers", Encode.dict identity (maybeStringEncoder relationshipToString) dependencies.consumers )
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
    , ("dependencies", dependenciesEncoder canvas.dependencies)
    ]

maybeStringEncoder : (t -> String) -> Maybe t -> Encode.Value
maybeStringEncoder encoder value =
  case value of
    Just v -> Encode.string (encoder v)
    Nothing -> Encode.null

maybeStringDecoder : (String -> Maybe v) -> Decoder (Maybe v)
maybeStringDecoder parser =
  Decode.map parser Decode.string

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

dependenciesDecoder : Decoder Dependencies
dependenciesDecoder =
  Decode.succeed Dependencies
    |> JP.optional "suppliers" (Decode.dict (maybeStringDecoder relationshipParser)) Dict.empty
    |> JP.optional "consumers" (Decode.dict (maybeStringDecoder relationshipParser)) Dict.empty

modelDecoder : Decoder BoundedContextCanvas
modelDecoder =
  Decode.succeed BoundedContextCanvas
    |> JP.required "name" Decode.string
    |> JP.optional "description" Decode.string ""
    |> JP.optional "classification" (maybeStringDecoder classificationParser) Nothing
    |> JP.optional "businessModel" (maybeStringDecoder businessModelParser) Nothing
    |> JP.optional "evolution" (maybeStringDecoder evolutionParser) Nothing
    |> JP.optional "businessDecisions" Decode.string ""
    |> JP.optional "ubiquitousLanguage" Decode.string ""
    |> JP.optional "modelTraits" Decode.string ""
    |> JP.optional "messages" messagesDecoder (initMessages ())
    |> JP.optional "dependencies" dependenciesDecoder (initDependencies ())
