module BoundedContext.Technical exposing (
  TechnicalDescription, Lifecycle, Deployment,
  modelDecoder, modelEncoder)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Url exposing (Url)

type alias Lifecycle =
  { issueTracker : Maybe Url
  , wiki : Maybe Url
  , repository : Maybe Url
  }

type alias Deployment =
  { healthCheck : Maybe Url
  , artifacts : Maybe Url
  }

type alias TechnicalDescription =
  { tools : Lifecycle
  , deployment : Deployment
  }

urlDecoder name =
  JP.optional name (Decode.map Url.fromString Decode.string) Nothing

urlEncoder url =
  case url of
    Just value -> value |> Url.toString |> Encode.string
    Nothing -> Encode.null

lifecycleDecoder : Decoder Lifecycle
lifecycleDecoder =
  Decode.succeed Lifecycle
    |> urlDecoder "issueTracker"
    |> urlDecoder "wiki"
    |> urlDecoder "repository"

deloymentDecoder : Decoder Deployment
deloymentDecoder =
  Decode.succeed Deployment
    |> urlDecoder "healthCheck"
    |> urlDecoder "artifacts"

modelDecoder : Decoder TechnicalDescription
modelDecoder =
  Decode.succeed TechnicalDescription
    |> JP.optional "tools" lifecycleDecoder { issueTracker = Nothing, wiki = Nothing, repository = Nothing }
    |> JP.optional "deployment" deloymentDecoder { healthCheck = Nothing, artifacts = Nothing }

lifecycleEncoder : Lifecycle -> Encode.Value
lifecycleEncoder model =
  Encode.object
    [ ("issueTracker", urlEncoder model.issueTracker)
    , ("wiki", urlEncoder model.wiki)
    , ("repository", urlEncoder model.repository)
    ]

deploymentEncoder : Deployment -> Encode.Value
deploymentEncoder model =
  Encode.object
    [ ("healthCheck", urlEncoder model.healthCheck)
    , ("artifacts", urlEncoder model.artifacts)
    ]


modelEncoder : TechnicalDescription -> Encode.Value
modelEncoder model =
  Encode.object
    [ ("tools", lifecycleEncoder model.tools)
    , ("deployment", deploymentEncoder model.deployment)
    ]