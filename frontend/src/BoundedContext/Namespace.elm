module BoundedContext.Namespace exposing (..)

import Json.Decode as Decode exposing (Decoder)
import Json.Encode as Encode


type alias Uuid =
    String


type alias NamespaceId =
    Uuid


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

type alias NamespaceTemplateId =
    Uuid

type alias TemplateLabelId =
    Uuid


type alias LabelTemplate =
    { id : TemplateLabelId
    , name : String
    , description : Maybe String
    , placeholder : Maybe String
    }

type alias NamespaceTemplate =
    { id : NamespaceTemplateId
    , name : String
    , description : Maybe String
    , template : List LabelTemplate
    }

uuidDecoder : Decoder Uuid
uuidDecoder = Decode.string

labelDecoder : Decoder Label
labelDecoder =
    Decode.map3 Label
        (Decode.field "id" uuidDecoder)
        (Decode.field "name" Decode.string)
        (Decode.field "value" Decode.string)

namespaceDecoder : Decoder Namespace
namespaceDecoder =
    Decode.map4 Namespace
        (Decode.field "id" uuidDecoder)
        (Decode.maybe (Decode.field "template" uuidDecoder))
        (Decode.field "name" Decode.string)
        (Decode.field "labels" (Decode.list labelDecoder))


templateLabelDecoder : Decoder LabelTemplate
templateLabelDecoder =
    Decode.map4 LabelTemplate
        (Decode.field "id" uuidDecoder)
        (Decode.field "name" Decode.string)
        (Decode.maybe (Decode.field "description" Decode.string))
        (Decode.maybe (Decode.field "placeholder" Decode.string))

namespaceTemplateDecoder : Decoder NamespaceTemplate
namespaceTemplateDecoder =
    Decode.map4 NamespaceTemplate
        (Decode.field "id" uuidDecoder)
        (Decode.field "name" Decode.string)
        (Decode.maybe (Decode.field "description" Decode.string))
        (Decode.field "template" (Decode.list templateLabelDecoder))

