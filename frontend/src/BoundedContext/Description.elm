module BoundedContext.Description exposing (
    Description,
    noDescription,
    update,
    optionalDescriptionDecoder)

import Json.Encode as Encode
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline as JP

import Api
import Http
import Url
import BoundedContext.BoundedContextId exposing (BoundedContextId)

type alias Description = String

noDescription : Description
noDescription = ""


update : Api.Configuration -> BoundedContextId -> Description -> Api.ApiResult Description msg
update configuration contextId description =
    let
        api = Api.boundedContext contextId
        request toMsg =
            Http.request
                { method = "PATCH"
                , url = api |> Api.url configuration |> Url.toString
                , body = Http.jsonBody <|
                    Encode.object [ descriptionEncoder description] 
                , expect = Http.expectJson toMsg descriptionDecoder
                , timeout = Nothing
                , tracker = Nothing
                , headers = []
                }
    in
        request


-- encoder

descriptionEncoder description = ("description", encoder description)

descriptionDecoder : Decode.Decoder Description
descriptionDecoder = Decode.at [ "description"] decoder

optionalDescriptionDecoder : Decode.Decoder (Description -> b) -> Decode.Decoder b
optionalDescriptionDecoder =
    JP.optional "description" decoder noDescription

encoder : Description -> Encode.Value
encoder description =
  Encode.string description


decoder : Decoder Description
decoder =
  Decode.string