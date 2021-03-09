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
        with
            static member Empty =
                { Domains = []
                  BoundedContexts = []
                  BusinessDecisions = []
                  Collaborations = [] }
    
    module Persistence =
        let read path = path |> File.ReadAllText

        let save path data =
            let tempFile = Path.GetTempFileName()
            (tempFile, data) |> File.WriteAllText
            File.Move(tempFile,path, true)

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
    
    type FileBased(fileName: string) =

        let mutable root = Persistence.read fileName |> Serialization.deserialize
        
        let write change =
            lock fileName (
                fun () ->
                    let changed: Root = change root
                    changed |> Serialization.serialize |> Persistence.save fileName
            )
            
        static member EmptyDatabase fileName =
            Root.Empty |> Serialization.serialize |> Persistence.save fileName
            FileBased fileName
        
        static member InitializeDatabase fileName =
            if not (File.Exists fileName) then
                FileBased.EmptyDatabase(fileName)
            else
                FileBased(fileName)
        
        member __.getDomains() =
            root.Domains

        member __.getSubdomains parentDomainId =
            __.getDomains()
            |> List.where (fun x -> x.ParentDomain = Some parentDomainId)
            
        member __.getBoundedContexts domainId =
            root.BoundedContexts
            |> List.where (fun x -> x.DomainId = domainId)
            
        member __.getCollaborations() =
            root.Collaborations
            
        member __.AddDomain domainName =
            write (fun rootDb ->
                let domain : Domain = {
                    Id =  rootDb.Domains.Length
                    Key = None
                    ParentDomain = None
                    Name = domainName
                    Vision = None
                }
                { rootDb with Domains = domain :: rootDb.Domains  }
            )
            

       
              
        
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
