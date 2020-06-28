module Dependency exposing (..)

import Dict exposing(Dict)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)

import BoundedContext exposing (BoundedContextId, idFromString, idToString)
import Domain

type RelationshipPattern
  = AntiCorruptionLayer
  | OpenHostService
  | PublishedLanguage
  | SharedKernel
  | UpstreamDownstream
  | Conformist
  | Octopus
  | Partnership
  | CustomerSupplier


type Collaborator
  = BoundedContext BoundedContextId
  | Domain Domain.DomainId

type alias Dependency = (Collaborator, Maybe RelationshipPattern)

type DependencyMap
  = DependencyMap (Dict String (Maybe RelationshipPattern))

emptyDependencies : DependencyMap
emptyDependencies =
  DependencyMap Dict.empty

dependencyCount : DependencyMap -> Int
dependencyCount (DependencyMap dict) =
  Dict.size dict

dependencyList : DependencyMap -> List Dependency
dependencyList (DependencyMap dict) =
  let
    buildCollaborator key =
      case key |> String.split ":" of
        [ "boundedcontext", potentialId ] ->
          potentialId
          |> idFromString
          |> Maybe.map BoundedContext
        [ "domain", potentialId ] ->
          potentialId
          |> Domain.idFromString
          |> Maybe.map Domain
        _ -> Nothing
  in
    dict
    |> Dict.toList
    |> List.filterMap
      ( \(key,r) ->
        key
        |> buildCollaborator
        |> Maybe.map (\s -> (s, r) )
      )

buildKey collaborator =
  case collaborator of
    BoundedContext id ->
      "boundedcontext:" ++ idToString id
    Domain id ->
      "domain:" ++ Domain.idToString id

registerDependency : Dependency -> DependencyMap -> DependencyMap
registerDependency (collaborator, relationship) (DependencyMap dict) =
  DependencyMap (Dict.insert (collaborator |> buildKey) relationship dict)

removeDependency : Dependency -> DependencyMap -> DependencyMap
removeDependency (collaborator, _) (DependencyMap dict) =
  DependencyMap (Dict.remove (collaborator |> buildKey) dict)


relationshipToString: RelationshipPattern -> String
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

relationshipParser: String -> Maybe RelationshipPattern
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


dependencyEncoder : DependencyMap -> Encode.Value
dependencyEncoder (DependencyMap dict) =
  Encode.dict identity (maybeStringEncoder relationshipToString) dict

dependencyDecoder : Decoder DependencyMap
dependencyDecoder =
  Decode.map DependencyMap (Decode.dict (maybeStringDecoder relationshipParser))


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