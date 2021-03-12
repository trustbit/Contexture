namespace Contexture.Api

open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Serialization

open Contexture.Api.Domain

module Database =

    type Root =
        { Version: int option
          Domains: Domain list
          BoundedContexts: BoundedContext list
          BusinessDecisions: BusinessDecision list
          Collaborations: Collaboration list }
        static member Empty =
            { Version = None
              Domains = []
              BoundedContexts = []
              BusinessDecisions = []
              Collaborations = [] }

    module Persistence =
        let read path = path |> File.ReadAllText

        let save path data =
            let tempFile = Path.GetTempFileName()
            (tempFile, data) |> File.WriteAllText
            File.Move(tempFile, path, true)

    module Serialization =

        open Newtonsoft.Json.Linq

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

        let migrate (json: string) =
            let root = JObject.Parse json

            let getRelationshipTypeProperty (content: JObject) = JProperty("relationshipType", content)

            let fixCasing (token: JObject) =
                let newValue =
                    match token.["initiatorRole"].Value<string>() with
                    | "upstream" -> "Upstream"
                    | "downstream" -> "Downstream"
                    | "customer" -> "Customer"
                    | "supplier" -> "Supplier"
                    | other -> other
                token.["initiatorRole"] <- JValue(newValue)
                token

            let processRelationship (token: JObject) =
                let parent = token.Parent

                let renamedProperty =
                    match token.Properties() |> List.ofSeq with
                    | firstProperty :: rest when firstProperty.Name = "initiatorRole" ->
                        match rest.Length with
                        | 2 ->
                            JObject(JProperty("upstreamDownstream", fixCasing token))
                            |> getRelationshipTypeProperty
                        | 0 ->
                            let renamedToken = fixCasing token

                            let newProperty =
                                JProperty("role", renamedToken.["initiatorRole"])

                            JObject(JProperty("upstreamDownstream", JObject(newProperty)))
                            |> getRelationshipTypeProperty
                        | _ -> failwith "Unsupported record with initiatorRole"
                    | _ -> getRelationshipTypeProperty token

                parent.Replace(renamedProperty)

            let addTechnicalDescription (token: JToken) =
                let obj = token :?> JObject

                let tools = obj.Property("tools")
                let deployment = obj.Property("deployment")

                let properties =
                    seq {
                        if tools <> null then yield tools
                        if deployment <> null then yield deployment
                    }

                match properties with
                | _ when Seq.isEmpty properties -> ()
                | _ ->
                    properties |> Seq.iter (fun x -> x.Remove())
                    obj.Add(JProperty("technicalDescription", JObject(properties)))

            root.["collaborations"]
            |> Seq.map (fun x -> x.["relationship"])
            |> Seq.where (fun x -> x.HasValues)
            |> Seq.iter (fun x -> x :?> JObject |> processRelationship)

            root.["boundedContexts"]
            |> Seq.iter addTechnicalDescription

            root.Add(JProperty("version", 1))
            root.ToString()

        let rec deserialize (json: string) =
            let root =
                (json, serializerOptions)
                |> JsonSerializer.Deserialize<Root>

            match root.Version with
            | None ->
                let migratedJson = migrate json
                deserialize migratedJson
            | Some _ -> root

    type UpdateError = EntityNotFound of int

    type FileBased(fileName: string) =

        let mutable root =
            Persistence.read fileName
            |> Serialization.deserialize

        let domainById domainId =
            root.Domains
            |> List.tryFind (fun x -> x.Id = domainId)

        let write change =
            lock fileName (fun () ->
                match change root with
                | Ok (changed: Root, returnValue) ->
                    changed
                    |> Serialization.serialize
                    |> Persistence.save fileName

                    root <- changed
                    Ok returnValue
                | Error e -> Error e)

        let nextId existingIds =
            let highestId =
                match existingIds with
                | [] -> 0
                | items -> items |> List.max

            highestId + 1

        static member EmptyDatabase(path: string) =
            match Path.GetDirectoryName path with
            | "" -> ()
            | directoryPath -> Directory.CreateDirectory directoryPath |> ignore

            Root.Empty
            |> Serialization.serialize
            |> Persistence.save path

            FileBased path

        static member InitializeDatabase fileName =
            if not (File.Exists fileName) then FileBased.EmptyDatabase(fileName) else FileBased(fileName)

        member __.getDomains() = root.Domains

        member __.getDomain domainId = domainById domainId

        member __.getSubdomains parentDomainId =
            __.getDomains ()
            |> List.where (fun x -> x.ParentDomain = Some parentDomainId)

        member __.getBoundedContexts domainId =
            root.BoundedContexts
            |> List.where (fun x -> x.DomainId = domainId)

        member __.getCollaborations() = root.Collaborations

        member __.AddDomain domainName =
            write (fun rootDb ->
                let domain: Domain =
                    { Id =
                          rootDb.Domains
                          |> List.map (fun d -> d.Id)
                          |> nextId
                      Key = None
                      ParentDomain = None
                      Name = domainName
                      Vision = None }

                Ok
                    ({ rootDb with
                           Domains = domain :: rootDb.Domains },
                     domain))

        member __.UpdateDomain domainId change =
            write (fun rootDb ->
                match domainId |> domainById with
                | Some domain ->
                    let updatedDomain = change domain

                    let updatedDomains =
                        updatedDomain
                        :: (rootDb.Domains |> List.except ([ domain ]))

                    Ok({ rootDb with Domains = updatedDomains }, updatedDomain)
                | None -> domainId |> EntityNotFound |> Error)

        member __.RemoveDomain domainId =
            write (fun rootDb ->
                match domainId |> domainById with
                | Some domain ->
                    let updatedDomains =
                        rootDb.Domains |> List.except ([ domain ])

                    Ok({ rootDb with Domains = updatedDomains }, Some domain)
                | None -> Ok(rootDb, None))






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
