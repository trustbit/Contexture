module BoundedContext.BusinessDecision exposing (..)

import Json.Encode as Encode
import Json.Decode as Decode


type BusinessDecision = 
    BusinessDecision BusinessDecisionInternal
    
type alias BusinessDecisionInternal = 
    { name : String
    , description : Maybe String
    }

type alias BusinessDecisions = List BusinessDecision

type Problem
  = DefinitionEmpty
  | AlreadyExists

getId : BusinessDecision -> String
getId (BusinessDecision decision) = 
    String.toLower decision.name

getName : BusinessDecision -> String 
getName (BusinessDecision decision) =
    decision.name

getDescription: BusinessDecision -> Maybe String
getDescription (BusinessDecision decision) =
    decision.description

isDecisionUnique : String -> BusinessDecisions -> Bool
isDecisionUnique name decisions =
  decisions
  |> List.map getId
  |> List.member (name |> String.toLower)
  |> not

defineBusinessDecision : BusinessDecisions -> String -> String -> Result Problem BusinessDecision
defineBusinessDecision decisions name description =
    if String.isEmpty name then
        Err DefinitionEmpty
    else
        if isDecisionUnique name decisions then
            BusinessDecisionInternal name (if String.isEmpty description then Nothing else Just description)
            |> BusinessDecision
            |> Ok
        else Err AlreadyExists

addBusinessDecision : BusinessDecisions -> BusinessDecision -> Result Problem BusinessDecisions
addBusinessDecision decisions decision =
    if decisions |> isDecisionUnique (getId decision) then
        List.singleton decision
        |> List.append decisions
        |> Ok
    else 
        Err AlreadyExists

deleteBusinessDecision : BusinessDecisions -> String -> BusinessDecisions
deleteBusinessDecision desicions id =
    List.filter (\item -> getId item /= id) desicions

modelEncoder : BusinessDecision -> Encode.Value
modelEncoder (BusinessDecision decision) = 
    Encode.object
    [
        ("name", Encode.string decision.name),
        ("description", 
            case decision.description of
                Just v -> Encode.string v
                Nothing -> Encode.null
        )
    ]

modelsEncoder : BusinessDecisions -> Encode.Value
modelsEncoder items = 
    Encode.list modelEncoder items

modelDecoder : Decode.Decoder BusinessDecision
modelDecoder = 
    Decode.map BusinessDecision
        (Decode.map2 BusinessDecisionInternal
            (Decode.field "name" Decode.string)
            (Decode.maybe (Decode.field "description" Decode.string))
        )

modelsDecoder : Decode.Decoder BusinessDecisions
modelsDecoder =
    Decode.list modelDecoder