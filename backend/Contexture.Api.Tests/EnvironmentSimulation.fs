namespace Contexture.Api.Tests.EnvironmentSimulation

open System
open System.Threading

type Clock = unit -> System.DateTime

module Clock =
    
    let systemClock =
        fun () -> System.DateTime.UtcNow

    let currentInstant (clock: Clock) = clock()
    let dateTimeUtc (clock: Clock) =
        clock
        |> currentInstant
        |> fun t -> t.ToUniversalTime()

type ISimulateEnvironment =
    abstract Time: unit -> System.DateTime
    abstract NextId: unit -> int

type FixedTimeEnvironment(now: DateTime, seed: int) =
    let ids = ref seed

    let nextId () =
        Interlocked.Increment(ids)

    static member FromInstance(now: DateTime) =
        FixedTimeEnvironment(now, 0) :> ISimulateEnvironment

    static member FromClock(clock: Clock) =
        FixedTimeEnvironment.FromInstance(Clock.currentInstant clock)

    static member FromSystemClock() =
        FixedTimeEnvironment.FromClock(Clock.systemClock)

    interface ISimulateEnvironment with
        member this.NextId() = nextId ()
        member __.Time() = now
