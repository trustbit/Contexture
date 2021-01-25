module ContextMapping.RelationshipType exposing (
    RelationshipType(..), SymmetricRelationship(..), UpstreamRelationship(..), DownstreamRelationship(..),UpstreamDownstreamRelationship(..),
    UpstreamCollaborator, DownstreamCollaborator,
    encoder, decoder)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)

import ContextMapping.Collaborator as Collaborator exposing (Collaborator)

type SymmetricRelationship
  = SharedKernel
  | Partnership
  | SeparateWays
  | BigBallOfMud

type UpstreamRelationship 
  = Upstream
  | PublishedLanguage
  | OpenHost

type DownstreamRelationship 
  = Downstream
  | AntiCorruptionLayer
  | Conformist

type UpstreamDownstreamRelationship
  = CustomerSupplierRole CustomerSupplierInformation
  | UpstreamDownstreamRole UpstreamCollaborator DownstreamCollaborator

type alias CustomerSupplierInformation = { customer: Collaborator, supplier: Collaborator }

type alias UpstreamCollaborator =  (Collaborator, UpstreamRelationship)
type alias DownstreamCollaborator =  (Collaborator, DownstreamRelationship)

type RelationshipType
  = Symmetric SymmetricRelationship Collaborator Collaborator
  | UpstreamDownstream UpstreamDownstreamRelationship
  | Octopus UpstreamCollaborator (List DownstreamCollaborator)
  | Unknown Collaborator Collaborator


upstreamCollaboratorEncoder : UpstreamCollaborator -> Encode.Value
upstreamCollaboratorEncoder (collaborator, upstreamType) =
  Encode.object
    [ ( "collaborator", Collaborator.encoder collaborator)
    , ( "type"
      , Encode.string <|
          case upstreamType of
            Upstream -> "Upstream"
            PublishedLanguage -> "PublishedLanguage"
            OpenHost -> "OpenHost"       
      )
    ]

upstreamCollaboratorDecoder : Decoder UpstreamCollaborator
upstreamCollaboratorDecoder =
  Decode.map2 Tuple.pair
    ( Decode.field "collaborator" Collaborator.decoder)
    ( Decode.field "type" Decode.string |> Decode.andThen (\upstreamType ->
        case upstreamType of
          "Upstream" -> Decode.succeed Upstream
          "PublishedLanguage" -> Decode.succeed PublishedLanguage
          "OpenHost" -> Decode.succeed OpenHost
          x  -> Decode.fail <| "could not decode pattern: " ++ x
      )
    )

downstreamCollaboratorEncoder : DownstreamCollaborator -> Encode.Value
downstreamCollaboratorEncoder (collaborator, downstreamType) =
  Encode.object
    [ ( "collaborator", Collaborator.encoder collaborator )
    , ( "type", Encode.string <| downstreamRelationshipToString downstreamType )
    ]

downstreamRelationshipToString : DownstreamRelationship -> String
downstreamRelationshipToString downstreamType =
  case downstreamType of
    Downstream -> "Downstream"
    AntiCorruptionLayer -> "AntiCorruptionLayer"
    Conformist -> "Conformist"

downstreamRelationshipFromString : String -> Result String DownstreamRelationship
downstreamRelationshipFromString downstreamType =
  case downstreamType of
    "Downstream" -> Ok Downstream
    "AntiCorruptionLayer" -> Ok AntiCorruptionLayer
    "Conformist" -> Ok Conformist
    x  -> Err x

downstreamCollaboratorDecoder : Decoder DownstreamCollaborator
downstreamCollaboratorDecoder =
  Decode.map2 Tuple.pair
    ( Decode.field "collaborator" Collaborator.decoder )
    ( Decode.field "type" Decode.string |> Decode.andThen ( resultToDecoder downstreamRelationshipFromString ) )

