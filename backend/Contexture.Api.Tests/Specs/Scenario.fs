namespace Contexture.Api.Tests

open System.Collections.Generic
open System.Net
open System.Net.Http
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage
open Contexture.Api.Infrastructure.Subscriptions
open Contexture.Api.Reactions
open FsToolkit.ErrorHandling


type Given = EventEnvelope list

module Given =
    let noEvents = []
    let anEvent event = [ event ]
    let andOneEvent event given = given @ [ event ]
    let andEvents events given = given @ events

module When =
    open System.Net.Http.Json
    open System.Net.Http

    open TestHost

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
        return capturedEvents |> List.ofSeq,result
    }
    let deleting (url: string) (environment: TestHostEnvironment) =
        task {
            return! environment |> captureEvents (fun env -> env.Client.DeleteAsync(url))
        }
    
    let postingJson (url: string) (jsonContent: string) (environment: TestHostEnvironment) =
        task {
            return! environment |> captureEvents (fun env -> env.Client.PostAsync(url, new StringContent(jsonContent)))
        }

    let gettingJson<'t> (url: string) (environment: TestHostEnvironment) =
        task {        
            let! result = environment.Client.GetAsync(url)
            if result.IsSuccessStatusCode then
                let! content = result.Content.ReadFromJsonAsync<'t>()
                return content
            else
                let! content = result.Content.ReadAsStringAsync() 
                raise (Xunit.Sdk.XunitException($"Could not get from %O{url}: %O{result} %s{content}"))
                return Unchecked.defaultof<'t>
        }

type Then = Xunit.Assert
module Then =
    module Response =
        let shouldNotBeSuccessful (response: HttpResponseMessage) =
            Then.Equal(false, response.IsSuccessStatusCode)
        let shouldBeSuccessful (response: HttpResponseMessage) =
            ignore <| response.EnsureSuccessStatusCode()
            
        let shouldHaveStatusCode (statusCode: HttpStatusCode) (response: HttpResponseMessage) =
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
