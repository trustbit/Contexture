namespace Contexture.Api.Tests.EnvironmentSimulation

open System
open System.Threading

type Clock = unit -> DateTime

module Clock =

    let systemClock = fun () -> DateTime.UtcNow

    let currentInstant (clock: Clock) = clock ()

    let dateTimeUtc (clock: Clock) =
        clock
        |> currentInstant
        |> fun t -> t.ToUniversalTime()

type ISimulateEnvironment =
    abstract Time : unit -> System.DateTime
    abstract NextId : unit -> int

type FixedTimeEnvironment(now: DateTime, seed: int) =
    let ids = ref seed

    let nextId () = Interlocked.Increment(ids)

    static member FromInstance(now: DateTime) =
        FixedTimeEnvironment(now, 0) :> ISimulateEnvironment

    static member FromClock(clock: Clock) =
        FixedTimeEnvironment.FromInstance(Clock.currentInstant clock)

    static member FromSystemClock() =
        FixedTimeEnvironment.FromClock(Clock.systemClock)

    interface ISimulateEnvironment with
        member this.NextId() = nextId ()
        member __.Time() = now


[<RequireQualifiedAccess>]
module PseudoRandom =
    let sequentialGuidString number = sprintf "00000000-6c78-4f2e-0000-%012i" number
    let sequentialGuid = sequentialGuidString >> Guid
    let guid (env: ISimulateEnvironment) = env.NextId() |> sequentialGuid
    let nameWithGuid prefix (env: ISimulateEnvironment) =
        $"%s{prefix}-%O{guid env}"