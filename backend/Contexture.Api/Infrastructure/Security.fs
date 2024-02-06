namespace Contexture.Api.Infrastructure

open System.Text
open System.Net.Http
open System.Security.Claims
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Authentication

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

    type AuthorizationResult = 
    | Success
    | Failure
    | NoResult

    type AuthorizationHandler = HttpContext -> Task<AuthorizationResult>

    module ApiKeyAuthentication = 

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
                Task.FromResult(result)
    

    let configureJwtBearerScheme settings (authenticationBuilder : AuthenticationBuilder) = 
        authenticationBuilder
            .AddJwtBearer(JwtBearer.JwtBearerDefaults.AuthenticationScheme, fun options ->
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

        settings.ApiKeyAuthentication
        |> Option.iter(fun apiKeySettings -> configureApiKeyScheme apiKeySettings authenticationBuilder)

        settings.OIDCAuthentication
        |> Option.iter(fun oidcSettings -> configureJwtBearerScheme oidcSettings authenticationBuilder)

        services
        
    let addSecurity addAuthentication configuration (services : IServiceCollection) =
        services.AddSingleton<SecurityConfiguration>(fun _ -> configuration) |> ignore
        match configuration with
        | Enabled authenticationSchemes ->
            services
            |> addAuthentication authenticationSchemes
        | Disabled -> services

    let configureSecurity configuration (services : IServiceCollection) =
        addSecurity (configureAuthentication configureJwtBearerScheme configureApiKeyScheme) configuration services

    let useSecurity (app : IApplicationBuilder) = 
        let configuration = app.ApplicationServices.GetRequiredService<SecurityConfiguration>()
        match configuration with
        | Enabled _ ->
            app.UseAuthentication()
        | Disabled -> app

    type IApplicationBuilder with
        member x.UseSecurity() = useSecurity x

    let authorizationFailed : HttpHandler = fun next ctx -> 
        // distinguish between authentication and authorization failure reasons
        match ctx.User.Identity.IsAuthenticated with
        | true -> setStatusCode 403 next ctx
        | false -> setStatusCode 401 next ctx

    let apiKeyAuthorization (ctx:HttpContext) = task {
        let! authenticationResult =  ctx.AuthenticateAsync(ApiKeyAuthentication.AuthenticationScheme)
        if authenticationResult.None then
            return NoResult
        elif authenticationResult.Succeeded then
            return Success
        else 
            return Failure
    }

    let requireClaimFromJsonn claimRequirement (user: ClaimsPrincipal) =
        match claimRequirement.ClaimType.Split(":") |> Array.toList with
        | [] -> false
        | claimName::jsonPath ->
            let claim = user.Claims |> Seq.tryFind (fun c -> c.Type = claimName)

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

            containsAnyItem claimValues (claimRequirement.AllowedValues |> Array.toList)

    let requireClaim claimRequirement (user: ClaimsPrincipal) =
        claimRequirement.AllowedValues
        |> Array.exists (fun value -> user.HasClaim(claimRequirement.ClaimType, value))


    let anyRequirementSatisified requirements user = 
        requirements
        |> List.exists(fun requirement ->
            match requirement with
            | RequireClaim claimRequirement ->
                match claimRequirement.ClaimType.Contains(":") with
                | true -> requireClaimFromJsonn claimRequirement user
                | false -> requireClaim claimRequirement user
        )

    let authorizeByPolicy policy (ctx:HttpContext) = task{
        match policy with
        | AllowAnonymous -> return Success
        | Requirements requirements ->
            let! authenticationResult = ctx.AuthenticateAsync(JwtBearer.JwtBearerDefaults.AuthenticationScheme)
            if authenticationResult.None then
                return NoResult
            elif authenticationResult.Succeeded then
                match anyRequirementSatisified requirements authenticationResult.Ticket.Principal with
                | true ->
                    return Success
                | _ ->
                    return Failure
            else
                return Failure
    }

    let oidcAuthorization settings (ctx:HttpContext) =
        match ctx.Request.Method with
        | "GET" ->
            authorizeByPolicy settings.GetDataPolicy ctx
        | "PUT"
        | "POST"
        | "PATCH"
        | "DELETE" ->
            authorizeByPolicy settings.ModifyDataPolicy ctx
        | _ -> 
            Failure |> Task.FromResult

    let authorize (authorizationHandlers: AuthorizationHandler list) : HttpHandler = fun next ctx -> task {
        let rec authorizeRec (handlers: AuthorizationHandler list) (result: AuthorizationResult) = task {
            match result with
            | Success
            | Failure 
                -> return result
            | NoResult ->
                match handlers with
                | [] -> return result
                | handler :: rest ->
                    let! res =  handler ctx
                    return! authorizeRec rest res

        }

        let! authorizationResult = authorizeRec authorizationHandlers NoResult
        match authorizationResult with
        | Success ->
            return! next ctx
        | Failure
        | NoResult ->
            return! authorizationFailed earlyReturn ctx
    }

    let protectApiRoutes: HttpHandler = fun next ctx -> task {
        let contextureSecurity = ctx.RequestServices.GetRequiredService<SecurityConfiguration>()
        match contextureSecurity with
        | Enabled settings ->
            match settings.ApiKeyAuthentication, settings.OIDCAuthentication with
            | Some _, Some oidcSettings ->
                return! authorize [apiKeyAuthorization; oidcAuthorization oidcSettings] next ctx
            | Some _, None ->
                return! authorize [apiKeyAuthorization] next ctx
            | None, Some oidcSettings ->
                return! authorize [oidcAuthorization oidcSettings] next ctx
            | _ ->
                return! ServerErrors.INTERNAL_ERROR "Invalid security settings" next ctx

        | _-> return! next ctx
    }

    type UserPermissions = {
        Permissions: string list
    }

    [<Literal>]
    let GetDataPermission = "get"

    [<Literal>]
    let ModifyDataPermission = "modify"

    let getUserPermission (ctx:HttpContext) = task {
        let contextureSecurity = ctx.RequestServices.GetRequiredService<SecurityConfiguration>()

        let evaluatePolicyToPermission policy (permissionName: string) = task{
            let! authorizationResult = authorizeByPolicy policy ctx
            return 
                match authorizationResult with
                | Success -> Some permissionName
                | _ -> None
        }
        
        match contextureSecurity with
        | Disabled ->
            return { Permissions = [ModifyDataPermission; GetDataPermission] }
        | Enabled settings ->
            match settings.OIDCAuthentication with
            | Some oidcSettings ->
                let! modifyDataPermission = evaluatePolicyToPermission oidcSettings.ModifyDataPolicy ModifyDataPermission
                let! getDataPermission = evaluatePolicyToPermission oidcSettings.GetDataPolicy GetDataPermission
                let permissions = [modifyDataPermission; getDataPermission] |> List.choose id
                return { Permissions = permissions}
            | _ -> 
                return { Permissions = []}
    }

    let userPermissions: HttpHandler = fun next ctx -> task {
        let! user = getUserPermission ctx                
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

        let tryMapOIDCSettings options : OIDCAuthenticationSettings option = 
            let unboxed = tryUnbox options.Authentication.OIDC
            unboxed
            |> Option.map(fun x -> 
                let authorizationSettings = getAuthorizationSettings options.Authorization
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