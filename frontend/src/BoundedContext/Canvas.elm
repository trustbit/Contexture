module BoundedContext.Canvas exposing (
  BoundedContextCanvas,
  init,
  modelDecoder)

import Json.Decode as Decode exposing (Decoder)

import BoundedContext.StrategicClassification as StrategicClassification exposing(StrategicClassification)
import BoundedContext.Message as Message exposing (Messages)
import BoundedContext.UbiquitousLanguage as UbiquitousLanguage exposing (UbiquitousLanguage)
import BoundedContext.BusinessDecision exposing (BusinessDecision)
import BoundedContext.DomainRoles exposing (DomainRoles)
import BoundedContext.BusinessDecision
import BoundedContext.DomainRoles
import BoundedContext.UbiquitousLanguage
import BoundedContext.StrategicClassification
import BoundedContext.Description
import BoundedContext.Message

-- MODEL

type alias BoundedContextCanvas =
  { description : String
  , classification : StrategicClassification
  , businessDecisions : List BusinessDecision
  , ubiquitousLanguage : UbiquitousLanguage
  , domainRoles : DomainRoles
  , messages : Messages
  }


init: BoundedContextCanvas
init =
  { description = ""
  , classification = StrategicClassification.noClassification
  , businessDecisions = []
  , ubiquitousLanguage = UbiquitousLanguage.noLanguageTerms
  , domainRoles = []
  , messages = Message.noMessages
  }


modelDecoder : Decoder BoundedContextCanvas
modelDecoder =
  Decode.succeed BoundedContextCanvas
    |> BoundedContext.Description.optionalDescriptionDecoder
    |> BoundedContext.StrategicClassification.optionalStategicClassificationDecoder
    |> BoundedContext.BusinessDecision.optionalBusinessDecisionsDecoder
    |> BoundedContext.UbiquitousLanguage.optionalUbiquitousLanguageDecoder
    |> BoundedContext.DomainRoles.optionalDomainRolesDecoder
    |> BoundedContext.Message.optionalMessagesDecoder