module BoundedContext.Canvas exposing (..)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Key as Key
import BoundedContext exposing (BoundedContext)
import BoundedContext.StrategicClassification as StrategicClassification exposing(StrategicClassification)
import BoundedContext.Message as Message exposing (Messages)
import BoundedContext.UbiquitousLanguage as UbiquitousLanguage exposing (UbiquitousLanguage)
import BoundedContext.BusinessDecision exposing (BusinessDecision)
import BoundedContext.DomainRoles exposing (DomainRole)

-- MODEL

type alias BoundedContextCanvas =
  { description : String
  , classification : StrategicClassification
  , businessDecisions : List BusinessDecision
  , ubiquitousLanguage : UbiquitousLanguage
  , domainRoles : List DomainRole
  , messages : Messages
  }


init: BoundedContext -> BoundedContextCanvas
init context =
  { description = ""
  , classification = StrategicClassification.noClassification
  , businessDecisions = []
  , ubiquitousLanguage = UbiquitousLanguage.noLanguageTerms
  , domainRoles = []
  , messages = Message.noMessages
  }

-- encoders


modelEncoder : BoundedContext -> BoundedContextCanvas -> Encode.Value
modelEncoder context canvas =
  Encode.object
    [ ("name", Encode.string (context |> BoundedContext.name))
    , ("key",
        case context |> BoundedContext.key of
          Just v -> Key.keyEncoder v
          Nothing -> Encode.null
      )
    , ("description", Encode.string canvas.description)
    , ("classification", StrategicClassification.encoder canvas.classification)
    , ("businessDecisions", BoundedContext.BusinessDecision.modelsEncoder canvas.businessDecisions)
    , ("ubiquitousLanguage", UbiquitousLanguage.modelEncoder canvas.ubiquitousLanguage)
    , ("domainRoles", BoundedContext.DomainRoles.modelsEncoder canvas.domainRoles)
    , ("messages", Message.messagesEncoder canvas.messages)
    ]

maybeStringDecoder : (String -> Maybe v) -> Decoder (Maybe v)
maybeStringDecoder parser =
  Decode.oneOf
    [ Decode.null Nothing
    , Decode.map parser Decode.string
    ]


modelDecoder : Decoder BoundedContextCanvas
modelDecoder =
  Decode.succeed BoundedContextCanvas
    |> JP.optional "description" Decode.string ""
    |> JP.optional "classification" StrategicClassification.decoder StrategicClassification.noClassification
    |> JP.optional "businessDecisions" BoundedContext.BusinessDecision.modelsDecoder []
    |> JP.optional "ubiquitousLanguage" UbiquitousLanguage.modelDecoder UbiquitousLanguage.noLanguageTerms
    |> JP.optional "domainRoles" BoundedContext.DomainRoles.modelsDecoder []
    |> JP.optional "messages" Message.messagesDecoder Message.noMessages
