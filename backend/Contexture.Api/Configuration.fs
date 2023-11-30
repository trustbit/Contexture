namespace Contexture.Api

open System
open Contexture.Api.Infrastructure

type ContextureConfiguration =
    { GitHash: string
      Engine: ContextureStorageEngine 
      Security: Security.SecurityConfiguration}

and ContextureStorageEngine =
    | FileBased of path: string
    | SqlServerBased of connectionString: string

module Options =
    [<CLIMutable>]
    type FileBased = { Path: string }

    [<CLIMutable>]
    type SqlBased = { ConnectionString: string }

    [<CLIMutable>]
    type ContextureOptions =
        { FileBased: FileBased
          SqlBased: SqlBased
          [<Obsolete("Use the new configuration option")>]
          DatabasePath: string
          GitHash: string 
          Security: Security.Options.SecurityOptions }        
        
    let buildConfiguration (options: ContextureOptions) =
        let securityConfiguration = Security.Options.buildSecurityConfiguration options.Security
        match tryUnbox options.SqlBased with
        | Some sql when not (String.IsNullOrEmpty sql.ConnectionString) ->
            { GitHash = options.GitHash
              Engine = SqlServerBased sql.ConnectionString
              Security = securityConfiguration
            }
        | _ ->
            match tryUnbox options.FileBased with
            | Some file when not (String.IsNullOrEmpty file.Path) ->
                { GitHash = options.GitHash
                  Engine = FileBased file.Path
                  Security = securityConfiguration
                }
            | _ when not (String.IsNullOrEmpty options.DatabasePath) ->
                    { GitHash = options.GitHash
                      Engine = FileBased options.DatabasePath
                      Security = securityConfiguration
                    }
            | _ -> failwith "Unable to initialize a correct Contexture configuration. Configure either SqlBased_ConnectionString or FileBased_Path"
