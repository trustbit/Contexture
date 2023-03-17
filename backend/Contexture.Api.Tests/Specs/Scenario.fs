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
        do! Runtime.waitUntilCaughtUp (subscription :: environment.Subscriptions)
        // do! Task.Delay 1000
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
    module theResponseShould =
        type HttpWhenResult = WhenResult<HttpResponseMessage>

        let private responseBodyLogLine (response:HttpResponseMessage) = task {
            let! body = response.Content.ReadAsStringAsync()
            return $"\n----- Details as received from the server -----\n%s{body}\n----- end of server details -----\n"
            }

        let private nonSuccessAssertions expectedStatusCode (result: HttpWhenResult) : Task = upcast task {
            let response = result.Result
            if response.IsSuccessStatusCode || not(response.StatusCode = expectedStatusCode)then
                let! bodyResponse = responseBodyLogLine response
                failwith $"Expected a {int expectedStatusCode}-{expectedStatusCode.ToString()} StatusCode but got %O{int response.StatusCode} %s{response.ReasonPhrase}.%s{bodyResponse}"
            }

        let private successAssertion expectedStatusCode (result: HttpWhenResult) : Task = upcast task {
            let response = result.Result
            if not response.IsSuccessStatusCode || expectedStatusCode |> Option.exists(fun expected -> not (response.StatusCode = expected)) then
                let! bodyResponse = responseBodyLogLine response
                failwith $"Expected a success StatusCode but got %O{response.StatusCode} %s{response.ReasonPhrase}.%s{bodyResponse}"
        }

        let beASuccessfulJson (result: HttpWhenResult) : Task = upcast task {
            do! result |> successAssertion None
            let response = result.Result
            if not(response.Content.Headers.ContentType.MediaType = "application/json") then
                let! bodyResponse = responseBodyLogLine response
                failwith $"Expected JSON but got %s{response.Content.Headers.ContentType.MediaType}.%s{bodyResponse}"
        }

        let beASuccessfulJsonWith (assertions: WhenResult<'a> -> unit) (result:HttpWhenResult) : Task = task {
            let! response = result |> WhenResult.asJsonResponse
            return assertions response
        }

        let beSuccessful (result: HttpWhenResult) = successAssertion None result
        let beForbidden (result: HttpWhenResult) = nonSuccessAssertions HttpStatusCode.Forbidden result

        let beUnauthorized (result: HttpWhenResult) = nonSuccessAssertions HttpStatusCode.Unauthorized result

        let beOk (result: HttpWhenResult) = successAssertion (Some HttpStatusCode.OK) result

        let beInternalServerError (result: HttpWhenResult) = nonSuccessAssertions HttpStatusCode.InternalServerError result

        let beConflict (result: HttpWhenResult) = nonSuccessAssertions HttpStatusCode.Conflict result

        let beNotFound (result: HttpWhenResult)  = nonSuccessAssertions HttpStatusCode.NotFound result

        let beBadRequest (result: HttpWhenResult) = nonSuccessAssertions HttpStatusCode.BadRequest result

        let beARedirectTo path (result: HttpWhenResult) : Task = upcast task {
            let response = result.Result
            if response.StatusCode <> HttpStatusCode.Redirect || response.Headers.Location <> path then
                let! body = response.Content.ReadAsStringAsync()
                failwith $"Expected a Redirect to %O{path} but got %O{response.Headers.Location} with %O{response.StatusCode} instead. Details from the server:\n%s{body}"
        }

module Utils =
    open Contexture.Api.Tests.EnvironmentSimulation
    open Xunit

    let asEvent id event =
        EventDefinition.from id event

    let singleEvent<'e> (eventStore: EventStore) : Async<EventEnvelope<'e>> = async {
        let! _,events = eventStore.AllStreams<'e>()
        return Then.Single events
    }
