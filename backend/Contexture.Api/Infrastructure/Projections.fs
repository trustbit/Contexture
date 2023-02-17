module Contexture.Api.Infrastructure.Projections


type Projection<'State, 'Event> =
    { Init: 'State
      Update: 'State -> 'Event -> 'State }

let projectIntoMap selectId projection =
    fun state (eventEnvelope: EventEnvelope<_>) ->
        let selectedId = selectId eventEnvelope

        state
        |> Map.tryFind selectedId
        |> Option.defaultValue projection.Init
        |> fun projectionState -> eventEnvelope.Event |> projection.Update projectionState
        |> fun newState -> state |> Map.add selectedId newState

let projectIntoMapBySourceId projection =
    projectIntoMap (fun eventEnvelope -> eventEnvelope.Metadata.Source) projection

let project projection (events: EventEnvelope<_> list) =
    events
    |> List.map (fun e -> e.Event)
    |> List.fold projection.Update projection.Init
