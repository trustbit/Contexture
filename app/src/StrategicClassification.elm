module StrategicClassification exposing (
    StrategicClassification, DomainType(..),BusinessModel(..),Evolution(..),
    noClassification,
    encoder, decoder,
    Description, domainDescription, businessDescription, evolutionDescription
    )

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP


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

noClassification : StrategicClassification
noClassification =
  { domain = Nothing
  , business = []
  , evolution = Nothing
  }

-- this should be placed in some 'localization component'?
type alias Description =
    { name : String
    , description : String
    }

description name desc = { name = name, description = desc }

domainDescription : DomainType -> Description
domainDescription domainType =
    case domainType of
       Core -> description "Core" "A key strategic initiative"
       Supporting -> description "Supporting" "Necessary but not a differentiator"
       Generic -> description "Generic" "A common capability found in many domains"
       OtherDomainType other -> description other "An unspecific domain"

businessDescription : BusinessModel -> Description
businessDescription business =
    case business of
        Revenue -> description "Revenue" "People pay directly for this"
        Engagement -> description "Engagement" "Users like it but they don't pay for it"
        Compliance -> description "Compliance" "Protects your business reputation and existence"
        CostReduction -> description "Cost reduction" "Helps your business to reduce cost or effort"
        OtherBusinessModel other -> description other "An unspecific business model"

evolutionDescription : Evolution -> Description
evolutionDescription evolution =
    case evolution of
        Genesis -> description "Genesis" "New unexplored domain"
        CustomBuilt -> description "Custom built" "Companies are building their own versions"
        Product -> description "Product" "Off-the-shelf versions exist with differentiation"
        Commodity -> description "Commodity" "Highly-standardised versions exist"

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

-- encoder


maybeEncoder : (t -> Encode.Value) -> Maybe t -> Encode.Value
maybeEncoder encode value =
  case value of
    Just v -> encode v
    Nothing -> Encode.null

maybeStringEncoder encode value =
  maybeEncoder (encode >> Encode.string) value

maybeStringDecoder : (String -> Maybe v) -> Decoder (Maybe v)
maybeStringDecoder parser =
  Decode.oneOf
    [ Decode.null Nothing
    , Decode.map parser Decode.string
    ]

encoder : StrategicClassification -> Encode.Value
encoder classification =
  Encode.object
    [ ("domainType", maybeStringEncoder domainTypeToString classification.domain)
    , ("businessModel", Encode.list (businessModelToString >> Encode.string)  classification.business)
    , ("evolution", maybeStringEncoder evolutionToString classification.evolution)
    ]


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

decoder : Decoder StrategicClassification
decoder =
  Decode.succeed StrategicClassification
    |> JP.optional "domainType" (maybeStringDecoder domainTypeParser) Nothing
    |> JP.optional "businessModel" businessModelDecoder []
    |> JP.optional "evolution" (maybeStringDecoder evolutionParser) Nothing