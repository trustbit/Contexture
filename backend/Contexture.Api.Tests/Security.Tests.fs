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
open Microsoft.AspNetCore.Authentication.JwtBearer
open Giraffe
open Microsoft.AspNetCore.Builder

let securityKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes("6e5ee162-d6a0-40cf-a8cc-c4a60f8d2587"))

module TestHost = 
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.AspNetCore.Authentication

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
            route "/frontend" >=> setStatusCode 200
        ]

    let testJwtBearerScheme _configuration (builder : AuthenticationBuilder) = 
        builder
            .AddJwtBearer(fun options ->
                options.MapInboundClaims <- false

                options.TokenValidationParameters <- TokenValidationParameters(
                    IssuerSigningKey = securityKey,
                    ValidateAudience = false,
                    ValidateIssuer = false
                )
            )
            |> ignore

    let createHost securityConfiguration =
        Host
            .CreateDefaultBuilder()
            .UseEnvironment("Tests")
            .ConfigureServices( fun s ->
                testJwtBearerScheme s |> ignore
                addSecurity (configureAuthentication testJwtBearerScheme configureApiKeyScheme) configureAuthorization securityConfiguration s |> ignore
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
    let securityKey = securityKey
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
    """nested""", """
        {
            "json": {
                "claim": "value"
            }
        }
    """
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

let executeScenario settings (request: unit -> HttpRequestMessage) assertResponse = task {
    use host = TestHost.createHost settings
    do! host.StartAsync() 
    let client = host.GetTestClient()
    let! response = client.SendAsync(request())
    response |> assertResponse
}

module ``using Bearer scheme`` =

    let runScenario = 
        Enabled {
            OIDCAuthentication = Some {
                Audience = ""
                Authority = ""
                ClientId = ""
                ClientSecret = None
                ModifyDataPolicy = Requirements [ 
                    RequireClaim {ClaimType = "group"; AllowedValues = [|"admin"|]}
                    RequireClaim {ClaimType = "nested:json:claim"; AllowedValues = [|"value"|]} 
                ]
                GetDataPolicy = AllowAnonymous
            }
            ApiKeyAuthentication = None
        }
        |> executeScenario

    module ``getting data`` =

        [<Fact>]
        let ``without access token should return ok``() = 
            runScenario
                getData
                returnsOk

        [<Fact>]
        let ``with access token and valid claims should return ok`` () =
            runScenario        
                (getData >> withTokenAndValidClaims)
                returnsOk

        [<Fact>]
        let ``with access token and invalid claims should return ok`` () =
            runScenario        
                (getData >> withTokenAndInvalidClaims)
                returnsOk


    module ``modifying data`` = 

        [<Fact>]
        let ``without access token should return unauthorized``()=
            runScenario
                modifyData
                returnsUnauthorized

        [<Fact>]
        let ``with access token and valid claims should return ok``()=
            runScenario       
                (modifyData >> withTokenAndValidClaims)
                returnsOk
        
        [<Fact>]
        let ``with access token and invalid claims should return forbidden``()=
            runScenario        
                (modifyData >> withTokenAndInvalidClaims)
                returnsForbidden

    module ``accessing frontend routes`` =

        [<Fact>]
        let ``without access token should return ok``() = 
            runScenario        
                accessFrontend
                returnsOk

        [<Fact>]
        let ``with access token and valid claims should return ok`` () =
            runScenario        
                (accessFrontend >> withTokenAndValidClaims)
                returnsOk

        [<Fact>]
        let ``with access token and invalid claims should return ok`` () =
            runScenario        
                (accessFrontend >> withTokenAndInvalidClaims)
                returnsOk


module ``using ApiKeyAuthentication scheme`` = 

    let validApiKey = "test"
    let invalidApiKey = "invalid"

    let withApiKey (apiKey:string) (request :HttpRequestMessage) = 
        request.Headers.Add(Contexture.Api.Infrastructure.ApiKeyAuthentication.HeaderName, apiKey)
        request

    let withValidApiKey = withApiKey validApiKey

    let withInvalidApiKey = withApiKey invalidApiKey

    let runScenario = 
        Enabled {
            OIDCAuthentication = None
            ApiKeyAuthentication = Some {ApiKey = validApiKey}
        }
        |> executeScenario

    module ``getting data`` =

        [<Fact>]
        let ``without api key should return unauthorized`` () =
            runScenario        
                (getData)
                returnsUnauthorized

        [<Fact>]
        let ``with valid api key should return ok`` () =
            runScenario        
                (getData >> withValidApiKey)
                returnsOk

        [<Fact>]
        let ``with invalid api key should return unauthorized`` () =
            runScenario        
                (getData >> withInvalidApiKey)
                returnsUnauthorized


    module ``modifying data`` = 

        [<Fact>]
        let ``without api key should return unauthorized`` () =
            runScenario        
                (modifyData)
                returnsUnauthorized

        [<Fact>]
        let ``with valid api key should return ok``()=
            runScenario       
                (modifyData >> withValidApiKey)
                returnsOk
        
        [<Fact>]
        let ``with invalid api key return unauthorized``()=
            runScenario        
                (modifyData >> withInvalidApiKey)
                returnsUnauthorized

    module ``accessing frontend routes`` =

        [<Fact>]
        let ``without api key should return ok`` () =
            runScenario        
                (accessFrontend)
                returnsOk

        [<Fact>]
        let ``with valid api key return ok`` () =
            runScenario        
                (accessFrontend >> withValidApiKey)
                returnsOk

        [<Fact>]
        let ``with invalid api key return ok`` () =
            runScenario        
                (accessFrontend >> withInvalidApiKey)
                returnsOk