namespace Contexture.Api.Infrastructure

open System.Text
open System.Net.Http
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Authentication

module ApiKeyAuthentication = 
    open System.Security.Claims

    [<Literal>]
    let AuthenticationScheme = "APIKey";
    [<Literal>]
    let HeaderName = "x-api-key";

    type ApiKeyAuthenticationOptions() = 
        inherit AuthenticationSchemeOptions()
        member val ApiKey = "" with get, set

    type ApiKeyAuthenticationHandler(options, logger, encoder, clock) = 
        inherit AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder, clock)
        let authenticate (request: HttpRequest) (options: ApiKeyAuthenticationOptions) = 
            match request.Headers.TryGetValue(HeaderName) with
            | true, apiKeyHeader -> 
                match options.ApiKey = apiKeyHeader.ToString() with
                | true ->
                    let claims = [Claim("api-key", apiKeyHeader.ToString())]
                    let identity = ClaimsIdentity(claims, AuthenticationScheme)
                    let principal = ClaimsPrincipal(identity)
                    let authenticationTicket = AuthenticationTicket(principal, AuthenticationScheme)
                    AuthenticateResult.Success(authenticationTicket)
                | _ ->
                    AuthenticateResult.Fail("Invalid API key")
            | _ ->
                AuthenticateResult.NoResult()
        
        override x.HandleAuthenticateAsync() = 
            let result = authenticate x.Request x.Options
            System.Threading.Tasks.Task.FromResult(result)

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
    
    type OIDCAuthenticationSettings = {
        Authority: string
        Audience: string
        ClientId: string
        ClientSecret: string option
        ModifyDataPolicy: PolicyRequirements
        GetDataPolicy: PolicyRequirements
    }
    
    type ApiKeyAuthenticationSettings = {
        ApiKey: string
    }

    type SecuritySettings = {
        OIDCAuthentication: OIDCAuthenticationSettings option
        ApiKeyAuthentication: ApiKeyAuthenticationSettings option
    }

    type SecurityConfiguration = 
    | Enabled of SecuritySettings
    | Disabled

    [<Literal>]
    let ModifyDataPolicyName = "ModifyData"
    [<Literal>]
    let ViewDataPolicyName = "ViewData"
    [<Literal>]
    let ApiKeyPolicyName = "ApiKey"
    

    let requireClaimFromJson (path: string) values (ctx: AuthorizationHandlerContext) = task {
        match path.Split(":") |> Array.toList with
        | [] -> 
            return false
        | claimName::jsonPath ->
            let claim = ctx.User.Claims |> Seq.tryFind (fun c -> c.Type = claimName)

            let tryFromJson (value:string) = 
                try
                    Json.JsonDocument.Parse(value).RootElement |> Some
                with
                | _ -> None

            let jsonClaim = claim |> Option.map(fun x-> x.Value) |> Option.bind tryFromJson

            let tryGetJsonElement (jsonElementOption:Json.JsonElement option) (propertyName:string) = 
                jsonElementOption |> Option.bind(fun jsonElement ->
                    match jsonElement.TryGetProperty(propertyName) with
                    | true, x -> Some x
                    | _ -> None
                )

            let jsonElement = List.fold tryGetJsonElement jsonClaim jsonPath

            let getValuesFromElement (element: Json.JsonElement) = 
                match element.ValueKind with
                | Json.JsonValueKind.Array ->
                    element.EnumerateArray()
                    |> Seq.map(fun x -> x.GetString())
                    |> Seq.toList
                | Json.JsonValueKind.String ->
                    element.GetString() |> List.singleton
                | _ -> []

            let claimValues = jsonElement |> Option.map getValuesFromElement |> Option.defaultValue []

            let containsAnyItem a b = 
                List.exists (fun item -> List.contains item b) a

            return containsAnyItem claimValues (values |> Array.toList)
    }

    let configurePolicy (builder: AuthorizationBuilder) (name: string, requirements)  = 
        match requirements with
        | Requirements requirementList ->
            builder.AddPolicy(name, fun p->
                p.RequireAuthenticatedUser() |> ignore

                requirementList
                |> List.iter (fun requirement ->
                    match requirement with
                    | RequireClaim claimRequirement ->
                        match claimRequirement.ClaimType.Contains(":") with
                        | true -> p.RequireAssertion((requireClaimFromJson claimRequirement.ClaimType claimRequirement.AllowedValues)) |> ignore
                        | false -> p.RequireClaim(claimRequirement.ClaimType, claimRequirement.AllowedValues) |> ignore
                )
            )
        | AllowAnonymous ->
            builder.AddPolicy(name, fun p ->
                p.RequireAssertion(fun _-> true)|> ignore
            )

    let configureJwtBearerScheme settings (authenticationBuilder : AuthenticationBuilder) = 
        authenticationBuilder
            .AddJwtBearer(fun options ->
                options.MapInboundClaims <- false
                options.Authority <- settings.Authority
                options.Audience <- settings.Audience
            )
            |> ignore

    let configureApiKeyScheme apiKeySettings (authenticationBuilder : AuthenticationBuilder) = 
        authenticationBuilder
            .AddScheme<ApiKeyAuthentication.ApiKeyAuthenticationOptions, ApiKeyAuthentication.ApiKeyAuthenticationHandler>(ApiKeyAuthentication.AuthenticationScheme, 
                fun options ->
                options.ApiKey <- apiKeySettings.ApiKey
            )
            |> ignore

    let configureAuthentication configureJwtBearerScheme configureApiKeyScheme settings (services : IServiceCollection) = 
        let authenticationBuilder = services.AddAuthentication()
  
        settings.OIDCAuthentication
        |> Option.iter(fun oidcSettings -> configureJwtBearerScheme oidcSettings authenticationBuilder)

        settings.ApiKeyAuthentication
        |> Option.iter(fun apiKeySettings -> configureApiKeyScheme apiKeySettings authenticationBuilder)

        services

    let configureAuthorization settings (services : IServiceCollection) = 
        let authorizationBuilder = services.AddAuthorizationBuilder()

        settings.OIDCAuthentication
        |> Option.iter(fun configuration ->
            let policies = [
                ModifyDataPolicyName, configuration.ModifyDataPolicy
                ViewDataPolicyName, configuration.GetDataPolicy
            ]

            policies |> List.iter (configurePolicy authorizationBuilder >> ignore)
        )

        settings.ApiKeyAuthentication
        |> Option.iter(fun configuration ->
            authorizationBuilder.AddPolicy(ApiKeyPolicyName, fun p -> 
                p.RequireAuthenticatedUser()
                |> ignore
            )
            |> ignore
        )

        services
        
    let addSecurity addAuthentication addAuthorization configuration (services : IServiceCollection) =
        services.AddSingleton<SecurityConfiguration>(fun _ -> configuration) |> ignore
        match configuration with
        | Enabled authenticationSchemes ->
            services
            |> addAuthentication authenticationSchemes
            |> addAuthorization authenticationSchemes
        | Disabled -> services

    let configureSecurity configuration (services : IServiceCollection) =
        addSecurity (configureAuthentication configureJwtBearerScheme configureApiKeyScheme) configureAuthorization configuration services

    let useSecurity (app : IApplicationBuilder) = 
        let configuration = app.ApplicationServices.GetRequiredService<SecurityConfiguration>()
        match configuration with
        | Enabled _ ->
            app.UseAuthentication().UseAuthorization()
        | Disabled -> app

    type IApplicationBuilder with
        member x.UseSecurity() = useSecurity x

    let authorizationFailed : HttpHandler = fun next ctx -> 
        // distinguish between authentication and authorization failure reasons
        match ctx.User.Identity.IsAuthenticated with
        | true -> setStatusCode 403 next ctx
        | false -> setStatusCode 401 next ctx

    let apiKeyAuthorization authorizationFailed : HttpHandler = fun next ctx ->
        authorizeByPolicyName ApiKeyPolicyName authorizationFailed next ctx

    let oidcAuthorization authorizationFailed: HttpHandler = fun next ctx ->
        match ctx.Request.Method with
        | "GET" ->
            authorizeByPolicyName ViewDataPolicyName authorizationFailed next ctx
        | "PUT"
        | "POST"
        | "PATCH"
        | "DELETE" ->
            authorizeByPolicyName ModifyDataPolicyName authorizationFailed next ctx
        | _ -> 
            RequestErrors.METHOD_NOT_ALLOWED ctx.Request.Method next ctx

    let protectApiRoutes: HttpHandler = fun next ctx -> task {
        let contextureSecurity = ctx.RequestServices.GetRequiredService<SecurityConfiguration>()
        match contextureSecurity with
        | Enabled settings ->
            match settings.ApiKeyAuthentication, settings.OIDCAuthentication with
            | Some _, Some _ ->
                return! apiKeyAuthorization (oidcAuthorization authorizationFailed) next ctx
            | Some _, None ->
                return! apiKeyAuthorization authorizationFailed next ctx
            | None, Some _ ->
                return! oidcAuthorization authorizationFailed next ctx
            | _ ->
                return! ServerErrors.INTERNAL_ERROR "Invalid security settings" next ctx
        | _-> return! next ctx
    }

    type UserInfo = {
        Authenticated: bool
        Permissions: string list
    }


    let policyNameToPermission policyName = 
        match policyName with
        | ViewDataPolicyName -> "view"
        | ModifyDataPolicyName -> "modify"
        | _-> failwith $"unknown policy name {policyName}"

    let getUserInfo (ctx:HttpContext) = task {
        let authorizationService = ctx.RequestServices.GetService<IAuthorizationService>()
        let contextureSecurity = ctx.RequestServices.GetRequiredService<SecurityConfiguration>()
        let evaluatePolicyToPermission (policyName: string) = task{
            let! authorizationResult = authorizationService.AuthorizeAsync(ctx.User, policyName)
            return 
                match authorizationResult.Succeeded with
                | true -> policyNameToPermission policyName |> Some
                | _ -> None
        }
        
        match contextureSecurity with
        | Disabled ->
            return { Authenticated = true; Permissions = [ModifyDataPolicyName; ViewDataPolicyName] |> List.map policyNameToPermission }
        | Enabled _ ->
            match ctx.User.Identity.IsAuthenticated with
            | true ->
                let! modify = evaluatePolicyToPermission ModifyDataPolicyName
                let! view = evaluatePolicyToPermission ViewDataPolicyName
                let permissions = [modify; view] |> List.choose id
                return { Authenticated = true; Permissions = permissions}
            | _ -> 
                return { Authenticated = false; Permissions = []}
    }

    let userInfo: HttpHandler = fun next ctx -> task {
        let! user = getUserInfo ctx                
        return! json user next ctx
    }

    let securityConfiguration: HttpHandler = fun next ctx -> 
        let contextureSecurity = ctx.RequestServices.GetRequiredService<SecurityConfiguration>()
        match contextureSecurity with
        | Disabled ->
            let result = {|
                SecurityType = "disabled"
            |}
            json result next ctx
        | Enabled securitySettings ->
            match securitySettings.OIDCAuthentication with
            | Some settings ->
                let result = {|
                    SecurityType = "oidc"
                    Authority = settings.Authority
                    ClientId = settings.ClientId
                    ClientSecret = settings.ClientSecret
                |}
                json result next ctx
            | None ->
                let result = {|
                    SecurityType = "disabled"
                |}
                json result next ctx

    module Options = 
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
            type OIDCAuthenticationSchemeOptions = {
            Authority: string
            Audience: string
            ClientId: string
            ClientSecret: string
        }

        [<CLIMutable>]
        type AuthenticationOptions = {
            OIDC: OIDCAuthenticationSchemeOptions
            ApiKey: string
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

        let getAuthorizationSettings options =
            tryUnbox options
            |> Option.map(fun x-> 
                {|
                    ModifyDataPolicy = x.ModifyData |> toPolicySettings |> Option.defaultValue AllowAnonymous
                    GetDataPolicy = x.GetData |> toPolicySettings |> Option.defaultValue AllowAnonymous
                |}
            )
            |> Option.defaultValue {| ModifyDataPolicy = AllowAnonymous; GetDataPolicy = AllowAnonymous |}

        let tryMapOIDCSettings securityOptions : OIDCAuthenticationSettings option = 
            let unboxed = tryUnbox securityOptions.Authentication.OIDC
            unboxed
            |> Option.map(fun x -> 
                let authorizationSettings = getAuthorizationSettings securityOptions.Authorization

                {
                    Authority = x.Authority
                    Audience = x.Audience
                    ClientId = x.ClientId
                    ClientSecret = Option.ofObj x.ClientSecret
                    GetDataPolicy = authorizationSettings.GetDataPolicy
                    ModifyDataPolicy = authorizationSettings.ModifyDataPolicy
                }
            )

        let tryMapApiKeySettings securityOptions = 
            Option.ofObj securityOptions.Authentication.ApiKey
            |> Option.map(fun key -> {ApiKey = key})

        let buildSecurityConfiguration (options:SecurityOptions) = 
            tryUnbox options
            |> Option.map(fun o ->
                match tryMapOIDCSettings o, tryMapApiKeySettings o with
                | None, None -> failwith "Could not parse securty configuration"
                | oidc, apiKey ->
                    Enabled {
                        OIDCAuthentication = oidc
                        ApiKeyAuthentication = apiKey
                    }
            )
            |> Option.defaultValue Disabled