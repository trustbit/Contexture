namespace Contexture.Api.Infrastructure

open System

type EventSource = Guid

type StreamKind =
    private
    | SystemType of string

    static member Of(systemType: Type) =
        if isNull systemType then
            nullArg <| nameof systemType

        SystemType systemType.FullName

    static member Of<'E>() = StreamKind.Of typeof<'E>
    static member Of(_: 'E) = StreamKind.Of typeof<'E>

module StreamKind =

    let toString (SystemType systemType) = systemType

    let ofString (value: string) =
        if String.IsNullOrWhiteSpace value then
            nullArg <| nameof value

        SystemType value


type Version = private Version of int64

module Version =
    let start = Version 0

    let from value =
        if value < 0 then
            invalidArg $"Value must not be smaller 0 but is {value}" (nameof value)

        Version value

    let value (Version value) = value

type Position = private Position of int64

module Position =
    let start = Position 0
    let value (Position value) = value

    let from value =
        if value < 0L then
            invalidArg $"Value must not be smaller 0 but is {value}" (nameof value)

        Position value

type EventMetadata =
    { Source: EventSource
      RecordedAt: System.DateTimeOffset
      Position: Position
      Version: Version }

type EventEnvelope<'Event> =
    { Metadata: EventMetadata
      Event: 'Event }

type EventEnvelope =
    { Metadata: EventMetadata
      Payload: obj
      EventType: System.Type
      StreamKind: StreamKind }

type SubscriptionHandler = EventEnvelope list -> Async<unit>

type SubscriptionHandler<'E> = EventEnvelope<'E> list -> Async<unit>

module EventEnvelope =
    let box (envelope: EventEnvelope<'E>) =
        { Metadata = envelope.Metadata
          Payload = box envelope.Event
          EventType = typeof<'E>
          StreamKind = StreamKind.Of<'E>() }

    let unbox (envelope: EventEnvelope) : EventEnvelope<'E> =
        { Metadata = envelope.Metadata
          Event = unbox<'E> envelope.Payload }

type EventResult = Result<Position * EventEnvelope list, string>
type EventResult<'e> = Result<Position * EventEnvelope<'e> list, string>
type StreamResult = Result<Version * EventEnvelope list, string>
type StreamResult<'e> = Result<Version * EventEnvelope<'e> list, string>

type EventDefinition<'Event> = 'Event

type AppendError =
    | LockingConflict of currentVersion: Version * exn
    | UnknownError of exn

type ExpectedVersion =
    | Empty
    | AtVersion of Version
    | Unknown

type EventStream<'Event> =
    abstract Read: Version -> Async<StreamResult<'Event>>
    abstract Append: ExpectedVersion -> EventDefinition<'Event> list -> Async<Result<Version * Position, AppendError>>
