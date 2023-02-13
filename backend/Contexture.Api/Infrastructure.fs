namespace Contexture.Api.Infrastructure

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Runtime.CompilerServices
open System.Text.Json
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Internal
open NStore.Core.Persistence

module Tuple =
    let mapFsg map (first, second) =
        (map first, second)
    let mapSnd map (first, second) =
        (first, map second)
module Async =

    let map mapper o =
        async {
            let! result = o
            return mapper result
        }

    let bindOption o =
        async {
            match o with
            | Some value ->
                let! v = value
                return Some v
            | None -> return None
        }

    let optionMap mapper o =
        async {
            let! bound = bindOption o
            return Option.map mapper bound
        }

type Agent<'T> = MailboxProcessor<'T>

type Clock = unit -> System.DateTime

type EventSource = System.Guid
type StreamKind =
    private
    | SystemType of System.Type
    static member Of<'E>() = SystemType typeof<'E>
    static member Of(_: 'E) = SystemType typeof<'E>
    static member Of(systemType: Type) =
        if isNull systemType then
            nullArg <| nameof systemType
        SystemType systemType
module StreamKind =
    let toString(SystemType systemType) =
        systemType.AssemblyQualifiedName
    let ofString(value:string) =
        value |> Type.GetType |> StreamKind.Of


type Version = private Version of int64
module Version =
    let start = Version 0
    let from value =
        if value < 0 then
           invalidArg $"Value must not be smaller 0 but is {value}" (nameof value)
        Version value

type EventMetadata =
    { Source: EventSource
      RecordedAt: System.DateTime }

type EventEnvelope<'Event> =
    { Metadata: EventMetadata
      Event: 'Event }

type EventEnvelope =
    { Metadata: EventMetadata
      Payload: obj
      EventType: System.Type
      StreamKind: StreamKind }

type Subscription<'E> = EventEnvelope<'E> list -> Async<unit>


type private InternalSubscriptionWrapper = EventEnvelope list -> Async<unit>

module EventEnvelope =
    let box (envelope: EventEnvelope<'E>) =
        { Metadata = envelope.Metadata
          Payload = box envelope.Event
          EventType = typeof<'E>
          StreamKind = StreamKind.Of<'E>() }

    let unbox (envelope: EventEnvelope) : EventEnvelope<'E> =
        { Metadata = envelope.Metadata
          Event = unbox<'E> envelope.Payload }
    
type EventResult = Result<Version * EventEnvelope list, string>
type EventResult<'e> = Result<Version * EventEnvelope<'e> list, string>

type EventDefinition<'Event> = 'Event
type AppendError =
    | LockingConflict of currentVersion: Version * exn
    | UnknownError of exn
type ExpectedVersion =
    | Empty
    | AtVersion of Version
    | Unknown
type EventStream<'Event> =
    abstract Read: Version -> Async<EventResult<'Event>>
    abstract Append : ExpectedVersion -> EventDefinition<'Event> list -> Async<Result<Version,AppendError>>

module Storage =
    type StreamIdentifier =
        private
        | StreamIdentifier of StreamKind * EventSource
    module StreamIdentifier =
        let name (StreamIdentifier (kind, source)) =
            $"{StreamKind.toString kind}/{source.ToString()}"
        let from(eventSource:EventSource) (kind: StreamKind) =
            StreamIdentifier (kind,eventSource)
        let source (StreamIdentifier(_,source)) =source
        let kind (StreamIdentifier(kind,_)) =kind

    type EventStorage =
        abstract member Stream : Version -> StreamIdentifier -> Async<EventResult>
        abstract member AllStreamsOf : StreamKind -> Async<EventResult>
        abstract member Append : StreamIdentifier -> ExpectedVersion ->  EventEnvelope list -> Async<Result<Version,AppendError>>
        abstract member All : unit -> Async<EventResult>

    module InMemoryStorage =

        type Msg =
            private
            | Get of StreamKind * AsyncReplyChannel<EventResult>
            | GetStream of StreamIdentifier * AsyncReplyChannel<EventResult>
            | GetAll of AsyncReplyChannel<EventResult>
            | Append of EventEnvelope list * AsyncReplyChannel<Version>

        type private History =
            { items: (Version * EventEnvelope) list
              byIdentifier: Dictionary<StreamIdentifier, (Version * EventEnvelope) list>
              byEventType: Dictionary<StreamKind, (Version * EventEnvelope) list> }
            static member Empty =
                { items = []
                  byIdentifier = Dictionary()
                  byEventType = Dictionary() }

        let private stream history source =
            let (success, events) = history.byIdentifier.TryGetValue source
            if success then events else []

        let private getAllStreamsOf history key =
            let (success, items) = history.byEventType.TryGetValue key
            if success then items else []
            
        let private withMaxVersion (items: (Version * EventEnvelope) list) =
            if List.isEmpty items then
                Version.start,[]
            else
                items |> List.maxBy (fun (Version version,_) -> version) |> fst,
                items |> List.map snd

        let private appendToHistory (history: History) (envelope: EventEnvelope) =
            let source = envelope.Metadata.Source
            let streamKind = envelope.StreamKind
            let key = StreamIdentifier.from source streamKind
            let version = Version.from (history.items.Length + 1)
            let fullStream =
                key |> stream history |> fun s -> s @ [ version,envelope ]

            history.byIdentifier.[key] <- fullStream
            let allEvents = getAllStreamsOf history streamKind
            history.byEventType.[streamKind] <- allEvents @ [ version, envelope ]
            
            { history with items = (version, envelope) :: history.items }

        let initialize (initialEvents: EventEnvelope list) =
            let proc (inbox: Agent<Msg>) =
                let rec loop history =
                    async {
                        let! msg = inbox.Receive()

                        match msg with
                        | Get (kind, reply) ->
                            kind
                            |> getAllStreamsOf history
                            |> withMaxVersion
                            |> Ok
                            |> reply.Reply

                            return! loop history

                        | GetStream (identifier, reply) ->
                            identifier
                            |> stream history
                            |> withMaxVersion
                            |> Ok |>
                            reply.Reply

                            return! loop history
                            
                        | GetAll reply ->
                            history.items
                            |> withMaxVersion
                            |> Ok
                            |> reply.Reply
                            
                            return! loop history

                        | Append (events, reply) ->
                            let extendedHistory =
                                events |> List.fold appendToHistory history
                                
                            let version = extendedHistory.items |> List.last |> fst
                            reply.Reply(version)

                            return! loop extendedHistory
                    }

                let initialHistory =
                    initialEvents
                    |> List.fold appendToHistory History.Empty

                loop initialHistory

            let agent = Agent<Msg>.Start (proc)

            { new EventStorage with
                member _.Stream version identifier =
                    agent.PostAndAsyncReply(fun reply -> GetStream(identifier, reply))

                member _.AllStreamsOf streamType =
                    agent.PostAndAsyncReply(fun reply -> Get(streamType, reply))

                member _.Append identifier expectedVersion events = async {
                    let! result = agent.PostAndAsyncReply(fun reply -> Append(events, reply))
                    return Ok result
                    }
            
                member _.All () =
                    agent.PostAndAsyncReply(fun reply -> GetAll(reply)) } 
    
    module NStoreBased =
        open NStore.Core.Persistence
        open NStore.Persistence.MsSql
        open NStore.Core.Streams
        open System.Text.Json
        open System.Text.Json.Serialization
        
        module Version =
            let ofChunk (chunk: IChunk) =
                Version chunk.Position
            let maxVersion (recorder: Recorder) =
                recorder.Chunks
                |> Seq.tryLast
                |> Option.map ofChunk
                |> Option.defaultValue Version.start
                
        module EventEnvelope =
            let ofChunk (chunk: IChunk) =
                 chunk.Payload
                |> tryUnbox<EventEnvelope list>
                |> Option.defaultValue []
            let ofRecorder (recorder:Recorder) =
                recorder.ToArray<EventEnvelope list>()
                |> Array.toList
                |> List.collect id
                
        type SerializableBatch =
            { StreamKind: string
              Events : SerializableEventEnvelope list
            }
        and SerializableEventEnvelope =
            { Metadata: EventMetadata
              Payload: JsonElement
              EventType: string
            }
        type JsonMsSqlSerializer(settings: JsonSerializerOptions) =
            let deserialize streamKind (item:SerializableEventEnvelope) =
                let eventType = item.EventType |> Type.GetType
                {
                    EventEnvelope.Payload = JsonSerializer.Deserialize(item.Payload,eventType,settings)
                    EventType = eventType
                    StreamKind = streamKind
                    Metadata = item.Metadata
                }
            let serialize (envelope:EventEnvelope) =
                {
                    Payload = JsonSerializer.SerializeToElement(envelope.Payload, settings)
                    EventType = envelope.EventType.AssemblyQualifiedName 
                    Metadata = envelope.Metadata
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
                    else if serializerInfo = nameof(SerializableBatch) then
                        let deserialized = JsonSerializer.Deserialize<SerializableBatch>(serialized, settings)
                        let streamKind = deserialized.StreamKind |> StreamKind.ofString
                        deserialized.Events
                        |> List.map (deserialize streamKind)
                        |> box
                    else
                        let returnType = Type.GetType serializerInfo
                        JsonSerializer.Deserialize(serialized, returnType, settings)

                member _.Serialize(payload, serializerInfo) =
                    match payload |> tryUnbox<byte[]> with
                    | Some bytes ->
                        bytes
                    | None ->
                        match payload |> tryUnbox<EventEnvelope list> with
                        | Some envelopes ->
                            let batch =
                                { StreamKind = StreamKind.toString envelopes.Head.StreamKind
                                  Events = envelopes |> List.map serialize
                                }
                            serializerInfo <- nameof(SerializableBatch)
                            JsonSerializer.SerializeToUtf8Bytes(batch, settings)
                        | None ->
                            serializerInfo <- payload.GetType().FullName
                            JsonSerializer.SerializeToUtf8Bytes(payload,settings)
                        
        type Storage(persistence: IPersistence) =
            let streamCache: ConcurrentDictionary<StreamIdentifier,IStream> = ConcurrentDictionary()
            let streamsFactory = StreamsFactory(persistence)
         
            let fetchAll ()= 
                task {
                    try
                        let recorder = Recorder()
                        do! persistence.ReadAllAsync(0,recorder)
                        let items = EventEnvelope.ofRecorder recorder
                        let version = Version.maxVersion recorder
                        return Ok (version,items)                            
                    with e ->
                        return Error (e.ToString())
                    }
                
            let fetchOf (kind: StreamKind)= 
                task {
                   try
                        let doesStreamKindMatch (chunk:IChunk) =
                            chunk
                            |> EventEnvelope.ofChunk
                            |> List.exists (fun envelope -> envelope.StreamKind = kind)
                            
                        let recorder = Recorder()
                        let filtered = SubscriptionWrapper(recorder, ChunkFilter = doesStreamKindMatch)
                        do! persistence.ReadAllAsync(0,filtered)
                        let items = EventEnvelope.ofRecorder recorder
                        let version = Version.maxVersion recorder
                        return Ok (version,items)
                    with e ->
                        return Error (e.ToString())
                    }
                
            let getOrCreateStream identifier =
                streamCache.GetOrAdd(
                    identifier,
                    fun identifier -> identifier |> StreamIdentifier.name |> streamsFactory.OpenOptimisticConcurrency
                    )
                
            let read identifier (Version version)  =
                task {
                    let stream = getOrCreateStream identifier
                    let recorder = Recorder()
                    do! stream.ReadAsync(recorder, version)
                    
                    let items =
                        recorder
                        |> EventEnvelope.ofRecorder
                    
                    return Ok(Version.maxVersion recorder, items)
               }
            let append identifier expectedVersion (envelopes: EventEnvelope list) = task {
                let stream = getOrCreateStream identifier
                let doAppend () =
                    stream.AppendAsync(envelopes)
                    |> Task.map Version.ofChunk
                    |> Task.map Ok
                try
                    try
                        let! peek = stream.PeekAsync()
                        match expectedVersion,peek  with
                        | Empty, item when isNull item || item.Position = 0 ->
                            (stream |> unbox<OptimisticConcurrencyStream>).MarkAsNew()
                            return! doAppend()
                        | Empty, _ ->
                            return Error (LockingConflict ((Version.from 0), exn "not empty"))
                        | AtVersion expected, item when not (isNull item) ->
                            let currentVersion = Version.ofChunk item
                            if currentVersion <> expected then
                                 return Error (LockingConflict (currentVersion, exn $"Expected {expected} version but got {currentVersion}"))
                            else
                                return! doAppend()
                        | AtVersion expected, _ ->
                            return Error (LockingConflict (Version.from 0, exn $"Expected {expected} version but got unknown"))
                        | (Unknown, _) ->
                            return! doAppend()
                    with
                        | :? ConcurrencyException as e ->
                            let! currentVersion = stream.PeekAsync()
                            return Error (LockingConflict (Version.ofChunk currentVersion, e) )
                with
                    | e ->
                        return Error (UnknownError e)
            }
            
                        
            let stream (identifier: StreamIdentifier) = task {
                let stream = identifier |> StreamIdentifier.name |> streamsFactory.OpenReadOnly
                try
                    let recorder = Recorder()
                    do! stream.ReadAsync(recorder)
                    let items = EventEnvelope.ofRecorder recorder
                    let version = Version.maxVersion recorder
                    return Ok (version,items)      
                with e ->
                    return Error (e.ToString())
            }
            
            let newStream (identifier: StreamIdentifier) = 
                let stream = identifier |> StreamIdentifier.name |> streamsFactory.OpenOptimisticConcurrency
                stream
            
            member this.NewStream identifier = newStream identifier
            
            interface EventStorage with
                member this.All() : Async<EventResult>=  Async.AwaitTask (fetchAll()) 
                member this.AllStreamsOf(kind) = Async.AwaitTask (fetchOf kind)
                member this.Append identifier expectedVersion envelopes = Async.AwaitTask (append identifier expectedVersion envelopes)
                member this.Stream version identifier = Async.AwaitTask (read identifier version)
                
                
            
open Storage
open Storage.NStoreBased
open NStore.Core.Persistence
open NStore.Core.Streams
type EventStore
    // private // TODO private?
    (
        storage: Storage.EventStorage,
        clock: ISystemClock,
        // TODO: use an agent for subscriptions?!
        subscriptions: ConcurrentDictionary<System.Type, InternalSubscriptionWrapper list>
    ) =

    let subscriptionsOf key =
        let (success, items) = subscriptions.TryGetValue key
        if success then items else []

    let asTyped items : EventEnvelope<'E> list =
        items |> List.map EventEnvelope.unbox
        
    let asUntyped items = items |> List.map EventEnvelope.box

    let notifySubscriptions (newItems: EventEnvelope<'E> list) =
        subscriptionsOf typedefof<'E>
        |> List.map
            (fun subscription ->
                let upcastSubscription events = events |> asUntyped |> subscription

                upcastSubscription newItems)
        |> Async.Sequential
        |> Async.Ignore

    // let appendAndNotify source (newItems: EventEnvelope<'E> list) =
    //     async {
    //         do! storage.Append (StreamIdentifier.from source (StreamKind.Of<'E>())) (newItems |> asUntyped)
    //         do! notifySubscriptions newItems
    //     }

    let subscribe (subscription: Subscription<'E>) =
        let key = typedefof<'E>

        let upcastSubscription events =
            events
            |> asTyped
            |> subscription

        subscriptions.AddOrUpdate(
            key,
            (fun _ -> [ upcastSubscription ]),
            (fun _ subscriptions -> subscriptions @ [ upcastSubscription ])
        )
        |> ignore

    let allStreams () : Async<Version * EventEnvelope<'E> list> =
        async {
            match! StreamKind.Of<'E>() |> storage.AllStreamsOf with
            | Ok allStreams -> return allStreams |> Tuple.mapSnd asTyped
            | Error e ->
                failwithf "Could not get all streams: %s" e
                return Version.start,List.empty
        }

    let stream version name : Async<Version * EventEnvelope<'E> list> =
        async {
            match! storage.Stream version (StreamIdentifier.from name (StreamKind.Of<'E>())) with
            | Ok events -> return events |> Tuple.mapSnd asTyped
            | Error e ->
                failwithf "Could not get stream %s" e
                return Version.start,List.empty
        }
        
    let newStream2 name : EventStream<'E> =
        let identifier = StreamIdentifier.from name (StreamKind.Of<'E>()) 
        { new EventStream<'E> with
               member _.Read version =
                   storage.Stream version identifier
                   |> AsyncResult.map (Tuple.mapSnd asTyped)
               member _.Append version definitions =
                    let envelopes =
                        definitions
                        |> List.map (fun payload ->
                            {
                                Metadata = {
                                    Source = identifier |> StreamIdentifier.source
                                    RecordedAt = clock.UtcNow.DateTime 
                                }
                                Event = payload
                            }
                        )
                        |> List.map EventEnvelope.box
                    storage.Append identifier version envelopes 
        }
        
        
    let all toAllStream : Async<Version * EventEnvelope<'E> list> =
        async {
            match! storage.All() with
            | Ok allStreams ->
                return allStreams |> Tuple.mapSnd (toAllStream)
            | Error e ->
                failwithf "Could not get all streams: %s" e
                return Version.start,List.empty
        }

    static member Empty =
        EventStore(Storage.InMemoryStorage.initialize List.empty,SystemClock(), ConcurrentDictionary())

    static member With(history: EventEnvelope list) =
        EventStore(Storage.InMemoryStorage.initialize history,SystemClock(), ConcurrentDictionary())

    member _.Stream name version =
        let stream = newStream2 name
        stream.Read version
    member _.Append identifier version items =
        let stream = newStream2 identifier
        stream.Append version items
    member _.AllStreams() = allStreams ()
    member _.Subscribe(subscription: Subscription<'E>) = subscribe subscription
    member _.All toAllStream = all toAllStream

module Projections =
    type Projection<'State, 'Event> =
        { Init: 'State
          Update: 'State -> 'Event -> 'State }

    let projectIntoMap selectId projection =
        fun state (eventEnvelope: EventEnvelope<_>) ->
            let selectedId = selectId eventEnvelope

            state
            |> Map.tryFind selectedId
            |> Option.defaultValue projection.Init
            |> fun projectionState ->
                eventEnvelope.Event
                |> projection.Update projectionState
            |> fun newState -> state |> Map.add selectedId newState

    let projectIntoMapBySourceId projection =
        projectIntoMap (fun eventEnvelope -> eventEnvelope.Metadata.Source) projection

    let project projection (events: EventEnvelope<_> list) =
        events
        |> List.map (fun e -> e.Event)
        |> List.fold projection.Update projection.Init


module ReadModels =
    type EventHandler<'Event> = EventEnvelope<'Event> list -> Async<unit>

    type ReadModelInitialization =
        abstract member ReplayAndConnect : unit -> Async<unit>

    module ReadModelInitialization =
        type private RMI<'Event>(eventStore: EventStore, handler: EventHandler<'Event>) =
            interface ReadModelInitialization with
                member __.ReplayAndConnect() =
                    async {
                        let! (_,allStreams) = eventStore.AllStreams<'Event>()
                        do! handler allStreams
                        eventStore.Subscribe handler
                    }

        let initializeWith (eventStore: EventStore) (handler: EventHandler<'Event>) : ReadModelInitialization =
            RMI(eventStore, handler) :> ReadModelInitialization

    type ReadModel<'Event, 'State> =
        abstract member EventHandler : EventEnvelope<'Event> list -> Async<unit>
        abstract member State : unit -> Task<'State>

    type Msg<'Event, 'Result> =
        | Notify of EventEnvelope<'Event> list * AsyncReplyChannel<unit>
        | State of AsyncReplyChannel<'Result>

    let readModel
        (updateState: 'State -> EventEnvelope<'Event> list -> 'State)
        (initState: 'State)
        : ReadModel<'Event, 'State> =
        let agent =
            let eventSubscriber (inbox: Agent<Msg<_, _>>) =
                let rec loop state =
                    async {
                        let! msg = inbox.Receive()

                        match msg with
                        | Notify (eventEnvelopes, reply) ->
                            reply.Reply()
                            return! loop (eventEnvelopes |> updateState state)

                        | State reply ->
                            reply.Reply state
                            return! loop state
                    }

                loop initState

            Agent<Msg<_, _>>.Start (eventSubscriber)

        { new ReadModel<'Event, 'State> with
            member _.EventHandler eventEnvelopes =
                agent.PostAndAsyncReply(fun reply -> Notify(eventEnvelopes, reply))

            member _.State() =
                agent.PostAndAsyncReply State |> Async.StartAsTask }
