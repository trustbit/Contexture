﻿namespace Contexture.Api

open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Serialization

open Contexture.Api.Aggregates
open Contexture.Api.Aggregates.NamespaceTemplate
open Contexture.Api.Aggregates.Collaboration
open FSharp.Control.Tasks

module Database =

    open System

    type UpdateError<'Error, 'Id> =
        | EntityNotFoundInCollection of 'Id
        | DuplicateKey of 'Id
        | ChangeError of 'Error

    type CollectionOfInt<'item>(itemsById: Map<int, 'item>) =

        let nextId existingIds =
            let highestId =
                match existingIds with
                | [] -> 0
                | items -> items |> List.max

            highestId + 1

        let getById idValue = itemsById.TryFind idValue

        let update idValue change =
            match idValue |> getById with
            | Some item ->
                match change item with
                | Ok updatedItem ->
                    let itemsUpdated = itemsById |> Map.add idValue updatedItem


                    Ok(CollectionOfInt(itemsUpdated), updatedItem)
                | Error e -> e |> ChangeError |> Error
            | None -> idValue |> EntityNotFoundInCollection |> Error

        let add seed =
            let newId =
                itemsById |> Map.toList |> List.map fst |> nextId

            let newItem = seed newId
            let updatedItems = itemsById |> Map.add newId newItem
            Ok(CollectionOfInt(updatedItems), newItem)

        let remove idValue =
            match idValue |> getById with
            | Some item ->
                let updatedItems = itemsById |> Map.remove idValue
                Ok(CollectionOfInt(updatedItems), Some item)
            | None -> Ok(CollectionOfInt(itemsById), None)

        member __.ById idValue = getById idValue
        member __.All = itemsById |> Map.toList |> List.map snd
        member __.Update change idValue = update idValue change
        member __.Add seed = add seed
        member __.Remove idValue = remove idValue

    type CollectionOfGuid<'item>(itemsById: Map<Guid, 'item>) =

        let getById idValue = itemsById.TryFind idValue

        let update idValue change =
            match idValue |> getById with
            | Some item ->
                match change item with
                | Ok updatedItem ->
                    let itemsUpdated = itemsById |> Map.add idValue updatedItem
                    itemsUpdated |> CollectionOfGuid |> Ok
                | Error e -> e |> ChangeError |> Error
            | None -> idValue |> EntityNotFoundInCollection |> Error

        let add newId item =
            if itemsById.ContainsKey newId then
                newId |> DuplicateKey |> Error
            else
                let updatedItems = itemsById |> Map.add newId item
                updatedItems |> CollectionOfGuid |> Ok

        let remove idValue =
            match idValue |> getById with
            | Some _ ->
                let updatedItems = itemsById |> Map.remove idValue
                updatedItems |> CollectionOfGuid |> Ok
            | None -> itemsById |> CollectionOfGuid |> Ok

        member __.ById idValue = getById idValue
        member __.All = itemsById |> Map.toList |> List.map snd
        member __.Update change idValue = update idValue change
        member __.Add newId item = add newId item
        member __.Remove idValue = remove idValue

    let collectionOfInt (items: _ list) getId =
        let collectionItems =
            if items |> box |> isNull then
                []
            else
                items

        let byId =
            collectionItems
            |> List.map (fun i -> getId i, i)
            |> Map.ofList

        CollectionOfInt(byId)

    let collectionOfGuid (items: _ list) getId =
        let collectionItems =
            if items |> box |> isNull then
                []
            else
                items

        let byId =
            collectionItems
            |> List.map (fun i -> getId i, i)
            |> Map.ofList

        CollectionOfGuid(byId)

    module Persistence =
        let read path = path |> File.ReadAllTextAsync

        let save path data =
            task {
                let tempFile = Path.GetTempFileName()
                do! File.WriteAllTextAsync(tempFile, data)
                File.Move(tempFile, path, true)
            }

    module Serialization =

        open Newtonsoft.Json.Linq
        open ValueObjects
        open Domain.ValueObjects
        open BoundedContext.ValueObjects

        type Domain =
            { Id: DomainId
              ParentDomainId: DomainId option
              Key: string option
              Name: string
              Vision: string option }

        type Collaboration =
            { Id: CollaborationId
              Description: string option
              Initiator: Collaborator
              Recipient: Collaborator
              RelationshipType: RelationshipType option }

        type BoundedContext =
            { Id: BoundedContextId
              DomainId: DomainId
              Key: string option
              Name: string
              Description: string option
              Classification: StrategicClassification
              BusinessDecisions: BusinessDecision list
              UbiquitousLanguage: Map<string, UbiquitousLanguageTerm>
              Messages: Messages
              DomainRoles: DomainRole list
              Namespaces: Namespace.Projections.Namespace list }

        type Root =
            { Version: int option
              Domains: Domain list
              BoundedContexts: BoundedContext list
              Collaborations: Collaboration list
              NamespaceTemplates: Projections.NamespaceTemplate list }
            static member Empty =
                { Version = None
                  Domains = []
                  BoundedContexts = []
                  Collaborations = []
                  NamespaceTemplates = [] }

        let serializerOptions =
            let options =
                JsonSerializerOptions(
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    IgnoreNullValues = true,
                    WriteIndented = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                )

            options.Converters.Add(
                JsonFSharpConverter(
                    unionEncoding =
                        (JsonUnionEncoding.Default
                         ||| JsonUnionEncoding.Untagged
                         ||| JsonUnionEncoding.UnwrapRecordCases
                         ||| JsonUnionEncoding.UnwrapFieldlessTags)
                )
            )

            options

        let serialize data =
            (data, serializerOptions)
            |> JsonSerializer.Serialize

        module Migrations =
            module IdentityHash =
                open System.Text
                open System.Security.Cryptography

                let private swapByteOrderPairs (bytes: byte []) : byte [] =
                    Array.mapi
                        (fun index value ->
                            match index with
                            | 0 -> Array.get bytes 3
                            | 1 -> Array.get bytes 2
                            | 2 -> Array.get bytes 1
                            | 3 -> Array.get bytes 0
                            | 4 -> Array.get bytes 5
                            | 5 -> Array.get bytes 4
                            | 6 -> Array.get bytes 7
                            | 7 -> Array.get bytes 6
                            | _ -> value)
                        bytes

                let buildNamespace (namespaceId: Guid) =
                    swapByteOrderPairs (namespaceId.ToByteArray())

                let generate namespaceBytes (identitierName: string) : Guid =
                    let inputBytes = Encoding.UTF8.GetBytes(identitierName)

                    using
                        (SHA1.Create())
                        (fun algorithm ->
                            algorithm.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0)
                            |> ignore

                            algorithm.TransformFinalBlock(inputBytes, 0, inputBytes.Length)
                            |> ignore

                            let result =
                                Array.truncate 16 algorithm.Hash
                                |> Array.mapi
                                    (fun index (value: byte) ->
                                        match index with
                                        | 6 -> (value &&& 0x0Fuy) ||| (5uy <<< 4)
                                        | 8 -> (value &&& 0x3Fuy) ||| 0x80uy
                                        | _ -> Array.get algorithm.Hash index)
                                |> swapByteOrderPairs

                            Guid(result))

            let private CollaborationNamespaceBytes =
                IdentityHash.buildNamespace (Guid("d24eb67c-1aed-4995-986b-5442c074549a"))

            let private DomainNamespaceBytes =
                IdentityHash.buildNamespace (Guid("04DF3500-497C-4973-902A-AED206345B21"))

            let private BoundedContextNamespaceBytes =
                IdentityHash.buildNamespace (Guid("676FA85B-CB26-469C-B7C7-D1C9E4CCC2A3"))

            let replaceIdProperty propertyName identityNamespace (obj: JObject) =
                let idProperty =
                    obj.Property(propertyName)
                    |> Option.ofObj
                    |> Option.bind
                        (fun p ->
                            p.Value
                            |> Option.ofObj
                            |> Option.bind (tryUnbox<JValue>))

                match idProperty with
                | Some idValue ->
                    let newId =
                        idValue.Value.ToString()
                        |> IdentityHash.generate identityNamespace
                    obj.Property(propertyName).Value <- JValue(newId)
                | None -> ()
            
            let renameProperty oldPropertyName newPropertyName (obj: JObject) =
                let property =
                    obj.Property(oldPropertyName)
                    |> Option.ofObj
                    |> Option.bind
                        (fun p ->
                            p.Value
                            |> Option.ofObj
                            |> Option.bind (tryUnbox<JValue>))

                match property with
                | Some propertyValue ->
                    obj.Remove(oldPropertyName) |> ignore
                    obj.Add(newPropertyName, propertyValue)
                | None -> ()

            let toVersion1 (json: string) =
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

                            if deployment <> null then
                                yield deployment
                        }

                    match properties with
                    | _ when Seq.isEmpty properties -> ()
                    | _ ->
                        properties |> Seq.iter (fun x -> x.Remove())
                        obj.Add(JProperty("technicalDescription", JObject(properties)))

                let processDomain (token: JToken) =
                    let obj = token :?> JObject

                    let domainIdProperty =
                        obj.Property("domainId")
                        |> Option.ofObj
                        |> Option.bind (fun p -> p.Value |> Option.ofObj)

                    match domainIdProperty with
                    | Some parentIdValue ->
                        obj.Remove("domainId") |> ignore
                        obj.Add("parentDomainId", parentIdValue)
                    | None -> ()

                root.["collaborations"]
                |> Seq.map (fun x -> x.["relationship"])
                |> Seq.where (fun x -> not (isNull x) && x.HasValues)
                |> Seq.iter (fun x -> x :?> JObject |> processRelationship)

                root.["boundedContexts"]
                |> Seq.iter addTechnicalDescription

                root.["domains"] |> Seq.iter processDomain

                root.Add(JProperty("version", 1))
                root.ToString()

            let toVersion2 (json: string) =
                let root = JObject.Parse json

                let addEmptyNamespaces (token: JToken) =
                    let obj = token :?> JObject

                    let namespaces = obj.Property("namespaces")

                    if isNull namespaces then
                        obj.Add("namespaces", JArray())


                let processCollaborations (token: JToken) =
                    let obj = token :?> JObject

                    obj
                    |> replaceIdProperty "id" CollaborationNamespaceBytes

                    let replaceReferences propertyName =
                        obj.Property(propertyName)
                        |> Option.ofObj
                        |> Option.iter
                            (fun p ->
                                let propertyObject = p.Value :?> JObject

                                propertyObject
                                |> replaceIdProperty "domain" DomainNamespaceBytes

                                propertyObject
                                |> replaceIdProperty "boundedContext" BoundedContextNamespaceBytes)

                    replaceReferences "initiator"
                    replaceReferences "recipient"

                let processDomains (token: JToken) =
                    let obj = token :?> JObject
                    obj |> replaceIdProperty "id" DomainNamespaceBytes

                    obj
                    |> replaceIdProperty "parentDomainId" DomainNamespaceBytes

                let processBoundedContexts (token: JToken) =
                    let obj = token :?> JObject
                    token |> addEmptyNamespaces

                    obj
                    |> replaceIdProperty "domainId" DomainNamespaceBytes

                    obj
                    |> replaceIdProperty "id" BoundedContextNamespaceBytes


                root.["collaborations"]
                |> Seq.iter processCollaborations

                root.["domains"] |> Seq.iter processDomains

                root.["boundedContexts"]
                |> Seq.iter processBoundedContexts

                if root.Property("namespaceTemplates") |> isNull
                   || not
                      <| root.Property("namespaceTemplates").HasValues then
                    root.Add(JProperty("namespaceTemplates", JArray()))
                root.Property("version").Value <- JValue(2)
                root.ToString()
                
            let toVersion3 (json: string) =
                let root = JObject.Parse json

                let processDomains (token: JToken) =
                    let obj = token :?> JObject
                    obj |> renameProperty "key" "shortName"

                let processBoundedContexts (token: JToken) =
                    let obj = token :?> JObject
                    obj |> renameProperty "key" "shortName"

                root.["domains"] |> Seq.iter processDomains

                root.["boundedContexts"] |> Seq.iter processBoundedContexts

                root.Property("version").Value <- JValue(3)
                root.ToString()

        type HasVersion = { Version: int option }

        let applyMigrations version json =
            let versions =
                [ 0, Migrations.toVersion1
                  1, Migrations.toVersion2
                  2, Migrations.toVersion3 ]

            versions
            |> List.skipWhile (fun (v, _) -> version > v)
            |> List.map snd
            |> List.fold (fun j migration -> migration j) json

        let private deserializeWithOptions<'T> (json: string) =
            JsonSerializer.Deserialize<'T>(json, serializerOptions)

        let deserialize (json: string) =
            let root =
                json |> deserializeWithOptions<HasVersion>

            let currentVersion = root.Version |> Option.defaultValue 0

            json
            |> applyMigrations currentVersion
            |> deserializeWithOptions<Root>

    type Document =
        { Domains: CollectionOfGuid<Serialization.Domain>
          BoundedContexts: CollectionOfGuid<Serialization.BoundedContext>
          Collaborations: CollectionOfGuid<Serialization.Collaboration>
          NamespaceTemplates: CollectionOfGuid<Projections.NamespaceTemplate> }

    type SingleFileBasedDatastore =
        abstract member Read : unit -> Async<Document>
        abstract member Change : (Document -> Result<Document, string>) -> Async<Result<unit, string>>

    module AgentBased =
        type Agent<'T> = MailboxProcessor<'T>

        type private Msg =
            | Read of AsyncReplyChannel<Document>
            | Write of (Document -> Result<Document, string>) * AsyncReplyChannel<Result<unit, string>>

        let initialize (fileName: string, initial: Serialization.Root) : SingleFileBasedDatastore =
            let version, initialDocument =
                (initial.Version,
                 { Domains = collectionOfGuid initial.Domains (fun d -> d.Id)
                   BoundedContexts = collectionOfGuid initial.BoundedContexts (fun d -> d.Id)
                   Collaborations = collectionOfGuid initial.Collaborations (fun d -> d.Id)
                   NamespaceTemplates = collectionOfGuid initial.NamespaceTemplates (fun d -> d.Id) })

            let filePersistence (inbox: Agent<Msg>) =
                let rec loop document =
                    async {
                        let! msg = inbox.Receive()

                        match msg with
                        | Read reply ->
                            reply.Reply document
                            return! loop document
                        | Write (change, reply) ->
                            match change document with
                            | Ok (changed: Document) ->
                                do!
                                    { Serialization.Version = version
                                      Serialization.Domains = changed.Domains.All
                                      Serialization.BoundedContexts = changed.BoundedContexts.All
                                      Serialization.Collaborations = changed.Collaborations.All
                                      Serialization.NamespaceTemplates = changed.NamespaceTemplates.All }
                                    |> Serialization.serialize
                                    |> Persistence.save fileName
                                    |> Async.AwaitTask

                                reply.Reply(Ok())
                                return! loop changed
                            | Error e ->
                                reply.Reply(Error e)
                                return! loop document
                    }

                loop initialDocument

            let agent = Agent.Start(filePersistence)

            { new SingleFileBasedDatastore with
                member _.Read() =
                    agent.PostAndAsyncReply(fun reply -> Read reply)

                member _.Change change =
                    agent.PostAndAsyncReply(fun reply -> Write(change, reply)) }

        let load (path: string) =
            task {
                let! content = Persistence.read path
                let root = content |> Serialization.deserialize

                return initialize (path, root)
            }

        let emptyDatabase (path: string) =
            task {
                match Path.GetDirectoryName path with
                | "" -> ()
                | directoryPath -> Directory.CreateDirectory directoryPath |> ignore

                let root = Serialization.Root.Empty

                do!
                    root
                    |> Serialization.serialize
                    |> Persistence.save path

                return initialize (path, root)
            }

        let initializeDatabase fileName =
            if not (File.Exists fileName) then
                emptyDatabase (fileName)
            else
                load (fileName)

    module Mutable =
        type FileBased(fileName: string, initial: Serialization.Root) =

            let mutable (version, document) =
                (initial.Version,
                 { Domains = collectionOfGuid initial.Domains (fun d -> d.Id)
                   BoundedContexts = collectionOfGuid initial.BoundedContexts (fun d -> d.Id)
                   Collaborations = collectionOfGuid initial.Collaborations (fun d -> d.Id)
                   NamespaceTemplates = collectionOfGuid initial.NamespaceTemplates (fun d -> d.Id) })

            let write change =
                lock
                    fileName
                    (fun () ->
                        async {
                            match change document with
                            | Ok (changed: Document) ->
                                do!
                                    { Serialization.Version = version
                                      Serialization.Domains = changed.Domains.All
                                      Serialization.BoundedContexts = changed.BoundedContexts.All
                                      Serialization.Collaborations = changed.Collaborations.All
                                      Serialization.NamespaceTemplates = changed.NamespaceTemplates.All }
                                    |> Serialization.serialize
                                    |> Persistence.save fileName
                                    |> Async.AwaitTask

                                document <- changed
                                return Ok()
                            | Error (e: string) -> return Error e
                        })

            static member Load(path: string) =
                task {
                    let! content = Persistence.read path
                    let root = content |> Serialization.deserialize

                    return FileBased(path, root) :> SingleFileBasedDatastore
                }

            static member EmptyDatabase(path: string) =
                task {
                    match Path.GetDirectoryName path with
                    | "" -> ()
                    | directoryPath -> Directory.CreateDirectory directoryPath |> ignore

                    let root = Serialization.Root.Empty

                    do!
                        root
                        |> Serialization.serialize
                        |> Persistence.save path

                    return FileBased(path, root) :> SingleFileBasedDatastore
                }

            static member InitializeDatabase fileName =
                if not (File.Exists fileName) then
                    FileBased.EmptyDatabase(fileName)
                else
                    FileBased.Load(fileName)

            interface SingleFileBasedDatastore with
                member _.Read() = async { return document }
                member _.Change change = async { return! write change }
