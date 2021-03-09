namespace Contexture.Api

open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Serialization

open Contexture.Api.Domain

module Database =

    type Root =
        { Domains: Domain list
          BoundedContexts: BoundedContext list
          BusinessDecisions: BusinessDecision list
          Collaborations: Collaboration list }
    
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
    
    let root = Persistence.read "../../example/restaurant-db.json" |> Serialization.deserialize 
    
    let getDomains() =
        root.Domains

    let getSubdomains parentDomainId =
        getDomains()
        |> List.where (fun x -> x.ParentDomain = Some parentDomainId)
        
    let getBoundedContexts domainId =
        root.BoundedContexts
        |> List.where (fun x -> x.DomainId = domainId)
        
    let getCollaborations() =
        root.Collaborations
        
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
