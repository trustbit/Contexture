module Bcc exposing (..)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Http
import Url exposing (Url)

import BoundedContext exposing (BoundedContext)
import Dependency
import StrategicClassification exposing(StrategicClassification)
import Message exposing (Messages)

-- MODEL

type alias BusinessDecisions = String
type alias UbiquitousLanguage = String
type alias ModelTraits = String

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

-- TODO: should this be part of the BCC or part of message?
type alias Dependencies =
  { suppliers : Dependency.DependencyMap
  , consumers : Dependency.DependencyMap
  }



initDependencies : Dependencies
initDependencies =
  { suppliers = Dependency.emptyDependencies
  , consumers = Dependency.emptyDependencies
  }

init: BoundedContext -> BoundedContextCanvas
init context =
  { boundedContext = context
  , description = ""
  , classification = StrategicClassification.noClassification
  , businessDecisions = ""
  , ubiquitousLanguage = ""
  , modelTraits = ""
  , messages = Message.noMessages
  , dependencies = initDependencies
  }

-- encoders

dependenciesEncoder : Dependencies -> Encode.Value
dependenciesEncoder dependencies =
  Encode.object
    [ ("suppliers", Dependency.dependencyEncoder dependencies.suppliers)
    , ("consumers", Dependency.dependencyEncoder dependencies.consumers)
    ]

modelEncoder : BoundedContextCanvas -> Encode.Value
modelEncoder canvas =
  Encode.object
    [ ("name", Encode.string (canvas.boundedContext |> BoundedContext.name))
    , ("description", Encode.string canvas.description)
    , ("classification", StrategicClassification.encoder canvas.classification)
    , ("businessDecisions", Encode.string canvas.businessDecisions)
    , ("ubiquitousLanguage", Encode.string canvas.ubiquitousLanguage)
    , ("modelTraits", Encode.string canvas.modelTraits)
    , ("messages", Message.messagesEncoder canvas.messages)
    , ("dependencies", dependenciesEncoder canvas.dependencies)
    ]

maybeStringDecoder : (String -> Maybe v) -> Decoder (Maybe v)
maybeStringDecoder parser =
  Decode.oneOf
    [ Decode.null Nothing
    , Decode.map parser Decode.string
    ]

dependenciesDecoder : Decoder Dependencies
dependenciesDecoder =
  Decode.succeed Dependencies
    |> JP.optional "suppliers" Dependency.dependencyDecoder Dependency.emptyDependencies
    |> JP.optional "consumers" Dependency.dependencyDecoder Dependency.emptyDependencies



modelDecoder : Decoder BoundedContextCanvas
modelDecoder =
  Decode.succeed BoundedContextCanvas
    |> JP.custom BoundedContext.modelDecoder
    |> JP.optional "description" Decode.string ""
    |> JP.optional "classification" StrategicClassification.decoder StrategicClassification.noClassification
    |> JP.optional "businessDecisions" Decode.string ""
    |> JP.optional "ubiquitousLanguage" Decode.string ""
    |> JP.optional "modelTraits" Decode.string ""
    |> JP.optional "messages" Message.messagesDecoder Message.noMessages
    |> JP.optional "dependencies" dependenciesDecoder initDependencies

-- load

-- load : Url -> BoundedContext -> (Result Http.Error BoundedContextCanvas -> msg) -> Cmd msg
-- load base boundedContext toMsg =
--   Http.get
--     { url = Url.toString { base | path = base.path ++ "/bccs/" ++ (boundedContext |> BoundedContext.id |> BoundedContext.idToString ) }
--     , expect = Http.expectJson toMsg modelDecoder
--     }

-- saveBCC: Url.Url -> EditingCanvas -> Cmd Msg
-- saveBCC url model =
--   let
--     c = model.canvas
--     canvas =
--       { c
--       | dependencies = model.addingDependencies |> Dependencies.asDependencies
--       , messages = model.addingMessage |> Messages.asMessages
--       }
--   in
--     Http.request
--       { method = "PATCH"
--       , headers = []
--       , url = Url.toString url
--       , body = Http.jsonBody <| Bcc.modelEncoder canvas
--       , expect = Http.expectWhatever Saved
--       , timeout = Nothing
--       , tracker = Nothing
--       }

-- deleteBCC: Model -> Cmd Msg
-- deleteBCC model =
--     Http.request
--       { method = "DELETE"
--       , headers = []
--       , url = Url.toString model.self
--       , body = Http.emptyBody
--       , expect = Http.expectWhatever Deleted
--       , timeout = Nothing
--       , tracker = Nothing
--       }
