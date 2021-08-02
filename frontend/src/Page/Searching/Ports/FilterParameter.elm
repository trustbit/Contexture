port module Page.Searching.Ports.FilterParameter exposing (FilterParameter, changeFilter, filterParametersChanged)

import Json.Decode as Decode exposing (Error)
import Json.Encode as Encode


type alias FilterParameter =
    { name : String
    , value : String
    }


filterParametersChanged : (Result Error (List FilterParameter) -> msg) -> Sub msg
filterParametersChanged toMsg =
    onQueryStringChanged (Decode.decodeString (Decode.list filterParameterDecoder) >> toMsg)


changeFilter : List FilterParameter -> Cmd msg
changeFilter parameters =
    changeQueryString (parameters |> Encode.list filterParameterEncoder |> Encode.encode 4)


port changeQueryString : String -> Cmd msg


port onQueryStringChanged : (String -> msg) -> Sub msg


filterParameterDecoder =
    Decode.map2 FilterParameter
        (Decode.field "name" Decode.string)
        (Decode.field "value" Decode.string)


filterParameterEncoder filter =
    Encode.object
        [ ( "name", Encode.string filter.name )
        , ( "value", Encode.string filter.value )
        ]
