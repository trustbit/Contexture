namespace Contexture.Api.Infrastructure

open System.Text
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open Giraffe
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Authentication.Cookies
open Microsoft.AspNetCore.Authentication.OpenIdConnect

module Security = 

    type RequireClaim = {
        ClaimType: string
        AllowedValues: string array
    }

    type PolicyRequirement = 
    | RequireClaim of RequireClaim

    type PolicyRequirements =
    | Requirements of PolicyRequirement list
    | AllowAnonymous
    
    type OIDCSchemeSettings = {
        Authority: string
        ClientId: string
        ClientSecret: string option
        CookieName: string option
    }

    type BearerSchemeSettings = {
        IssuerSigningKey: string
    }

    type AuthenticationScheme =
    | OIDC of OIDCSchemeSettings
    | Bearer of BearerSchemeSettings

    type SecuritySettings = {
        AuthenticationScheme: AuthenticationScheme
        ModifyDataPolicy: PolicyRequirements
        GetDataPolicy: PolicyRequirements
    }

    type ContextureSecurity = 
    | Enabled of SecuritySettings
    | Disabled

    [<Literal>]
    let ModifyDataPolicyName = "ModifyData"

    [<Literal>]
    let GetDataPolicyName = "GetData"

    let configurePolicy (authorization: AuthorizationOptions) (name: string, requirements)  = 
        match requirements with
        | Requirements requirementList ->
            authorization.AddPolicy(name, fun p->
                p.RequireAuthenticatedUser() |> ignore

                requirementList
                |> List.iter (fun requirement ->
                    match requirement with
                    | RequireClaim claimRequirement ->
                        p.RequireClaim(claimRequirement.ClaimType, claimRequirement.AllowedValues) |> ignore
                )
            )
        | AllowAnonymous ->
            authorization.AddPolicy(name, fun p ->
                p.RequireAssertion(fun _-> true)|> ignore
            )

    let configureOIDCScheme settings (services : IServiceCollection) = 
        services
            .AddAuthentication(fun options ->
                options.DefaultScheme <- CookieAuthenticationDefaults.AuthenticationScheme
                options.DefaultChallengeScheme <- OpenIdConnectDefaults.AuthenticationScheme
            )
            .AddCookie(fun options ->
                settings.CookieName |> Option.iter(fun name -> options.Cookie.Name <- name)
            )
            .AddOpenIdConnect(fun options ->
                options.Authority <- settings.Authority
                options.ClientId <- settings.ClientId
                settings.ClientSecret |> Option.iter(fun x-> options.ClientSecret <- x)
                options.ResponseType <- "code"
                options.SaveTokens <- true
                options.RequireHttpsMetadata <- false
                options.MapInboundClaims <- false
            );

    let configureBearerScheme settings (services : IServiceCollection) =
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(fun options ->
                options.MapInboundClaims <- false
                options.TokenValidationParameters <- new TokenValidationParameters(
                    // todo token validation options should be configurable

                    // By default token should always have signature so does signing key needs to be configured
                    IssuerSigningKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.IssuerSigningKey)),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = false,
                    ValidateLifetime = false
                )
            )


    let configureAuthentication configuration (services : IServiceCollection) = 
        match configuration.AuthenticationScheme with
        | OIDC settings -> configureOIDCScheme settings services |> ignore
        | Bearer settings -> configureBearerScheme settings services |> ignore

        services

    let configureAuthorization configuration (services : IServiceCollection) = 
        let policies = [
                ModifyDataPolicyName, configuration.ModifyDataPolicy
                GetDataPolicyName, configuration.GetDataPolicy
            ]
        services.AddAuthorization(fun options -> policies |> List.iter (configurePolicy options))    

    let configure security (services : IServiceCollection) =
        services.AddSingleton<ContextureSecurity>(fun _ -> security) |> ignore
        
        match security with
        | Enabled configuration ->
            services
            |> configureAuthentication configuration
            |> configureAuthorization configuration
        | Disabled -> services

    let allowAnonymous: HttpHandler = fun next ctx -> next ctx

    let authorizationFailed : HttpHandler = fun next ctx -> 
        // distinguish between authentication and authorization failure reasons
        match ctx.User.Identity.IsAuthenticated with
        | true -> setStatusCode 403 next ctx
        | false -> setStatusCode 401 next ctx

    let authorizeBy policyName: HttpHandler = fun next ctx ->
        authorizeByPolicyName policyName authorizationFailed next ctx

    let protectApiRoutes: HttpHandler = fun next ctx ->
        let contextureSecurity = ctx.RequestServices.GetRequiredService<ContextureSecurity>()
        match contextureSecurity with
        | Enabled _ ->
            match ctx.Request.Method with
            | "GET" ->
                authorizeBy GetDataPolicyName next ctx 
            | "PUT"
            | "POST"
            | "PATCH"
            | "DELETE" ->
                authorizeBy ModifyDataPolicyName next ctx
            | _ -> 
                RequestErrors.METHOD_NOT_ALLOWED ctx.Request.Method next ctx
        | _-> next ctx

    let mustBeLoggedIn = 
        requiresAuthentication (challenge OpenIdConnectDefaults.AuthenticationScheme)

    let protectFrontendRoutes: HttpHandler = fun next ctx ->
        let contextureSecurity = ctx.RequestServices.GetRequiredService<ContextureSecurity>()
        match contextureSecurity with
        | Enabled settings ->
            match settings.AuthenticationScheme with
            | OIDC _ -> mustBeLoggedIn next ctx
            | Bearer _-> allowAnonymous next ctx
        | Disabled -> allowAnonymous next ctx


    module Configuration = 
        open Microsoft.Extensions.Configuration
        open Microsoft.Extensions.Options

        [<CLIMutable>]
        type ClaimRequirementOptions = {
        ClaimType: string
        AllowedValues : string array
        }

        [<CLIMutable>]
        type PolicyOptions = {
        RequiredClaims: ClaimRequirementOptions array
        }

        [<CLIMutable>]
        type AuthorizationOptions = {
            ModifyData: PolicyOptions
            GetData: PolicyOptions
        }

        [<CLIMutable>]
        type BearerAuthenticationSchemeOptions = {
            IssuerSigningKey: string
        }

        [<CLIMutable>]
            type OIDCAuthenticationSchemeOptions = {
            Authority: string
            ClientId: string
            ClientSecret: string
            CookieName: string
        }

        [<CLIMutable>]
        type AuthenticationOptions = {
            Bearer: BearerAuthenticationSchemeOptions
            OIDC: OIDCAuthenticationSchemeOptions
        }

        [<CLIMutable>]
        type SecurityOptions = {
            Authentication: AuthenticationOptions
            Authorization: AuthorizationOptions
        }

        let toPolicySettings policy =
            tryUnbox policy
            |> Option.map(fun x->
                x.RequiredClaims
                |> Array.map(fun claim ->
                RequireClaim {ClaimType = claim.ClaimType; AllowedValues = claim.AllowedValues}
                )
                |> Array.toList
                |> Requirements
            )

        let tryMapBearerSettings authenticationOptions = 
            tryUnbox authenticationOptions.Bearer
            |> Option.map(fun x -> OIDC {
                    Authority = x.Authority
                    ClientId = x.ClientId
                    ClientSecret = Option.ofObj x.ClientSecret
                    CookieName = Option.ofObj x.CookieName
                }
            )

        let tryMapOIDCSettings authenticationOptions = 
            tryUnbox authenticationOptions.OIDC
            |> Option.map(fun x -> Bearer {
                    IssuerSigningKey = x.IssuerSigningKey
                }
            )

        let getAuthenticationSettings authenticationOptions = 
            tryMapBearerSettings authenticationOptions
            |> Option.orElse (tryMapOIDCSettings authenticationOptions)
            |> Option.defaultWith (failwith "Unable to initialize authentication settings")

        let getAuthorizationSettings options =
            tryUnbox options
            |> Option.map(fun x-> 
                {|
                ModifyDataPolicy = x.ModifyData |> toPolicySettings |> Option.defaultValue AllowAnonymous
                GetDataPolicy = x.GetData |> toPolicySettings |> Option.defaultValue AllowAnonymous
                |}
            )
            |> Option.defaultValue {| ModifyDataPolicy = AllowAnonymous; GetDataPolicy = AllowAnonymous |}

        let getSecuritySettings options = 
            let authenticationSettings = getAuthenticationSettings options.Authentication
            let authorizationSettings = getAuthorizationSettings options.Authorization
            {
                AuthenticationScheme = authenticationSettings
                GetDataPolicy = authorizationSettings.GetDataPolicy
                ModifyDataPolicy = authorizationSettings.ModifyDataPolicy
            }

        let configureSecurity (services: IServiceCollection) =
            services.AddOptions<SecurityOptions>().BindConfiguration("Security") |> ignore

            services.AddSingleton<ContextureSecurity>(fun x-> 
                let securityOptions = x.GetService<IOptions<SecurityOptions>>()
                
                match securityOptions.Value |> tryUnbox with
                | Some options -> 
                    options |> getSecuritySettings |> Enabled
                | None -> Disabled
            )