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
          Collaborations: Collaboration list }
        static member Empty =
            { Version = None
              Domains = []
              BoundedContexts = []
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

    type UpdateError<'Error> =
        | EntityNotFound of int
        | ChangeError of 'Error
        
    type Collection<'item when 'item : equality>(items :'item list, getId: 'item -> int) =
        let itemsById =
            lazy
                items
                |> List.map (fun i -> getId i,i)
                |> Map.ofList
                
        let nextId existingIds =
            let highestId =
                match existingIds with
                | [] -> 0
                | items -> items |> List.max

            highestId + 1
            
        let getById idValue =
            itemsById.Value.TryFind idValue 

        let update idValue change =
            match idValue |> getById with
            | Some item ->
                match change item with
                | Ok updatedItem ->
                    let updatedItems =
                        updatedItem
                        :: (items |> List.except ([ item ]))

                    Ok(Collection(updatedItems, getId), updatedItem)
                | Error e ->
                    e |> ChangeError |> Error
            | None ->
                idValue |> EntityNotFound |> Error
            
        let add seed =
            let newId =
                 itemsById.Value
                 |> Map.toList
                 |> List.map fst
                 |> nextId
            let newItem = seed newId
            Ok (Collection(newItem :: items, getId),newItem)
        
        let remove idValue =
            match idValue |> getById with
            | Some item ->
                let updatedItems =
                    items |> List.except ([ item ])
                Ok(Collection(updatedItems,getId), Some item)
            | None ->
                Ok(Collection(items, getId), None)

        member __.ById idValue = getById idValue
        member __.All = items
        member __.Update change idValue = update idValue change
        member __.Add seed = add seed
        member __.Remove idValue = remove idValue
        
    type Document =
        {
            Domains: Collection<Domain>
            BoundedContexts: Collection<BoundedContext>
            Collaborations: Collection<Collaboration>
        }
    
    module Document =
        let subdomainsOf (domains: Collection<Domain>) parentDomainId =
            domains.All |> List.where (fun x -> x.ParentDomain = Some parentDomainId)
        let boundedContextsOf (boundedContexts: Collection<BoundedContext>) domainId =
             boundedContexts.All
            |> List.where (fun x -> x.DomainId = domainId)
        
    type FileBased(fileName: string) =
        let mutable (version,document) =
            Persistence.read fileName
            |> Serialization.deserialize
            |> fun root ->
                let document = {
                  Domains = Collection(root.Domains, fun d -> d.Id)
                  BoundedContexts = Collection (root.BoundedContexts, fun d -> d.Id)
                  Collaborations = Collection (root.Collaborations, fun d -> d.Id)
                }
                root.Version,document
                
        let write change =
            lock fileName (fun () ->
                match change document with
                | Ok (changed: Document, returnValue) ->
                    {
                        Version = version
                        Domains = changed.Domains.All
                        BoundedContexts = changed.BoundedContexts.All
                        Collaborations = changed.Collaborations.All                        
                    }
                    |> Serialization.serialize
                    |> Persistence.save fileName

                    document <- changed
                    Ok returnValue
                | Error e -> Error e)        
            
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
        member __.Read = document
        member __.Change change = write change

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
