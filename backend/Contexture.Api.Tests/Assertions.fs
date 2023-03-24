module Contexture.Api.Tests.Assertions

type Then = Xunit.Assert
module Then =
    let expectOk (result: Async<Result<'r, _>>) : Async<unit> =
        async {
            match! result with
            | Ok _ -> return ()
            | Error e -> return failwithf "Expected an Ok result but got Error:\n%O" e
        }

    let resultOrFail (result: Async<Result<'r, _>>) : Async<'r> =
        async {
            match! result with
            | Ok r -> return r
            | Error e -> return failwithf "Expected an Ok result but got Error:\n%O" e
        }
