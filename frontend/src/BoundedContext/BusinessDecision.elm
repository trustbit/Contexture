module BoundedContext.BusinessDecision exposing (
    BusinessDecision, BusinessDecisions, Problem(..),
    getId,getName,getDescription,defineBusinessDecision,
    addBusinessDecision,removeBusinessDecision,getBusinessDecisions,
    optionalBusinessDecisionsDecoder)

import Json.Encode as Encode
import Json.Decode as Decode
import Json.Decode.Pipeline as JP

import Http
import Url
import Api as Api
import BoundedContext.BoundedContextId exposing (BoundedContextId)


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

insertBusinessDecision : BusinessDecisions -> BusinessDecision -> Result Problem BusinessDecisions
insertBusinessDecision decisions decision =
    if decisions |> isDecisionUnique (getId decision) then
        List.singleton decision
        |> List.append decisions
        |> Ok
    else 
        Err AlreadyExists


addBusinessDecision : Api.Configuration -> BoundedContextId -> BusinessDecisions -> BusinessDecision -> Result Problem (Api.ApiResult BusinessDecisions msg)
addBusinessDecision configuration contextId decisions decision =
  case insertBusinessDecision decisions decision of
    Ok updatedDecisions ->
      let
        api = Api.boundedContext contextId
        request toMsg =
          Http.request
            { method = "PATCH"
            , url = api |> Api.url configuration |> Url.toString
            , body = Http.jsonBody <|
                Encode.object [ businessDecisionsEncoder updatedDecisions ]
            , expect = Http.expectJson toMsg businessDecisionsDecoder
            , timeout = Nothing
            , tracker = Nothing
            , headers = []
            }
      in
        Ok request
    Err problem ->
      problem |> Err



getBusinessDecisions : Api.Configuration -> BoundedContextId -> Api.ApiResult BusinessDecisions msg
getBusinessDecisions configuration contextId =
  let
    api = Api.boundedContext contextId
    request toMsg =
      Http.get
        { url = api |> Api.url configuration |> Url.toString
        , expect = Http.expectJson toMsg businessDecisionsDecoder
        }
  in
    request

deleteBusinessDecision : BusinessDecisions -> String -> BusinessDecisions
deleteBusinessDecision desicions id =
    List.filter (\item -> getId item /= id) desicions



removeBusinessDecision : Api.Configuration -> BoundedContextId -> BusinessDecisions -> String -> Api.ApiResult BusinessDecisions msg
removeBusinessDecision configuration contextId decisions decision =
  let
    api = Api.boundedContext contextId
    removedRoles = deleteBusinessDecision decisions decision
    request toMsg =
      Http.request
        { method = "PATCH"
        , url = api |> Api.url configuration |> Url.toString
        , body = Http.jsonBody <|
            Encode.object [ businessDecisionsEncoder removedRoles ]
        , expect = Http.expectJson toMsg businessDecisionsDecoder
        , timeout = Nothing
        , tracker = Nothing
        , headers = []
        }
  in
    request


businessDecisionsEncoder language = ("businessDecisions", modelsEncoder language)

businessDecisionsDecoder : Decode.Decoder BusinessDecisions
businessDecisionsDecoder = Decode.at [ "businessDecisions"] modelsDecoder

optionalBusinessDecisionsDecoder : Decode.Decoder (BusinessDecisions -> b) -> Decode.Decoder b
optionalBusinessDecisionsDecoder =
    JP.optional "businessDecisions" modelsDecoder []

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