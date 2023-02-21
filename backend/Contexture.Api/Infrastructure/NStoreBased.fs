module Contexture.Api.Infrastructure.Storage.NStoreBased

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open NStore.Core.Logging
open NStore.Persistence.MsSql
open NStore.Core.Streams
open System.Text.Json
open System.Text.Json.Serialization
open NStore.Core.Persistence

open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage

module Position =
    let ofChunk (chunk: IChunk) = Position chunk.Position

    let maxPosition (recorder: Recorder) =
        recorder.Chunks
        |> Seq.tryLast
        |> Option.map ofChunk
        |> Option.defaultValue Position.start

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

type Storage(persistence: IPersistence, logger: INStoreLoggerFactory) =
    let streamCache: ConcurrentDictionary<StreamIdentifier, IStream> =
        ConcurrentDictionary()

    let streamsFactory = StreamsFactory(persistence)

    let fetchAll () =
        task {
            try
                let recorder = Recorder()
                do! persistence.ReadAllAsync(0, recorder)
                let items = EventEnvelope.ofRecorder recorder
                let version = Position.maxPosition recorder
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
                let position = Position.maxPosition recorder
                return Ok(position, items)
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

    let unfilteredSubscription (subscription: SubscriptionHandler) =
        fun chunk ->
            task {
                let envelopes = EventEnvelope.ofChunk chunk
                do! subscription envelopes
                return true
            }

    let filteredSubscription streamKind (subscription: SubscriptionHandler) =
        fun chunk ->
            task {
                let envelopes = EventEnvelope.ofChunk chunk
                let filtered = envelopes |> List.filter (fun e -> e.StreamKind = streamKind)

                if not (List.isEmpty filtered) then
                    do! subscription filtered

                return true
            }

    let captureStatus (processor: ChunkProcessor) =
        let mutable subscriptionStatus = NotRunning

        LambdaSubscription(
            fun chunk ->
                task {
                    let! result = processor.Invoke chunk
                    subscriptionStatus <- Processing(Position.ofChunk chunk)
                    return result
                }
            , OnComplete =
                fun pos ->
                    subscriptionStatus <- CaughtUp(Position.from pos)
                    Task.CompletedTask
            , OnError =
                fun (pos: int64) (ex: Exception) ->
                    subscriptionStatus <- Failed(ex, pos |> Position.from |> Some)
                    Task.CompletedTask
            , OnStart =
                fun pos ->
                    subscriptionStatus <- Processing(Position.from pos)
                    Task.CompletedTask
            , OnStop =
                fun pos ->
                    subscriptionStatus <- Stopped(Position.from pos)
                    Task.CompletedTask
        ),
        fun () -> subscriptionStatus

    let valueFromPosition position =
        match position with
        | Start -> Position.start |> Position.value |> Task.FromResult
        | From position -> position |> Position.value |> Task.FromResult
        | End -> persistence.ReadLastPositionAsync()

    let startAsPolling position (subscription: ChunkProcessor) =
        task {
            let wrappedSubscription, status = captureStatus subscription
            let! positionValue = valueFromPosition position

            let pollingClient =
                PollingClient(persistence, positionValue, wrappedSubscription, logger)

            pollingClient.Start()

            return
                { new Subscription with
                    member _.Status = status ()
                    member _.DisposeAsync() = ValueTask(pollingClient.Stop()) }
        }

    let subscribe definition (subscription: SubscriptionHandler) =
        match definition with
        | FromStream(streamIdentifier, version) ->
            let stream =
                streamIdentifier |> StreamIdentifier.name |> streamsFactory.OpenReadOnly

            let token = new CancellationTokenSource()

            let wrappedSubscription, status =
                captureStatus (unfilteredSubscription subscription)

            // TODO: this is not polling - it just reads data once!
            let readTask =
                stream.ReadAsync(
                    wrappedSubscription,
                    version |> Option.defaultValue Version.start |> Version.value,
                    token.Token
                )

            Task.FromResult
                { new Subscription with
                    member _.Status = status ()

                    member _.DisposeAsync() =
                        ValueTask
                        <| task {
                            if (not token.IsCancellationRequested) then
                                token.Cancel()
                                token.Dispose()

                            while not readTask.IsCompleted do
                                do! Task.Delay(100)

                            readTask.Dispose()
                            return ()
                        } }
        | FromAll position -> startAsPolling position (unfilteredSubscription subscription)
        | FromKind(streamKind, position) -> startAsPolling position (filteredSubscription streamKind subscription)

    interface EventStorage with
        member this.All() : Async<EventResult> = Async.AwaitTask(fetchAll ())
        member this.AllStreamsOf(kind) = Async.AwaitTask(fetchOf kind)

        member this.Append identifier expectedVersion envelopes =
            Async.AwaitTask(append identifier expectedVersion envelopes)

        member this.Stream version identifier =
            Async.AwaitTask(read identifier version)

        member this.Subscribe definition subscription =
            Async.AwaitTask(subscribe definition subscription)
