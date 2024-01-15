namespace Contexture.Api.Infrastructure

open System.Text
open System.Net.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication.JwtBearer
open Giraffe
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Authorization

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
        Audience: string
        ClientId: string
        ClientSecret: string option
    }

    type AuthenticationScheme =
    | OIDC of OIDCSchemeSettings

    type SecuritySettings = {
        AuthenticationScheme: AuthenticationScheme
        ModifyDataPolicy: PolicyRequirements
        GetDataPolicy: PolicyRequirements
    }

    type SecurityConfiguration = 
    | Enabled of SecuritySettings
    | Disabled

    [<Literal>]
    let ModifyDataPolicyName = "ModifyData"

    [<Literal>]
    let ViewDataPolicyName = "ViewData"

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

    let configurePolicy (authorization: AuthorizationOptions) (name: string, requirements)  = 
        match requirements with
        | Requirements requirementList ->
            authorization.AddPolicy(name, fun p->
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
            authorization.AddPolicy(name, fun p ->
                p.RequireAssertion(fun _-> true)|> ignore
            )

    let configureJwtBearerScheme settings (services : IServiceCollection) = 
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(fun options ->
                options.MapInboundClaims <- false
                options.Authority <- settings.Authority
                options.Audience <- settings.Audience

                
                options.RequireHttpsMetadata <- false
                let handler = new HttpClientHandler();
                handler.ServerCertificateCustomValidationCallback <- HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                options.BackchannelHttpHandler <- handler
            )

    let configureAuthentication configuration (services : IServiceCollection) = 
        match configuration.AuthenticationScheme with
        | OIDC settings -> configureJwtBearerScheme settings services |> ignore
        services

    let configureAuthorization configuration (services : IServiceCollection) = 
        let policies = [
                ModifyDataPolicyName, configuration.ModifyDataPolicy
                ViewDataPolicyName, configuration.GetDataPolicy
            ]
        services.AddAuthorization(fun options -> policies |> List.iter (configurePolicy options))

    let addSecurity addAuthentication addAuthorization configuration (services : IServiceCollection) =
        services.AddSingleton<SecurityConfiguration>(fun _ -> configuration) |> ignore
        match configuration with
        | Enabled configuration ->
            services
            |> addAuthentication configuration
            |> addAuthorization configuration
        | Disabled -> services

    let configureSecurity configuration (services : IServiceCollection) =
        addSecurity configureAuthentication configureAuthorization configuration services

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

    let authorizeBy policyName: HttpHandler = fun next ctx ->
        authorizeByPolicyName policyName authorizationFailed next ctx

    let protectApiRoutes: HttpHandler = fun next ctx ->
        let contextureSecurity = ctx.RequestServices.GetRequiredService<SecurityConfiguration>()
        match contextureSecurity with
        | Enabled _ ->
            match ctx.Request.Method with
            | "GET" ->
                authorizeBy ViewDataPolicyName next ctx 
            | "PUT"
            | "POST"
            | "PATCH"
            | "DELETE" ->
                authorizeBy ModifyDataPolicyName next ctx
            | _ -> 
                RequestErrors.METHOD_NOT_ALLOWED ctx.Request.Method next ctx
        | _-> next ctx

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
                SecurityType = "none"
            |}
            json result next ctx    
        | Enabled securitySettings ->
            match securitySettings.AuthenticationScheme with
            | OIDC settings ->
                let result = {|
                    SecurityType = "oidc"
                    Authority = settings.Authority
                    ClientId = settings.ClientId
                    ClientSecret = settings.ClientSecret
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

        let tryMapOIDCSettings authenticationOptions = 
            let unboxed = tryUnbox authenticationOptions.OIDC
            unboxed
            |> Option.map(fun x -> OIDC {
                    Authority = x.Authority
                    Audience = x.Audience
                    ClientId = x.ClientId
                    ClientSecret = Option.ofObj x.ClientSecret
                }
            )

        let getAuthenticationSettings authenticationOptions = 
            tryMapOIDCSettings authenticationOptions
            |> Option.defaultWith (fun () -> failwith "Unable to initialize authentication settings")

        let getAuthorizationSettings options =
            tryUnbox options
            |> Option.map(fun x-> 
                {|
                    ModifyDataPolicy = x.ModifyData |> toPolicySettings |> Option.defaultValue AllowAnonymous
                    GetDataPolicy = x.GetData |> toPolicySettings |> Option.defaultValue AllowAnonymous
                |}
            )
            |> Option.defaultValue {| ModifyDataPolicy = AllowAnonymous; GetDataPolicy = AllowAnonymous |}

        let buildSecurityConfiguration (options:SecurityOptions) = 
            tryUnbox options
            |> Option.map(fun o ->
                let authenticationSettings = getAuthenticationSettings o.Authentication
                let authorizationSettings = getAuthorizationSettings o.Authorization
                let securitySettings =  {
                    AuthenticationScheme = authenticationSettings
                    GetDataPolicy = authorizationSettings.GetDataPolicy
                    ModifyDataPolicy = authorizationSettings.ModifyDataPolicy
                }

                Enabled securitySettings
            )
            |> Option.defaultValue Disabled