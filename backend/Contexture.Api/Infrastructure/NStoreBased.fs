module Contexture.Api.Infrastructure.Storage.NStoreBased

open System
open System.Collections.Concurrent
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open NStore.Persistence.MsSql
open NStore.Core.Streams
open System.Text.Json
open System.Text.Json.Serialization
open NStore.Core.Persistence

open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage

module Version =
    let ofChunk (chunk: IChunk) = Version chunk.Position

    let maxVersion (recorder: Recorder) =
        recorder.Chunks
        |> Seq.tryLast
        |> Option.map ofChunk
        |> Option.defaultValue Version.start

module EventEnvelope =
    let ofChunk (chunk: IChunk) =
        chunk.Payload |> tryUnbox<EventEnvelope list> |> Option.defaultValue []

    let ofRecorder (recorder: Recorder) =
        recorder.ToArray<EventEnvelope list>() |> Array.toList |> List.collect id

type SerializableBatch =
    { StreamKind: string
      Events: SerializableEventEnvelope list }

and SerializableEventEnvelope =
    { Metadata: EventMetadata
      Payload: JsonElement
      EventType: string }

type JsonMsSqlSerializer(settings: JsonSerializerOptions) =
    let deserialize streamKind (item: SerializableEventEnvelope) =
        let eventType = item.EventType |> Type.GetType

        { EventEnvelope.Payload = JsonSerializer.Deserialize(item.Payload, eventType, settings)
          EventType = eventType
          StreamKind = streamKind
          Metadata = item.Metadata }

    let serialize (envelope: EventEnvelope) =
        { Payload = JsonSerializer.SerializeToElement(envelope.Payload, settings)
          EventType = envelope.EventType.AssemblyQualifiedName
          Metadata = envelope.Metadata }

    static member Default: JsonMsSqlSerializer =
        let options =
            JsonSerializerOptions(
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

        JsonMsSqlSerializer options

    interface IMsSqlPayloadSerializer with
        member _.Deserialize(serialized, serializerInfo) =
            if serializerInfo = "byte[]" then
                serialized
            else if serializerInfo = nameof (SerializableBatch) then
                let deserialized =
                    JsonSerializer.Deserialize<SerializableBatch>(serialized, settings)

                let streamKind = deserialized.StreamKind |> StreamKind.ofString
                deserialized.Events |> List.map (deserialize streamKind) |> box
            else
                let returnType = Type.GetType serializerInfo
                JsonSerializer.Deserialize(serialized, returnType, settings)

        member _.Serialize(payload, serializerInfo) =
            match payload |> tryUnbox<byte[]> with
            | Some bytes -> bytes
            | None ->
                match payload |> tryUnbox<EventEnvelope list> with
                | Some envelopes ->
                    let batch =
                        { StreamKind = StreamKind.toString envelopes.Head.StreamKind
                          Events = envelopes |> List.map serialize }

                    serializerInfo <- nameof (SerializableBatch)
                    JsonSerializer.SerializeToUtf8Bytes(batch, settings)
                | None ->
                    serializerInfo <- payload.GetType().FullName
                    JsonSerializer.SerializeToUtf8Bytes(payload, settings)

type Storage(persistence: IPersistence) =
    let streamCache: ConcurrentDictionary<StreamIdentifier, IStream> =
        ConcurrentDictionary()

    let streamsFactory = StreamsFactory(persistence)

    let fetchAll () =
        task {
            try
                let recorder = Recorder()
                do! persistence.ReadAllAsync(0, recorder)
                let items = EventEnvelope.ofRecorder recorder
                let version = Version.maxVersion recorder
                return Ok(version, items)
            with e ->
                return Error(e.ToString())
        }

    let fetchOf (kind: StreamKind) =
        task {
            try
                let doesStreamKindMatch (chunk: IChunk) =
                    chunk
                    |> EventEnvelope.ofChunk
                    |> List.exists (fun envelope -> envelope.StreamKind = kind)

                let recorder = Recorder()
                let filtered = SubscriptionWrapper(recorder, ChunkFilter = doesStreamKindMatch)
                do! persistence.ReadAllAsync(0, filtered)
                let items = EventEnvelope.ofRecorder recorder
                let version = Version.maxVersion recorder
                return Ok(version, items)
            with e ->
                return Error(e.ToString())
        }

    let getOrCreateStream identifier =
        streamCache.GetOrAdd(
            identifier,
            fun identifier -> identifier |> StreamIdentifier.name |> streamsFactory.OpenOptimisticConcurrency
        )

    let read identifier (Version version) =
        task {
            let stream = getOrCreateStream identifier
            let recorder = Recorder()
            do! stream.ReadAsync(recorder, version)

            let items = recorder |> EventEnvelope.ofRecorder

            return Ok(Version.maxVersion recorder, items)
        }

    let append identifier expectedVersion (envelopes: EventEnvelope list) =
        task {
            let stream = getOrCreateStream identifier

            let doAppend () =
                stream.AppendAsync(envelopes) |> Task.map Version.ofChunk |> Task.map Ok

            try
                try
                    let! peek = stream.PeekAsync()

                    match expectedVersion, peek with
                    | Empty, item when isNull item || item.Position = 0 ->
                        (stream |> unbox<OptimisticConcurrencyStream>).MarkAsNew()
                        return! doAppend ()
                    | Empty, _ -> return Error(LockingConflict((Version.from 0), exn "not empty"))
                    | AtVersion expected, item when not (isNull item) ->
                        let currentVersion = Version.ofChunk item

                        if currentVersion <> expected then
                            return
                                Error(
                                    LockingConflict(
                                        currentVersion,
                                        exn $"Expected {expected} version but got {currentVersion}"
                                    )
                                )
                        else
                            return! doAppend ()
                    | AtVersion expected, _ ->
                        return
                            Error(LockingConflict(Version.from 0, exn $"Expected {expected} version but got unknown"))
                    | (Unknown, _) -> return! doAppend ()
                with :? ConcurrencyException as e ->
                    let! currentVersion = stream.PeekAsync()
                    return Error(LockingConflict(Version.ofChunk currentVersion, e))
            with e ->
                return Error(UnknownError e)
        }


    let stream (identifier: StreamIdentifier) =
        task {
            let stream = identifier |> StreamIdentifier.name |> streamsFactory.OpenReadOnly

            try
                let recorder = Recorder()
                do! stream.ReadAsync(recorder)
                let items = EventEnvelope.ofRecorder recorder
                let version = Version.maxVersion recorder
                return Ok(version, items)
            with e ->
                return Error(e.ToString())
        }

    let newStream (identifier: StreamIdentifier) =
        let stream =
            identifier |> StreamIdentifier.name |> streamsFactory.OpenOptimisticConcurrency

        stream

    let subscribe definition (subscription: Subscription) =
        let unfilteredSubscription =
            { new ISubscription with
                member _.OnStartAsync(indexOrPosition) = Task.CompletedTask

                member _.OnNextAsync(chunk: IChunk) =
                    task {
                        let envelopes = EventEnvelope.ofChunk chunk
                        do! subscription envelopes
                        return true
                    }

                member _.CompletedAsync(indexOrPosition) = Task.CompletedTask

                member _.StoppedAsync(indexOrPosition) = Task.CompletedTask

                member _.OnErrorAsync(indexOrPosition, ex) = Task.CompletedTask }

        match definition with
        | FromStream(streamIdentifier, version) ->
            let stream =
                streamIdentifier |> StreamIdentifier.name |> streamsFactory.OpenReadOnly

            stream.ReadAsync(unfilteredSubscription, version |> Option.defaultValue Version.start |> Version.value)
        | FromAll position -> persistence.ReadAllAsync(Position.value position, unfilteredSubscription)
        | FromKind(streamKind, position) ->
            let filteredSubscription =
                { new ISubscription with
                    member _.OnStartAsync(indexOrPosition) = Task.CompletedTask

                    member _.OnNextAsync(chunk: IChunk) =
                        task {
                            let envelopes = EventEnvelope.ofChunk chunk
                            let filtered = envelopes |> List.filter (fun e -> e.StreamKind = streamKind)

                            if not (List.isEmpty filtered) then
                                do! subscription filtered

                            return true
                        }

                    member _.CompletedAsync(indexOrPosition) = Task.CompletedTask

                    member _.StoppedAsync(indexOrPosition) = Task.CompletedTask

                    member _.OnErrorAsync(indexOrPosition, ex) = Task.CompletedTask }

            persistence.ReadAllAsync(Position.value position, filteredSubscription)


    member this.NewStream identifier = newStream identifier

    interface EventStorage with
        member this.All() : Async<EventResult> = Async.AwaitTask(fetchAll ())
        member this.AllStreamsOf(kind) = Async.AwaitTask(fetchOf kind)

        member this.Append identifier expectedVersion envelopes =
            Async.AwaitTask(append identifier expectedVersion envelopes)

        member this.Stream version identifier =
            Async.AwaitTask(read identifier version)

        member this.Subscribe definition subscription =
            Async.AwaitTask(subscribe definition subscription)
