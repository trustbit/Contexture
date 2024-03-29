namespace Contexture.Api.Infrastructure

open System
open Contexture.Api.Infrastructure.NonEmptyList

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

    let create value =
        if value < 0L then None else Some(Position value)

    let parse (value: string) =
        value |> Int64.TryParse |> Option.ofTryGet |> Option.bind create

    let nextPosition (Position value) = Position(value + 1L)

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

module EventEnvelope =
    let box (envelope: EventEnvelope<'E>) =
        { Metadata = envelope.Metadata
          Payload = box envelope.Event
          EventType = typeof<'E>
          StreamKind = StreamKind.Of<'E>() }

    let unbox (envelope: EventEnvelope) : EventEnvelope<'E> =
        { Metadata = envelope.Metadata
          Event = unbox<'E> envelope.Payload }

    let tryUnbox (envelope: EventEnvelope) : EventEnvelope<'E> option =
        envelope.Payload
        |> tryUnbox<'E>
        |> Option.map (fun event ->
            { Metadata = envelope.Metadata
              Event = event })


    let map mapper (envelope: EventEnvelope<'E>) =
        { Metadata = envelope.Metadata
          Event = mapper envelope.Event }

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
    abstract Append: ExpectedVersion -> NonEmptyList<EventDefinition<'Event>> -> Async<Result<Version * Position, AppendError>>

type StreamIdentifier = private StreamIdentifier of StreamKind * EventSource

module StreamIdentifier =
    let name (StreamIdentifier(kind, source)) =
        $"{StreamKind.toString kind}/{source.ToString()}"

    let from (eventSource: EventSource) (kind: StreamKind) = StreamIdentifier(kind, eventSource)
    let source (StreamIdentifier(_, source)) = source
    let kind (StreamIdentifier(kind, _)) = kind
