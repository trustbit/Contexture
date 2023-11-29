namespace Contexture.Api

open System

type ContextureConfiguration =
    { GitHash: string
      Engine: ContextureStorageEngine }

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
          GitHash: string }        
        
    let buildConfiguration (options: ContextureOptions) =
        match tryUnbox options.SqlBased with
        | Some sql when not (String.IsNullOrEmpty sql.ConnectionString) ->
            { GitHash = options.GitHash
              Engine = SqlServerBased sql.ConnectionString
            }
        | _ ->
            match tryUnbox options.FileBased with
            | Some file when not (String.IsNullOrEmpty file.Path) ->
                { GitHash = options.GitHash
                  Engine = FileBased file.Path
                }
            | _ when not (String.IsNullOrEmpty options.DatabasePath) ->
                    { GitHash = options.GitHash
                      Engine = FileBased options.DatabasePath
                    }
            | _ -> failwith "Unable to initialize a correct Contexture configuration. Configure either SqlBased_ConnectionString or FileBased_Path"
