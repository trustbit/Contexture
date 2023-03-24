module Tests

open Contexture.Api
open Xunit
open FileBased.Database

[<Fact>]
let ``Unversioned JSON deserialization`` () = task {
    let exampleInputPath = @"../../../../../example/restaurant-db.json"
    
    let! expectedJson = exampleInputPath |> Persistence.read
    
    let parsedRoot = expectedJson |> Serialization.deserialize
    
    Assert.NotEmpty(parsedRoot.Collaborations)
    Assert.NotEmpty(parsedRoot.Domains)
    Assert.NotEmpty(parsedRoot.BoundedContexts)
    Assert.True(parsedRoot.Version.IsSome)
    let resultJson = parsedRoot |> Serialization.serialize
    
    Assert.NotEmpty resultJson
    }