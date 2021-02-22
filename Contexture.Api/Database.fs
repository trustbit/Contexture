namespace Contexture.Api

open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Serialization

open Contexture.Api.Domain

module Database =

    module Persistence =
        let read path = path |> File.ReadAllText

        let save path data = (path, data) |> File.WriteAllText

    module Serialization =

        let serializerOptions =
            let options =
                JsonSerializerOptions
                    (Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                     PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                     IgnoreNullValues = true,
                     WriteIndented = true,
                     NumberHandling = JsonNumberHandling.AllowReadingFromString)
                    
            options.Converters.Add
                (JsonFSharpConverter
                    (unionEncoding =
                        (JsonUnionEncoding.Default
                         ||| JsonUnionEncoding.Untagged
                         ||| JsonUnionEncoding.UnwrapRecordCases
                         ||| JsonUnionEncoding.UnwrapFieldlessTags)))

            options

        let serialize data =
            (data, serializerOptions)
            |> JsonSerializer.Serialize

        let deserialize (json: string) =
            (json, serializerOptions)
            |> JsonSerializer.Deserialize<Root>

    let getDomains = 0

//    let getSubdomains parentDomainId =
//        getDomains
//        |> List.filter (fun x -> x.ParentDomain = Some parentDomainId)

//    let mapInitiator (initiator: Context.Initiator) =
//        { BoundedContext = initiator.BoundedContext
//          Domain = initiator.Domain }
//
//    let getCollaborations =
//        root.Collaborations
//        |> Array.map (fun x -> {
//            Description = x.Description
//            Initiator = x.Initiator |> mapInitiator
//            Recipient = x.Recipient |> mapCollaborator
//            Relationship = x.Relationship
//        })
//        |> Array.toList
