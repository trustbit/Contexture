module Contexture.Api.Tests.Security

open System
open System.Text
open System.Security.Claims
open System.Net.Http
open Microsoft.IdentityModel.Tokens
open System.IdentityModel.Tokens.Jwt
open Xunit
open System.Net
open Contexture.Api.Infrastructure.Security
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
        choose [
            route "/api" 
            >=> protectApiRoutes 
            >=> choose [
                GET  >=> setStatusCode 200
                POST  >=> setStatusCode 200
            ]
            route "/frontend" >=> protectFrontendRoutes >=> setStatusCode 200
        ]

    let createHost securityConfiguration =
        Host
            .CreateDefaultBuilder()
            .UseEnvironment("Tests")
            .ConfigureServices( fun s -> 
                configure securityConfiguration s |> ignore
            )
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
    new HttpRequestMessage(HttpMethod.Get,"/api")

let modifyData() =
    let request = new HttpRequestMessage(HttpMethod.Post,"/api")
    request

let accessFrontend() =
    new HttpRequestMessage(HttpMethod.Get,"/frontend")

let createToken (claims: (string * string) list) = 
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
    tokenHandler.WriteToken(token)

let withToken (claims: (string * string) list) (request :HttpRequestMessage) = 
    let token = createToken claims
    request.Headers.Add("Authorization", $"Bearer {token}")
    request

let withTokenAndValidClaims = withToken [
    JwtRegisteredClaimNames.Sub, "admin"
    "group", "admin"
]

let withTokenAndInvalidClaims = withToken [
    JwtRegisteredClaimNames.Sub, "admin"
    "group", "invalid-claim"
]

let returns statusCode (response:HttpResponseMessage) =
    Assert.Equal(statusCode, response.StatusCode)

let returnsOk = returns HttpStatusCode.OK

let returnsUnauthorized = returns HttpStatusCode.Unauthorized

let returnsForbidden = returns HttpStatusCode.Forbidden

let securitySettings = {
    AuthenticationScheme = Bearer { IssuerSigningKey = "6e5ee162-d6a0-40cf-a8cc-c4a60f8d2587" }
    ModifyDataPolicy = Requirements [ RequireClaim {ClaimType = "group"; AllowedValues = [|"admin"|]} ]
    GetDataPolicy = AllowAnonymous
}

let executeScenario settings (request: unit -> HttpRequestMessage) assertResponse = task {
    use host = TestHost.createHost settings
    do! host.StartAsync() 
    let client = host.GetTestClient()
    let! response = client.SendAsync(request())
    response |> assertResponse
}

module ``using Bearer scheme`` =

    let usingBearerScheme = 
        Enabled {
            AuthenticationScheme = Bearer { IssuerSigningKey = "6e5ee162-d6a0-40cf-a8cc-c4a60f8d2587" }
            ModifyDataPolicy = Requirements [ RequireClaim {ClaimType = "group"; AllowedValues = [|"admin"|]} ]
            GetDataPolicy = AllowAnonymous
        }
        |> executeScenario

    module ``getting data`` =

        [<Fact>]
        let ``without access token should return ok``() = 
            usingBearerScheme
                getData
                returnsOk

        [<Fact>]
        let ``with access token and valid claims should return ok`` () =
            usingBearerScheme        
                (getData >> withTokenAndValidClaims)
                returnsOk

        [<Fact>]
        let ``with access token and invalid claims should return ok`` () =
            usingBearerScheme        
                (getData >> withTokenAndInvalidClaims)
                returnsOk


    module ``modifying data`` = 

        [<Fact>]
        let ``without access token should return unauthorized``()=
            usingBearerScheme
                modifyData
                returnsUnauthorized

        [<Fact>]
        let ``with access token and valid claims should return ok``()=
            usingBearerScheme       
                (modifyData >> withTokenAndValidClaims)
                returnsOk
        
        [<Fact>]
        let ``with access token and invalid claims should return forbidden``()=
            usingBearerScheme        
                (modifyData >> withTokenAndInvalidClaims)
                returnsForbidden

    module ``accessing frontend routes`` =

        [<Fact>]
        let ``without access token should return ok``() = 
            usingBearerScheme        
                accessFrontend
                returnsOk

        [<Fact>]
        let ``with access token and valid claims should return ok`` () =
            usingBearerScheme        
                (accessFrontend >> withTokenAndValidClaims)
                returnsOk

        [<Fact>]
        let ``with access token and invalid claims should return ok`` () =
            usingBearerScheme        
                (accessFrontend >> withTokenAndInvalidClaims)
                returnsOk
