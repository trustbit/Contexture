module Tests

open Contexture.Api
open Xunit
open Database

[<Fact>]
let ``Unversioned JSON deserialization`` () =
    let exampleInputPath = @"../../../../../example/restaurant-db.json"
    
    let expectedJson = exampleInputPath |> Persistence.read
    
    let parsedRoot = expectedJson |> Serialization.deserialize
    let resultJson = parsedRoot |> Serialization.serialize
    
    (@"../../../../../example/restaurant-db-parsed.json", resultJson) ||> Persistence.save
    
    ()