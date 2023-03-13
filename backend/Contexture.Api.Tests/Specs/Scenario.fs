namespace Contexture.Api.Tests

open System.Net
open System.Net.Http
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Storage


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

    let deleting (url: string) (environment: TestHostEnvironment) =
        task {
            let! result = environment.Client.DeleteAsync(url)
            return result
        }
    
    let postingJson (url: string) (jsonContent: string) (environment: TestHostEnvironment) =
        task {
            let! result = environment.Client.PostAsync(url, new StringContent(jsonContent))
            return result.EnsureSuccessStatusCode()
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
