module Contexture.Api.Infrastructure.Subscriptions

open System.Threading.Tasks
open Contexture.Api.Infrastructure.Storage
open FsToolkit.ErrorHandling

type SubscriptionStatistics =
    {
        CaughtUp : (Position * string) list
        Processing : (Position * string) list
        Failed: (exn * Position option * string) list
        NotRunning : string list
        Stopped : (Position * string) list
    }
    with
        static member Initial =
            { CaughtUp = []
              Processing =[]
              Failed = []
              NotRunning = []
              Stopped = []
            }
    
let caughtUpOrErrored (subscriptions: Subscription List) =
    subscriptions
    |> List.fold (fun (c,e) item ->
        match item.Status with
        | CaughtUp p ->  (p,item.Name) :: c, e
        | Failed (ex,pos) -> c, (ex,pos,item.Name) :: e
        
        | _ -> c,e
    ) (List.empty,List.empty)
    
let calculateStatistics (subscriptions: Subscription List) =
    subscriptions
    |> List.fold (fun state item ->
        match item.Status with
        | CaughtUp p ->  { state with CaughtUp = (p,item.Name) :: state.CaughtUp }
        | Failed (ex,pos) -> { state with Failed = (ex,pos,item.Name) :: state.Failed }
        | NotRunning -> {state with NotRunning = item.Name :: state.NotRunning }
        | Processing p -> { state with Processing = (p, item.Name) :: state.Processing }
        | Stopped p -> { state with Stopped = (p, item.Name) :: state.Stopped }
    ) SubscriptionStatistics.Initial
    
    
let didAllSubscriptionsCatchup caughtUp subscriptions =
    let selectPositions status =
        status |> List.map fst |> List.distinct
    caughtUp |> List.length = (subscriptions |> List.length ) && (caughtUp |> selectPositions |> List.length = 1)

let waitUntilCaughtUp (subscriptions: Subscription List) =
    task {
        let initialStatus = calculateStatistics subscriptions
        let mutable lastStatus = initialStatus
        let mutable counter = 0
        while not(didAllSubscriptionsCatchup lastStatus.CaughtUp subscriptions) do
            do! Task.Delay(100)
            let calculatedStatus = calculateStatistics subscriptions
            lastStatus <- calculatedStatus
            counter <- counter + 1
            if counter > 100 then
                failwithf "No result after %i iterations. Last Status %A" counter lastStatus
    }