module Tests

open Contexture.Api
open Xunit

[<Fact>]
let ``Unversioned JSON deserialization`` () =
    let exampleInputPath = "../../../../example/restaurant-db.json"
    
    let expectedJson = exampleInputPath |> Database.Persistence.read
    
    let parsedRoot = expectedJson |> Database.Serialization.deserialize
    let resultJson = parsedRoot |> Database.Serialization.serialize
    
    ("../../../../example/restaurant-db-parsed.json", resultJson) ||> Database.Persistence.save
    
    (expectedJson, resultJson) |> Assert.Equal