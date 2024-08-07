module Contexture.Api.Infrastructure.Storage.NStoreBased

open System
open System.Collections.Concurrent
open System.Reflection
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Logging
open NStore.Core.Logging
open NStore.Persistence.MsSql
open NStore.Core.Streams
open System.Text.Json
open System.Text.Json.Serialization
open NStore.Core.Persistence

open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Infrastructure.Subscriptions

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

type NStoreEventBatch =
    { Events: NonEmptyList<EventDefinition>
      RecordedAt: DateTimeOffset }

module EventEnvelope =
    let ofChunk (chunk: IChunk) =
        chunk.Payload
        |> tryUnbox<NStoreEventBatch>
        |> Option.map (
            fun batch ->
                batch.Events
                |> NonEmptyList.map (fun e ->
                    {
                        Payload = e.Event
                        StreamKind = e.StreamKind
                        EventType = e.EventType
                        Metadata = {
                            Source = e.Source
                            Position = Position.ofChunk chunk
                            Version = Version.ofChunk chunk
                            RecordedAt = batch.RecordedAt
                        }
                    })
                |> NonEmptyList.asList
        )
        |> Option.defaultValue []

    let ofRecorder (recorder: Recorder) =
        recorder.Chunks |> Seq.collect ofChunk |> Seq.toList

type private SerializableBatch =
    { StreamKind: string
      RecordedAt: System.DateTimeOffset
      Events: SerializableEventEnvelope list }

and private SerializableEventEnvelope =
    { Payload: JsonElement
      EventType: string
      Source: EventSource
    }    

module ReflectionBasedTypeResolver =
    let exportedTypesAssemblyCache =
        lazy
            AppDomain.CurrentDomain.GetAssemblies()
            |> Seq.filter (fun a -> not a.IsDynamic)
            |> Seq.filter (fun a -> not(a.FullName.StartsWith "System" || a.FullName.StartsWith "Microsoft"))
            |> Seq.collect (fun a -> a.ExportedTypes)
            |> Seq.map(fun t -> t.FullName, t)
            |> Map.ofSeq
        
    let findType (eventTypeName:string) =
        let firstAttempt = Type.GetType eventTypeName
        if not(isNull firstAttempt) then
            firstAttempt
        else
            let secondAttempt =
                exportedTypesAssemblyCache
                    .Force()
                    .TryFind eventTypeName
            if secondAttempt |> Option.isNone then
                invalidArg (nameof eventTypeName) $"Cannot resolve a runtime type for {eventTypeName}"
            secondAttempt.Value

type JsonMsSqlSerializer(settings: JsonSerializerOptions, typeResolver: string -> Type) =
    let deserialize streamKind (item: SerializableEventEnvelope) : EventDefinition =
        let eventType = typeResolver item.EventType

        { Event = JsonSerializer.Deserialize(item.Payload, eventType, settings)
          EventType = eventType
          StreamKind = streamKind
          Source = item.Source
        }

    let serialize (envelope: EventDefinition) =
        { Payload = JsonSerializer.SerializeToElement(envelope.Event, settings)
          EventType = envelope.EventType.FullName
          Source = envelope.Source
        }

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
                     ||| JsonUnionEncoding.InternalTag
                     ||| JsonUnionEncoding.UnwrapRecordCases
                     ||| JsonUnionEncoding.UnwrapFieldlessTags)
            )
        )

        JsonMsSqlSerializer(options, ReflectionBasedTypeResolver.findType)

    interface IMsSqlPayloadSerializer with
        member _.Deserialize(serialized, serializerInfo) =
            if serializerInfo = "byte[]" then
                serialized
            else if serializerInfo = nameof SerializableBatch then
                let deserialized =
                    JsonSerializer.Deserialize<SerializableBatch>(serialized, settings)

                let streamKind = deserialized.StreamKind |> StreamKind.ofString
                let events =
                    deserialized.Events
                    |> List.map (deserialize streamKind)
                    |> NonEmptyList.fromList
                match events with
                | Some events ->
                    box {
                        Events = events
                        RecordedAt = deserialized.RecordedAt
                    }
                | None ->
                    failwithf "Received empty batch of events in stream %s at %O" deserialized.StreamKind deserialized.RecordedAt
            else
                let returnType = typeResolver serializerInfo
                JsonSerializer.Deserialize(serialized, returnType, settings)

        member _.Serialize(payload, serializerInfo) =
            match payload |> tryUnbox<byte[]> with
            | Some bytes -> bytes
            | None ->
                match payload |> tryUnbox<NStoreEventBatch> with
                | Some eventBatch ->
                    let batch =
                        { StreamKind =
                            eventBatch.Events
                            |> NonEmptyList.head
                            |> fun h -> h.StreamKind
                            |> StreamKind.toString
                          RecordedAt = eventBatch.RecordedAt
                          Events = eventBatch.Events |> NonEmptyList.map serialize |> NonEmptyList.asList}

                    serializerInfo <- nameof SerializableBatch
                    JsonSerializer.SerializeToUtf8Bytes(batch, settings)
                | None ->
                    serializerInfo <- payload.GetType().FullName
                    JsonSerializer.SerializeToUtf8Bytes(payload, settings)

