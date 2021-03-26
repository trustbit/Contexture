module BoundedContext.Namespace exposing (..)

import Json.Decode as Decode exposing (Decoder)
import Json.Encode as Encode


type alias Uuid =
    String


type alias NamespaceId =
    Uuid


type alias NamespaceTemplateId =
    Int


type alias LabelId =
    Uuid


type alias Label =
    { id : LabelId
    , name : String
    , value : String
    }


type alias Namespace =
    { id : NamespaceId
    , template : Maybe NamespaceTemplateId
    , name : String
    , labels : List Label
    }


labelDecoder : Decoder Label
labelDecoder =
    Decode.map3 Label
        (Decode.field "id" Decode.string)
        (Decode.field "name" Decode.string)
        (Decode.field "value" Decode.string)

namespaceDecoder : Decoder Namespace
namespaceDecoder =
    Decode.map4 Namespace
        (Decode.field "id" Decode.string)
        (Decode.maybe (Decode.field "template" Decode.int))
        (Decode.field "name" Decode.string)
        (Decode.field "labels" (Decode.list labelDecoder))


