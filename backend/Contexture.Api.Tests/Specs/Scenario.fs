namespace Contexture.Api.Tests

open System.Collections.Generic
open System.Net
open System.Net.Http
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Infrastructure.Subscriptions
open Contexture.Api.Reactions
open Contexture.Api.Tests.TestHost
open FsToolkit.ErrorHandling
open TestHost

type Given = EventEnvelope list
type WhenResult<'Result> =
        { TestEnvironment: TestHostEnvironment
          Result: 'Result
          Changes: EventEnvelope<AllEvents> list }

type When<'R> = TestHostEnvironment -> Task<WhenResult<'R>>
type ThenAssertion<'T> = WhenResult<'T> -> Task

module Given =
    let noEvents = []
    let anEvent event = [ event ]
    let andOneEvent event given = given @ [ event ]
    let andEvents events given = given @ events

module WhenResult =
    open System.Net.Http
    open System.Net.Http.Json
    
    let withResult newResult (result: WhenResult<_>) =
        { TestEnvironment = result.TestEnvironment
          Changes = result.Changes
          Result = newResult }
    let map mapper (result: WhenResult<_>) =
        result |> withResult (mapper result.Result) 
        
    let events chooser { Changes = events} =
        events 
        |> List.map (fun e -> e.Event)
        |> List.choose chooser
        
    let asJsonResponse<'a> (result:WhenResult<HttpResponseMessage>) : Task<WhenResult<'a>> =
        task {
            let! content = result.Result.Content.ReadAsStringAsync()
            if result.Result.IsSuccessStatusCode then
                let! response =
                    try
                        result.Result.Content.ReadFromJsonAsync<'a>()
                    with e ->
                        failwith $"Could not deserialize '%s{typeof<'a>.FullName}':\n\n%s{e.Message} from content:\n\n%s{content}"

                return result |> withResult response 
            else
                return failwith $"Could not get from %s{result.Result.RequestMessage.RequestUri.ToString()}: %O{result} %s{content}"
        }

module When =
    open System.Net.Http.Json
    
    let private captureEvents action (environment: TestHostEnvironment) = task {
        let mutable capturedEvents = List()
        let captureEvents =
            fun position events ->
                capturedEvents.AddRange events
                Async.retn ()
        let store = environment.GetService<EventStore>()
        let! subscription = store.SubscribeAll AllEvents.fromEnvelope "capture events" Subscriptions.End captureEvents
        let! result = action environment
        do! Runtime.waitUntilCaughtUp [ subscription ]
        do! Task.Delay 1000
        return {
            TestEnvironment = environment
            Changes = capturedEvents |> List.ofSeq
            Result =result
        }
    }
    let deleting (url: string) (environment: TestHostEnvironment) =
        task {
            return! environment |> captureEvents (fun env -> env.Client.DeleteAsync(url))
        }
    
    let postingJson (url: string) (jsonContent: string) (environment: TestHostEnvironment) =
        task {
            return! environment |> captureEvents (fun env -> env.Client.PostAsync(url, new StringContent(jsonContent)))
        }

    let getting (url: string) (environment: TestHostEnvironment) =
        task {
            return! environment |> captureEvents (fun env -> env.Client.GetAsync(url))
        }
    let gettingJson<'t> (url: string) (environment: TestHostEnvironment) =
        environment
        |> getting url
        |> Task.bind WhenResult.asJsonResponse<'t>

type Then = Xunit.Assert
module Then =
    module Items =
        let areEmpty { Result = items: #seq<_> } =
            Then.Empty items
        let areNotEmpty { Result = items: #seq<_> } =
            Then.NotEmpty items
        let contains (item:'a) { Result = items: #seq<'a> } =
            Then.Contains (item, items)
    module Events =
        let arePublished { Changes = events } =
            Then.NotEmpty events
    open When
    module Response =
        let shouldNotBeSuccessful { Result = response: HttpResponseMessage}=
            Then.Equal(false, response.IsSuccessStatusCode)
        let shouldBeSuccessful { Result = response: HttpResponseMessage} =
            ignore <| response.EnsureSuccessStatusCode()
            
        let shouldHaveStatusCode (statusCode: HttpStatusCode) { Result = response: HttpResponseMessage} =
            Then.Equal(statusCode,response.StatusCode)

module Utils =
    open Contexture.Api.Tests.EnvironmentSimulation
    open Xunit

    let asEvent id event =
        EventDefinition.from id event

    let singleEvent<'e> (eventStore: EventStore) : Async<EventEnvelope<'e>> = async {
        let! _,events = eventStore.AllStreams<'e>()
        return Then.Single events
    }