type Storage(persistence: IPersistence, clock: Clock, logger: INStoreLoggerFactory) =
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

    let append identifier expectedVersion (definitions: NonEmptyList<EventDefinition>) =
        task {
            let envelopes =
                {
                    Events = definitions
                    RecordedAt = clock()
                }
                
            let stream = getOrCreateStream identifier

            let doAppend () =
                stream.AppendAsync(envelopes)
                |> Task.map (fun chunk -> Version.ofChunk chunk, Position.ofChunk chunk)
                |> Task.map Ok

            try
                try
                    let! peek = stream.PeekAsync()

                    match expectedVersion, peek with
                    | Empty, item when isNull item || item.Position = 0 ->
                        (stream |> unbox<OptimisticConcurrencyStream>).MarkAsNew()
                        return! doAppend ()
                    | Empty, item ->
                        return Error(LockingConflict(Version.ofChunk item, exn "Stream is not empty"))
                    | AtVersion expected, item when expected = Version.start && (isNull item || item.Position = 0 ) ->
                        (stream |> unbox<OptimisticConcurrencyStream>).MarkAsNew()
                        return! doAppend ()
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
                    | AtVersion expected, item ->
                        let version =
                            if isNull item then
                                Version.from 0
                            else
                                Version.ofChunk item
                        return
                            Error(LockingConflict(version, exn $"Expected {expected} version but got {version}"))
                    | Unknown, _ -> return! doAppend ()
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
                let position = Position.ofChunk chunk
                do! subscription position envelopes
                return true
            }

    let filteredSubscription streamKind (subscription: SubscriptionHandler) =
        fun chunk ->
            task {
                let envelopes = EventEnvelope.ofChunk chunk
                let position = Position.ofChunk chunk
                let filtered = envelopes |> List.filter (fun e -> e.StreamKind = streamKind)

                if not (List.isEmpty filtered) then
                    do! subscription position filtered
                else
                    do! subscription position List.empty

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
                    let completedStatus = Position.from pos
                    match subscriptionStatus with
                    | Processing status when status >= completedStatus && completedStatus = Position.start ->
                        subscriptionStatus <- CaughtUp status
                    | Processing status
                    | CaughtUp status
                    | Stopped status
                    | Failed(_, Some status) when completedStatus >= status  ->
                        subscriptionStatus <- CaughtUp completedStatus
                    | _ -> ()
                    Task.CompletedTask
            , OnError =
                fun (pos: int64) (ex: Exception) ->
                    subscriptionStatus <- Failed(ex, pos |> Position.from |> Some)
                    Task.CompletedTask
            , OnStart =
                fun pos ->
                    let processingPos = Position.from pos    
                    match subscriptionStatus with
                    | CaughtUp status when processingPos > Position.nextPosition status ->
                        subscriptionStatus <- Processing processingPos
                    | CaughtUp _ ->
                        ()
                    | _ ->
                        subscriptionStatus <- Processing processingPos
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

    let startAsPolling name position (subscription: ChunkProcessor) =
        task {
            let wrappedSubscription, status = captureStatus subscription
            let! positionValue = valueFromPosition position

            let pollingClient =
                PollingClient(persistence, positionValue, wrappedSubscription, logger)
            
            let poll = fun () -> (
                task {
                    while true do
                        do! pollingClient.Poll().ConfigureAwait(false)
                        do! Task.Delay(pollingClient.PollingIntervalMilliseconds).ConfigureAwait(false)
                    }
                :> Task
            )

            Task
                .Run(poll)
                .ContinueWith(fun t ->
                    if t.IsFaulted then
                        let innerException = t.Exception.Flatten().InnerException;
                        let logger = logger.CreateLogger("PollingClient")
                        logger.LogError($"Error during Poll, first exception: {innerException.Message}.\n{innerException}");
                        wrappedSubscription.OnErrorAsync(pollingClient.Position ,t.Exception)
                    else
                        Task.CompletedTask
                )
                .ConfigureAwait(false)
                |> ignore

            return
                { new Subscription with
                    member _.Name = name
                    member _.Status = status ()
                    member _.DisposeAsync() = ValueTask(pollingClient.Stop()) }
        }

    let subscribe name definition (subscription: SubscriptionHandler) =
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
                    member _.Name = name

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
        | FromAll position -> startAsPolling name position (unfilteredSubscription subscription)
        | FromKind(streamKind, position) -> startAsPolling name position (filteredSubscription streamKind subscription)

    interface EventStorage with
        member this.All() : Async<EventResult> = Async.AwaitTask(fetchAll ())
        member this.AllStreamsOf(kind) = Async.AwaitTask(fetchOf kind)

        member this.Append identifier expectedVersion envelopes =
            Async.AwaitTask(append identifier expectedVersion envelopes)

        member this.Stream version identifier =
            Async.AwaitTask(read identifier version)

        member this.Subscribe name definition subscription =
            Async.AwaitTask(subscribe name definition subscription)

type private MicrosoftLoggingLogger(logger:ILogger) =
    interface INStoreLogger with
        member this.BeginScope(state) = logger.BeginScope state
        member this.LogDebug(message, args) = logger.LogDebug(message,args)
        member this.LogError(message, args) = logger.LogError(message,args)
        member this.LogInformation(message, args) = logger.LogInformation(message,args)
        member this.LogWarning(message, args) = logger.LogWarning(message,args)
        member this.IsDebugEnabled = logger.IsEnabled(LogLevel.Debug)
        member this.IsInformationEnabled = logger.IsEnabled(LogLevel.Information)
        member this.IsWarningEnabled = logger.IsEnabled(LogLevel.Warning)
    
type MicrosoftLoggingLoggerFactory(loggerFactory: ILoggerFactory) =
    interface INStoreLoggerFactory with
        member this.CreateLogger(categoryName) = MicrosoftLoggingLogger(loggerFactory.CreateLogger(categoryName))
    