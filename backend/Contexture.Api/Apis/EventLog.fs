namespace Contexture.Api.Apis

open System
open Contexture.Api.Infrastructure.ReadModels
open Contexture.Api.ReadModels
open Giraffe

module EventLog =

    let getEventsForEntity (entityId: Guid) : HttpHandler = fun next ctx -> task {
        let! state = ctx |> State.fetch State.fromReadModel<EventLog.EventLogReadModel>
        let events = EventLog.eventsForEntity state entityId

        return! json events next ctx
    }
                       
    let routes : HttpHandler =
        subRouteCi "/event-log"
            (choose [
                subRoutef "/%O"
                    (fun (entityId:Guid) ->
                        GET >=> getEventsForEntity entityId
                    )
                RequestErrors.NOT_FOUND "Not found"
            ])