upstreamDownstreamEncoder : UpstreamDownstreamRelationship -> Encode.Value
upstreamDownstreamEncoder upstreamDownstreamRealtionship =
  case upstreamDownstreamRealtionship of
    CustomerSupplierRole { customer, supplier} ->
      Encode.object
        [ ( "customer",  Collaborator.encoder customer )
        , ( "supplier", Collaborator.encoder supplier )
        ]
    UpstreamDownstreamRole upstream downstream ->
      Encode.object
        [ ( "upstream", upstreamCollaboratorEncoder upstream )
        , ( "downstream", downstreamCollaboratorEncoder downstream )
        ] 

encoder : RelationshipType -> Encode.Value
encoder relationshipType =
  case relationshipType of
    Symmetric symmetricType participant1 participant2 ->
      Encode.object
        [ ( "symmetric", Encode.string <| symmetricRelationshipToString symmetricType )
        , ( "participant1", Collaborator.encoder participant1 )
        , ( "participant2", Collaborator.encoder participant2 )
        ]
    UpstreamDownstream upstreamDownstreamType ->
      upstreamDownstreamEncoder upstreamDownstreamType
    Octopus upstream downstreams ->
      Encode.object
        [ ( "upstream", upstreamCollaboratorEncoder upstream )
        , ( "downstreams", downstreams |> Encode.list downstreamCollaboratorEncoder )
        ]
    Unknown participant1 participant2 ->
      Encode.object
        [ ( "participant1", Collaborator.encoder participant1 )
        , ( "participant2", Collaborator.encoder participant2 )
        ]

symmetricRelationshipFromString : String -> Result String SymmetricRelationship
symmetricRelationshipFromString symmetricType =
  case symmetricType of
    "SharedKernel" -> Ok SharedKernel
    "SeparateWays" -> Ok SeparateWays
    "BigBallOfMud" -> Ok BigBallOfMud
    "Partnership" -> Ok Partnership
    x  -> Err x

symmetricRelationshipToString : SymmetricRelationship -> String
symmetricRelationshipToString symmetricType =
 case symmetricType of
    SharedKernel -> "SharedKernel"
    SeparateWays -> "SeparateWays"
    BigBallOfMud -> "BigBallOfMud"
    Partnership -> "Partnership"



upstreamDownstreamDecoder : Decoder UpstreamDownstreamRelationship
upstreamDownstreamDecoder =
  let
    customerSupplierDecode =
      Decode.map2 CustomerSupplierInformation
        ( collaboratorFieldDecoder "customer" )
        ( collaboratorFieldDecoder "supplier" )
  in
    Decode.oneOf
      [ Decode.map CustomerSupplierRole
          customerSupplierDecode
      , Decode.map2 UpstreamDownstreamRole
        ( Decode.field "upstream" upstreamCollaboratorDecoder )
        ( Decode.field "downstream" downstreamCollaboratorDecoder )
      ]


decoder : Decoder RelationshipType
decoder =
  Decode.oneOf 
    [ Decode.map3 Symmetric
      ( Decode.field 
          "symmetric" 
          Decode.string 
          |> Decode.andThen (resultToDecoder symmetricRelationshipFromString)
      )      
      ( collaboratorFieldDecoder "participant1" )
      ( collaboratorFieldDecoder "participant2" )
    , Decode.map UpstreamDownstream
        upstreamDownstreamDecoder
    , Decode.map2 Octopus
      ( Decode.field "upstream" upstreamCollaboratorDecoder )
      ( Decode.field "downstreams" ( Decode.list downstreamCollaboratorDecoder ) )
    , Decode.map2 Unknown
      ( collaboratorFieldDecoder "participant1" )
      ( collaboratorFieldDecoder "participant2" )
    ]


collaboratorFieldDecoder fieldName =
   Decode.field fieldName Collaborator.decoder 


resultToDecoder : (a -> Result String b) -> (a -> Decoder b)
resultToDecoder convert =
  \value ->
    case convert value of
      Ok v -> Decode.succeed v
      Err e -> Decode.fail <| "Could not decode pattern:" ++ e
   