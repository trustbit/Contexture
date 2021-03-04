module ContextMapping.RelationshipType exposing (
    RelationshipType(..), SymmetricRelationship(..), UpstreamRelationship(..), DownstreamRelationship(..),UpstreamDownstreamRelationship(..),
    InitiatorUpstreamDownstreamRole(..), InitiatorCustomerSupplierRole(..),
    encoder, decoder)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)

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
  = CustomerSupplierRelationship InitiatorCustomerSupplierRole
  | UpstreamDownstreamRelationship InitiatorUpstreamDownstreamRole UpstreamRelationship DownstreamRelationship

type InitiatorUpstreamDownstreamRole
  = UpstreamRole
  | DownstreamRole

type InitiatorCustomerSupplierRole
  = SupplierRole
  | CustomerRole


type RelationshipType
  = Symmetric SymmetricRelationship
  | UpstreamDownstream UpstreamDownstreamRelationship
  | Unknown 


upstreamCollaboratorEncoder : UpstreamRelationship -> Encode.Value
upstreamCollaboratorEncoder upstreamType =
  Encode.string <|
    case upstreamType of
      Upstream -> "Upstream"
      PublishedLanguage -> "PublishedLanguage"
      OpenHost -> "OpenHost"       

upstreamCollaboratorDecoder : Decoder UpstreamRelationship
upstreamCollaboratorDecoder =
  Decode.string 
  |> Decode.andThen (\upstreamType ->
    case upstreamType of
      "Upstream" -> Decode.succeed Upstream
      "PublishedLanguage" -> Decode.succeed PublishedLanguage
      "OpenHost" -> Decode.succeed OpenHost
      x  -> Decode.fail <| "could not decode pattern: " ++ x
  )
  
downstreamCollaboratorEncoder : DownstreamRelationship -> Encode.Value
downstreamCollaboratorEncoder downstreamType =
  Encode.string <| downstreamRelationshipToString downstreamType  

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

downstreamCollaboratorDecoder : Decoder DownstreamRelationship
downstreamCollaboratorDecoder =
  Decode.string |> Decode.andThen ( resultToDecoder downstreamRelationshipFromString ) 

upstreamDownstreamEncoder : UpstreamDownstreamRelationship -> Encode.Value
upstreamDownstreamEncoder upstreamDownstreamRealtionship =
  case upstreamDownstreamRealtionship of
    CustomerSupplierRelationship CustomerRole ->
      Encode.object
        [ ( "initiatorRole",  Encode.string "customer" )
        ]
    CustomerSupplierRelationship SupplierRole ->
      Encode.object
        [ ( "initiatorRole",  Encode.string "supplier" )
        ]
    UpstreamDownstreamRelationship initiatorRole upstreamType downstreamType ->
      Encode.object
        [ ( "initiatorRole",  Encode.string <|
            case initiatorRole of
              UpstreamRole -> 
                "upstream"
              DownstreamRole ->
                "downstream"
          )
        , ( "upstreamType", upstreamCollaboratorEncoder upstreamType )
        , ( "downstreamType", downstreamCollaboratorEncoder downstreamType )
        ] 


upstreamDownstreamDecoder : Decoder UpstreamDownstreamRelationship
upstreamDownstreamDecoder =
  Decode.oneOf
    [ Decode.map CustomerSupplierRelationship 
      ( Decode.field "initiatorRole" Decode.string |> Decode.andThen (\upstreamType ->
          case upstreamType of
            "customer" -> Decode.succeed CustomerRole
            "supplier" -> Decode.succeed SupplierRole
            x  -> Decode.fail <| "could not decode pattern: " ++ x
        )
      )
    , Decode.map3 UpstreamDownstreamRelationship
      ( Decode.field "initiatorRole" Decode.string |> Decode.andThen (\upstreamType ->
          case upstreamType of
            "upstream" -> Decode.succeed UpstreamRole
            "downstream" -> Decode.succeed DownstreamRole
            x  -> Decode.fail <| "could not decode pattern: " ++ x
        )
      )
      ( Decode.field "upstreamType" upstreamCollaboratorDecoder )
      ( Decode.field "downstreamType" downstreamCollaboratorDecoder )
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




encoder : RelationshipType -> Encode.Value
encoder relationshipType =
  case relationshipType of
    Symmetric symmetricType ->
      Encode.object
        [ ( "symmetric", Encode.string <| symmetricRelationshipToString symmetricType )
        ]
    UpstreamDownstream upstreamDownstreamType ->
      upstreamDownstreamEncoder upstreamDownstreamType
    Unknown ->
      Encode.object
        [ ]


decoder : Decoder RelationshipType
decoder =
  Decode.oneOf 
    [ Decode.map Symmetric
      ( Decode.field 
          "symmetric" 
          Decode.string 
          |> Decode.andThen (resultToDecoder symmetricRelationshipFromString)
      )      
    , Decode.map UpstreamDownstream
        upstreamDownstreamDecoder
    , Decode.succeed Unknown
    ]

resultToDecoder : (a -> Result String b) -> (a -> Decoder b)
resultToDecoder convert =
  \value ->
    case convert value of
      Ok v -> Decode.succeed v
      Err e -> Decode.fail <| "Could not decode pattern:" ++ e
   