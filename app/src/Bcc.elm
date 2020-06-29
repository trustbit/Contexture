module Bcc exposing (..)

import Set exposing(Set)
import Set as Set

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import BoundedContext exposing (BoundedContext)
import Dependency

-- MODEL

type DomainType
  = Core
  | Supporting
  | Generic
  | OtherDomainType String

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

type alias StrategicClassification =
    { domain : Maybe DomainType
    , business : List BusinessModel
    , evolution : Maybe Evolution
    }

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

type alias Dependencies =
  { suppliers : Dependency.DependencyMap
  , consumers : Dependency.DependencyMap
  }

type alias BoundedContextCanvas =
  { boundedContext : BoundedContext
  , description : String
  , classification : StrategicClassification
  , businessDecisions : BusinessDecisions
  , ubiquitousLanguage : UbiquitousLanguage
  , modelTraits : ModelTraits
  , messages : Messages
  , dependencies : Dependencies
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
  { suppliers = Dependency.emptyDependencies
  , consumers = Dependency.emptyDependencies
  }

initStrategicClassification =
  { domain = Nothing
  , business = []
  , evolution = Nothing
  }

init: BoundedContext -> BoundedContextCanvas
init context =
  { boundedContext = context
  , description = ""
  , classification = initStrategicClassification
  , businessDecisions = ""
  , ubiquitousLanguage = ""
  , modelTraits = ""
  , messages = initMessages ()
  , dependencies = initDependencies ()
  }

-- conversions

domainTypeToString: DomainType -> String
domainTypeToString classification =
  case classification of
      OtherDomainType value -> value
      Generic -> "Generic"
      Supporting -> "Supporting"
      Core -> "Core"

domainTypeParser: String -> Maybe DomainType
domainTypeParser classification =
  case classification of
      "Generic" -> Just Generic
      "Supporting" -> Just Supporting
      "Core" -> Just Core
      "" -> Nothing
      value -> Just (OtherDomainType value)

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


dependenciesEncoder : Dependencies -> Encode.Value
dependenciesEncoder dependencies =
  Encode.object
    [ ("suppliers", Dependency.dependencyEncoder dependencies.suppliers)
    , ("consumers", Dependency.dependencyEncoder dependencies.consumers)
    ]

strategicClassificationEncoder : StrategicClassification -> Encode.Value
strategicClassificationEncoder classification =
  Encode.object
    [ ("domainType", maybeStringEncoder domainTypeToString classification.domain)
    , ("businessModel", Encode.list (businessModelToString >> Encode.string)  classification.business)
    , ("evolution", maybeStringEncoder evolutionToString classification.evolution)
    ]

modelEncoder : BoundedContextCanvas -> Encode.Value
modelEncoder canvas =
  Encode.object
    [ ("name", Encode.string (canvas.boundedContext |> BoundedContext.name))
    , ("description", Encode.string canvas.description)
    , ("classification", strategicClassificationEncoder canvas.classification)
    , ("businessDecisions", Encode.string canvas.businessDecisions)
    , ("ubiquitousLanguage", Encode.string canvas.ubiquitousLanguage)
    , ("modelTraits", Encode.string canvas.modelTraits)
    , ("messages", messagesEncoder canvas.messages)
    , ("dependencies", dependenciesEncoder canvas.dependencies)
    ]

maybeEncoder : (t -> Encode.Value) -> Maybe t -> Encode.Value
maybeEncoder encoder value =
  case value of
    Just v -> encoder v
    Nothing -> Encode.null

maybeStringEncoder encoder value =
  maybeEncoder (encoder >> Encode.string) value

maybeStringDecoder : (String -> Maybe v) -> Decoder (Maybe v)
maybeStringDecoder parser =
  Decode.oneOf
    [ Decode.null Nothing
    , Decode.map parser Decode.string
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

dependenciesDecoder : Decoder Dependencies
dependenciesDecoder =
  Decode.succeed Dependencies
    |> JP.optional "suppliers" Dependency.dependencyDecoder Dependency.emptyDependencies
    |> JP.optional "consumers" Dependency.dependencyDecoder Dependency.emptyDependencies

businessModelDecoder : Decoder (List BusinessModel)
businessModelDecoder =
    let
      maybeListDecoder = Decode.list (Decode.map businessModelParser Decode.string)
      maybeAsList =
        List.concatMap (\li ->
          case li of
            Just value -> [value]
            Nothing -> []
        )
    in
      maybeListDecoder |> Decode.map maybeAsList

strategicClassificationDecoder : Decoder StrategicClassification
strategicClassificationDecoder =
  Decode.succeed StrategicClassification
    |> JP.optional "domainType" (maybeStringDecoder domainTypeParser) Nothing
    |> JP.optional "businessModel" businessModelDecoder []
    |> JP.optional "evolution" (maybeStringDecoder evolutionParser) Nothing


modelDecoder : Decoder BoundedContextCanvas
modelDecoder =
  Decode.succeed BoundedContextCanvas
    |> JP.custom BoundedContext.modelDecoder
    |> JP.optional "description" Decode.string ""
    |> JP.optional "classification" strategicClassificationDecoder initStrategicClassification
    |> JP.optional "businessDecisions" Decode.string ""
    |> JP.optional "ubiquitousLanguage" Decode.string ""
    |> JP.optional "modelTraits" Decode.string ""
    |> JP.optional "messages" messagesDecoder (initMessages ())
    |> JP.optional "dependencies" dependenciesDecoder (initDependencies ())
