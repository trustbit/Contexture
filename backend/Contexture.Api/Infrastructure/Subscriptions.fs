namespace Contexture.Api.Infrastructure.Subscriptions

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Contexture.Api.Infrastructure

type SubscriptionDefinition =
    | FromAll of SubscriptionStartingPosition
    | FromKind of StreamKind * SubscriptionStartingPosition
    | FromStream of StreamIdentifier * Version option

and SubscriptionStartingPosition =
    | Start
    | From of Position
    | End

type Subscription =
    inherit System.IAsyncDisposable
    abstract Name: string
    abstract Status: SubscriptionStatus

and SubscriptionStatus =
    | NotRunning
    | Processing of current: Position
    | CaughtUp of at: Position
    | Failed of exn * at: Position option
    | Stopped of at: Position


type SubscriptionHandler = Position -> EventEnvelope list -> Async<unit>

type SubscriptionHandler<'E> = Position -> EventEnvelope<'E> list -> Async<unit>

module SubscriptionHandler =
    let trackPosition tracker (subscription: SubscriptionHandler<_>) : SubscriptionHandler<_> =
        fun position events -> async {
            do! subscription position events
            do! tracker position
            }

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

module Runtime =
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
        
    let didAllSubscriptionsCatchup caughtUpSubscriptions subscriptions =
        let positions =
            caughtUpSubscriptions |> List.map fst |> List.distinct
        (positions |> List.length = 1) && (List.length caughtUpSubscriptions) = (List.length subscriptions )

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