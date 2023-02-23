namespace Contexture.Api

type ContextureConfiguration =
    { GitHash: string
      Engine : ContextureStorageEngine }
and ContextureStorageEngine =
    | FileBased of path: string
    | SqlServerBased of connectionString:string