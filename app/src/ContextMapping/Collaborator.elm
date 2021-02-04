module ContextMapping.Collaborator exposing (
    Collaborator(..),
    encoder,decoder
    )

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)

import Domain
import Domain.DomainId as Domain
import BoundedContext.BoundedContextId exposing (BoundedContextId)

type Collaborator
  = BoundedContext BoundedContextId
  | Domain Domain.DomainId
  | ExternalSystem String
  | Frontend String
  | UserInteraction String

encoder : Collaborator -> Encode.Value
encoder collaborator =
    Encode.object 
    [ case collaborator of
        BoundedContext bcId -> 
            ( "boundedContext", BoundedContext.BoundedContextId.idEncoder bcId)
        Domain domainId ->
            ( "domain", Domain.idEncoder domainId)
        ExternalSystem external ->
            ( "externalSystem", Encode.string external)
        Frontend frontend ->
            ( "frontend", Encode.string frontend)
        UserInteraction user ->
            ( "userInteraction", Encode.string user) 
    ]

decoder : Decoder Collaborator
decoder =
    Decode.oneOf
        [ Decode.map BoundedContext <| Decode.field "boundedContext" BoundedContext.BoundedContextId.idDecoder
        , Decode.map Domain <| Decode.field "domain" Domain.idDecoder
        , Decode.map ExternalSystem <| Decode.field "externalSystem" Decode.string
        , Decode.map Frontend <| Decode.field "frontend" Decode.string
        , Decode.map UserInteraction <| Decode.field "userInteraction" Decode.string
        ]