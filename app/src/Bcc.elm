module Bcc exposing (..)

import Url.Parser exposing (Parser, custom)

import Http
import Json.Decode exposing (Decoder, map2, field, string, int, at, nullable)

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

type alias BoundedContextCanvas = 
  { name: String
  , description: String
  , classification : Maybe Classification
  , businessModel: Maybe BusinessModel
  , evolution: Maybe Evolution
  , businessDecisions: String
  , ubiquitousLanguage: String
  }

init: () -> BoundedContextCanvas
init _ = 
  { name = ""
  , description = ""
  , classification = Nothing
  , businessModel = Nothing
  , evolution = Nothing
  , businessDecisions = ""
  , ubiquitousLanguage = "" }

-- UPDATE

type Msg
  = SetName String
  | SetDescription String
  | SetClassification Classification
  | SetBusinessModel BusinessModel
  | SetEvolution Evolution
  | SetBusinessDecisions String
  | SetUbiquitousLanguage String

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