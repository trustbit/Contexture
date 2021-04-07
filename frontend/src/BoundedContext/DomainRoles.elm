module BoundedContext.DomainRoles exposing (
    DomainRole, DomainRoles, Problem(..),
    getId, getName, getDescription,createDomainRole,
    addDomainRole,deleteDomainRole,getDomainRoles,
    optionalDomainRolesDecoder)

import Json.Encode as Encode
import Json.Decode as Decode
import Json.Decode.Pipeline as JP

import Http
import Url
import Api as Api
import BoundedContext.BoundedContextId exposing (BoundedContextId)


type DomainRole =
    DomainRole DomainRoleInternal

type alias DomainRoles = List DomainRole

type alias DomainRoleInternal =
    { name : String
    , description : Maybe String
    }

type Problem
  = DefinitionEmpty
  | AlreadyExists

getId : DomainRole -> String
getId (DomainRole role) =
    String.toLower role.name

getName : DomainRole -> String
getName (DomainRole role) =
    role.name

getDescription: DomainRole -> Maybe String
getDescription (DomainRole role) =
    role.description

isUnique : String -> DomainRoles -> Bool
isUnique name roles =
  roles
  |> List.map getId
  |> List.member (name |> String.toLower)
  |> not

createDomainRole : DomainRoles -> String -> String -> Result Problem DomainRole
createDomainRole existingRoles name description =
    if String.isEmpty name then
        Err DefinitionEmpty
    else
        if isUnique name existingRoles then
            DomainRoleInternal name (if String.isEmpty description then Nothing else Just description)
            |> DomainRole
            |> Ok
        else Err AlreadyExists

insertDomainRole : DomainRoles -> DomainRole -> Result Problem DomainRoles
insertDomainRole existingRoles role =
    if existingRoles |> isUnique (getId role) then
        List.singleton role
        |> List.append existingRoles
        |> Ok
    else
        Err AlreadyExists

addDomainRole : Api.Configuration -> BoundedContextId -> DomainRoles -> DomainRole -> Result Problem (Api.ApiResult DomainRoles msg)
addDomainRole configuration contextId existingRoles role =
    case insertDomainRole existingRoles role of
        Ok updatedRoles ->
            let
                api = Api.boundedContext contextId
                request toMsg =
                    Http.request
                        { method = "POST"
                        , url = api |> Api.url configuration  |> (\c -> c ++ "/domainRoles")
                        , body = Http.jsonBody <|
                            Encode.object [ domainRolesEncoder updatedRoles ]
                        , expect = Http.expectJson toMsg domainRolesDecoder
                        , timeout = Nothing
                        , tracker = Nothing
                        , headers = []
                        }
            in
                Ok request
        Err problem ->
            problem |> Err



removeDomainRole : DomainRoles -> String -> DomainRoles
removeDomainRole existingRoles id =
    List.filter (\item -> getId item /= id) existingRoles

deleteDomainRole : Api.Configuration -> BoundedContextId -> DomainRoles -> String -> Api.ApiResult DomainRoles msg
deleteDomainRole configuration contextId existingRoles id =
    let
        api = Api.boundedContext contextId
        removedRoles = removeDomainRole existingRoles id
        request toMsg =
            Http.request
                { method = "POST"
                , url = api |> Api.url configuration  |> (\c -> c ++ "/domainRoles")
                , body = Http.jsonBody <|
                    Encode.object [ domainRolesEncoder removedRoles ]
                , expect = Http.expectJson toMsg domainRolesDecoder
                , timeout = Nothing
                , tracker = Nothing
                , headers = []
                }
    in
        request

getDomainRoles : Api.Configuration -> BoundedContextId -> Api.ApiResult DomainRoles msg
getDomainRoles configuration contextId =
    let
        api = Api.boundedContext contextId
        request toMsg =
            Http.get
                { url = api |> Api.url configuration 
                , expect = Http.expectJson toMsg domainRolesDecoder
                }
    in
        request

domainRolesEncoder roles = ("domainRoles", modelsEncoder roles)

domainRolesDecoder : Decode.Decoder DomainRoles
domainRolesDecoder = Decode.at [ "domainRoles"] modelsDecoder

optionalDomainRolesDecoder : Decode.Decoder (DomainRoles -> b) -> Decode.Decoder b
optionalDomainRolesDecoder =
    JP.optional "domainRoles" modelsDecoder []

modelEncoder : DomainRole -> Encode.Value
modelEncoder (DomainRole role) =
    Encode.object
    [
        ("name", Encode.string role.name),
        ("description",
            case role.description of
                Just v -> Encode.string v
                Nothing -> Encode.null
        )
    ]

modelsEncoder : DomainRoles -> Encode.Value
modelsEncoder items =
    Encode.list modelEncoder items

modelDecoder : Decode.Decoder DomainRole
modelDecoder =
    Decode.map DomainRole
        (Decode.map2 DomainRoleInternal
            (Decode.field "name" Decode.string)
            (Decode.maybe (Decode.field "description" Decode.string))
        )

modelsDecoder : Decode.Decoder DomainRoles
modelsDecoder =
    Decode.list modelDecoder