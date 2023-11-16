namespace Contexture.Api

open System
open Infrastructure.Authorization

type ContextureConfiguration =
    { GitHash: string
      Engine: ContextureStorageEngine
      AuthorizationConfiguration: AuthorizationConfiguration }

and ContextureStorageEngine =
    | FileBased of path: string
    | SqlServerBased of connectionString: string

module Options =
    [<CLIMutable>]
    type FileBased = { Path: string }

    [<CLIMutable>]
    type SqlBased = { ConnectionString: string }

    [<CLIMutable>]
    type Claim = {
      ClaimType: string
      AllowedValues : string array
    }

    [<CLIMutable>]
    type Policy = {
      Claims: Claim array
    }

    [<CLIMutable>]
    type Policies = {
      ModifyData: Policy
      GetData: Policy
    }

    [<CLIMutable>]
    type Authorization = {
      Policies: Policies
    }

    [<CLIMutable>]
    type ContextureOptions =
        { FileBased: FileBased
          SqlBased: SqlBased
          [<Obsolete("Use the new configuration option")>]
          DatabasePath: string
          GitHash: string
          Authorization: Authorization }

    let toPolicySettings policy =
      tryUnbox policy
      |> Option.map(fun x->
        x.Claims
        |> Array.map(fun claim ->
          RequireClaim {ClaimType = claim.ClaimType; AllowedValues = claim.AllowedValues}
        )
        |> Array.toList
        |> Requirements
      )

    let buildAuthorizationConfiguration (options: ContextureOptions) =
      tryUnbox options.Authorization
      |> Option.map(fun x-> 
        {
          ModifyDataPolicy = x.Policies.ModifyData |> toPolicySettings |> Option.defaultValue AllowAnonymous
          GetDataPolicy = x.Policies.GetData |> toPolicySettings |> Option.defaultValue AllowAnonymous
        }
      )
      |> Option.defaultValue { ModifyDataPolicy = AllowAnonymous; GetDataPolicy = AllowAnonymous }
        
        
    let buildConfiguration (options: ContextureOptions) =
        let authorizationConfiguration = buildAuthorizationConfiguration options
        match tryUnbox options.SqlBased with
        | Some sql when not (String.IsNullOrEmpty sql.ConnectionString) ->
            { GitHash = options.GitHash
              Engine = SqlServerBased sql.ConnectionString
              AuthorizationConfiguration = authorizationConfiguration
            }
        | _ ->
            match  tryUnbox options.FileBased with
            | Some file when not (String.IsNullOrEmpty file.Path) ->
                { GitHash = options.GitHash
                  Engine = FileBased file.Path
                  AuthorizationConfiguration = authorizationConfiguration
                }
            | _ when not (String.IsNullOrEmpty options.DatabasePath) ->
                    { GitHash = options.GitHash
                      Engine = FileBased options.DatabasePath
                      AuthorizationConfiguration = authorizationConfiguration
                    }
            | _ -> failwith "Unable to initialize a correct Contexture configuration. Configure either SqlBased_ConnectionString or FileBased_Path"
