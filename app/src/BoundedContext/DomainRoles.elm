module BoundedContext.DomainRoles exposing (..)

import Json.Encode as Encode
import Json.Decode as Decode

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

removeDomainRole : DomainRoles -> String -> DomainRoles
removeDomainRole existingRoles id =
    List.filter (\item -> getId item /= id) existingRoles

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