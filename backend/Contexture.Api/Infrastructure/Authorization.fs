namespace Contexture.Api.Infrastructure

open System.Text
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open Giraffe
open Microsoft.AspNetCore.Authorization

module Authorization = 

    type RequireClaim = {
        ClaimType: string
        AllowedValues: string array
    }

    type PolicyRequirement = 
    | RequireClaim of RequireClaim

    type PolicySettings =
    | Requirements of PolicyRequirement list
    | AllowAnonymous

    type AuthorizationConfiguration = {
        ModifyDataPolicy: PolicySettings
        GetDataPolicy: PolicySettings
    }

    [<Literal>]
    let ModifyDataPolicyName = "ModifyData"

    [<Literal>]
    let GetDataPolicyName = "GetData"

    let configurePolicy (authorization: AuthorizationOptions) (settings: string * PolicySettings) =
        match settings with
        | (name, AllowAnonymous) ->
            authorization.AddPolicy(name, fun p -> 
                p.RequireAssertion(fun _ctx -> true) 
                |> ignore
            )
        | (name, Requirements requirements) ->
            authorization.AddPolicy(name, fun p->
                requirements
                |> List.iter (fun requirement ->
                    match requirement with
                    | RequireClaim claimRequirement ->
                        p.RequireClaim(claimRequirement.ClaimType, claimRequirement.AllowedValues)
                        // .RequireAuthenticatedUser()
                        |> ignore
                )
            )

    let configureAuthorization config (services : IServiceCollection) =
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(fun options ->
                options.MapInboundClaims <- false
                options.TokenValidationParameters <- new TokenValidationParameters(
                    // todo token validation options should be configurable

                    // By default token should always have signature so does signing key needs to be configured
                    IssuerSigningKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes("6e5ee162-d6a0-40cf-a8cc-c4a60f8d2587")),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = false,
                    ValidateLifetime = false
                )
            )
            |> ignore

        services.AddAuthorization(fun options ->
            [
                ModifyDataPolicyName, config.ModifyDataPolicy
                GetDataPolicyName, config.GetDataPolicy
            ]
            |> List.iter (configurePolicy options)
        )

    let satisfiesPolicy policyName = 
        authorizeByPolicyName policyName (setStatusCode 401)

    let authorizeByGetDataPolicy = satisfiesPolicy GetDataPolicyName

    let authorizeByModifyDataPolicy = satisfiesPolicy ModifyDataPolicyName

    let authorize: HttpHandler = fun next ctx ->
        match ctx.Request.Method with
        | "GET" ->
            authorizeByGetDataPolicy next ctx 
        | "PUT"
        | "POST"
        | "PATCH"
        | "DELETE" ->
            authorizeByModifyDataPolicy next ctx
        | _ -> 
            RequestErrors.METHOD_NOT_ALLOWED ctx.Request.Method next ctx

    
