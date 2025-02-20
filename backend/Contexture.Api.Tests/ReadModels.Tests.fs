module Contexture.Api.ReadModels.Tests

open System
open System.Threading.Tasks
open Xunit

open Contexture.Api.Infrastructure.ReadModels
open Contexture.Api.Infrastructure

module State =
    let Initial = "initialstate"

let timeoutms = (TimeSpan.FromSeconds 1).TotalMilliseconds |> int
let testReadModel: ReadModel<string,string> = 
    readModel (fun state _events -> state) State.Initial (Some timeoutms)

[<Fact>]
let ``fetching state when requested position is ahead of processed should throw after timeout``() = task{
    let getState = fun () -> testReadModel.State(Some(Position.from 1)) :> Task
    do! Assert.ThrowsAsync<TimeoutException>(fun () -> Task.Run(getState, (new Threading.CancellationTokenSource(TimeSpan.FromSeconds(2))).Token)) :> Task
}