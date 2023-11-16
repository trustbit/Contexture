module Contexture.Api.Tests.Auth

open System
open System.Text
open System.Security.Claims
open System.Net.Http
open Microsoft.IdentityModel.Tokens
open System.IdentityModel.Tokens.Jwt
open Xunit
open System.Net
open Contexture.Api.Infrastructure.Authorization
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Microsoft.AspNetCore.TestHost
open Microsoft.AspNetCore.Hosting
open Giraffe
open Microsoft.AspNetCore.Builder

module TestHost = 

    let configureLogging (builder: ILoggingBuilder) =
            builder
                .AddSimpleConsole(fun f -> f.IncludeScopes <- true)
                .AddDebug()
            |> ignore

    let testRoutes = 
            authorize 
            >=> choose [
                GET >=> setStatusCode 200
                POST >=> setStatusCode 200
            ]

    let createHost authoriationConfiguration =
        Host
            .CreateDefaultBuilder()
            .UseEnvironment("Tests")
            .ConfigureServices(configureAuthorization authoriationConfiguration >> ignore)
            .ConfigureWebHostDefaults(fun webHostBuilder ->
                webHostBuilder
                    .Configure(Action<IApplicationBuilder> (fun builder-> 
                        builder
                            .UseAuthentication()
                            .UseAuthorization()
                            .UseGiraffe(testRoutes)
                    ))
                    .UseTestServer()
                    .ConfigureLogging(configureLogging)
                    |> ignore)
            .Build()

let getData () =
    new HttpRequestMessage(HttpMethod.Get,"")

let modifyData() =
    let request = new HttpRequestMessage(HttpMethod.Post,"")
    request

let asAnonymousUser request :HttpRequestMessage = request

let withToken (claims: (string * string) list) (request :HttpRequestMessage) = 
    let securityKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes("6e5ee162-d6a0-40cf-a8cc-c4a60f8d2587"))
    securityKey.KeyId <- "keyid"
    let credentials = SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
    let tokenClaims = claims |> List.map Claim
    let token = JwtSecurityToken(
        claims = tokenClaims,
        expires = DateTime.Now.AddMinutes(30),
        signingCredentials = credentials
    )
    let tokenHandler = new JwtSecurityTokenHandler();
    let tokenString = tokenHandler.WriteToken(token)
    request.Headers.Add("Authorization", $"Bearer {tokenString}")

    request

let asAuthorizedUser = withToken [
    JwtRegisteredClaimNames.Sub, "admin"
    "group", "admin"
]

let asUnauthorizedUser = withToken [
    JwtRegisteredClaimNames.Sub, "admin"
    "group", "invalid-claim"
]

let returns statusCode (response:HttpResponseMessage) =
    Assert.Equal(statusCode, response.StatusCode)

let returnsOk = returns HttpStatusCode.OK

let returnsUnauthorized = returns HttpStatusCode.Unauthorized

let authConfig = {
    ModifyDataPolicy = Requirements [ RequireClaim {ClaimType = "group"; AllowedValues = [|"admin"|]} ]
    GetDataPolicy = AllowAnonymous
}

let executeScenario (request: unit -> HttpRequestMessage) assertResponse = task {
    use host = TestHost.createHost authConfig
    do! host.StartAsync() 
    let client = host.GetTestClient()
    let! response = client.SendAsync(request())
    response |> assertResponse
}

module ``When getting data`` =
    [<Fact>]
    let ``as anonymous user should returns ok``() = 
        executeScenario        
            (getData >> asAnonymousUser)
            returnsOk

    [<Fact>]
    let ``as authorized user should returns ok`` () =
        executeScenario        
            (getData >> asAuthorizedUser)
            returnsOk

    [<Fact>]
    let ``as unauthorized user should returns ok`` () =
        executeScenario        
            (getData >> asAuthorizedUser)
            returnsOk


module ``When modifying data`` =
    [<Fact>]
    let ``as anonymous user should returns unauthorized``()=
        executeScenario        
            (modifyData >> asAnonymousUser)
            returnsUnauthorized

    [<Fact>]
    let ``as authorized user returns ok``()=
        executeScenario        
            (modifyData >> asAuthorizedUser)
            returnsOk

    [<Fact>]
    let ``as unauthorized user returns unauthorized``()=
        executeScenario        
            (modifyData >> asUnauthorizedUser)
            returnsUnauthorized
