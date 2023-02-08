module Contexture.Api.Tests.SqlDockerSupport

open System
open System.Data
open System.Data.SqlClient
open Xunit

module MsSqlContainer =
    open Ductus.FluentDocker.Builders
    open Ductus.FluentDocker.Services

    type MsSqlConfiguration =
        { ContainerName: string
          /// Requires: A strong system administrator (SA) password: At least 8 characters including uppercase, lowercase letters, base-10 digits and/or non-alphanumeric symbols
          Password: string
          Port: int }
    module MsSqlConfiguration =
        module Defaults =
            [<Literal>]
            let image = "mcr.microsoft.com/mssql/server:2019-latest"
            [<Literal>]
            let port = 1433
            let password = "Integration-Test-123"
            [<Literal>]
            let host = "localhost"
            [<Literal>]
            let user = "SA"
            [<Literal>]
            let integrationTestingPort = 1434
        let Default =
            { ContainerName = "mssql-contexture-test"
              Password = Defaults.password
              Port = Defaults.integrationTestingPort }
        let buildConnectionString (config: MsSqlConfiguration) =
            let builder = SqlConnectionStringBuilder()
            builder.DataSource <- $"{Defaults.host},{config.Port}"
            builder.UserID <- Defaults.user
            builder.Password <- config.Password
            builder.ToString()

    type MsSqlConnection(connectionString: string, container: IDisposable) =
        member _.ConnectionString = connectionString
        interface IDisposable with
            member _.Dispose() = container.Dispose()

    let private wait (connectionString: string) (_: IContainerService) (_: int) =
        use conn =
            new System.Data.SqlClient.SqlConnection(connectionString)

        try
            conn.Open()
            if conn.State <>ConnectionState.Open then
                1000
            else
                0
        with ex ->
            1000

    
    let initializeDocker config =
        let connectionString = MsSqlConfiguration.buildConnectionString config

        let builder =
            Builder()
                .UseContainer()
                .UseImage(MsSqlConfiguration.Defaults.image)
                .WithName(config.ContainerName)
                .RemoveVolumesOnDispose()
                .ReuseIfExists()
                .ExposePort(config.Port, MsSqlConfiguration.Defaults.port)
                .WithEnvironment(
                    "ACCEPT_EULA=Y",
                    $"MSSQL_SA_PASSWORD={config.Password}"
                    )
                .Wait(config.ContainerName, (fun x y -> wait connectionString x y))
                .Builder()

        let containerService = builder.Build()
        let service = containerService.Start()
        new MsSqlConnection(connectionString, service)

type MsSqlFixture() =
    // kind of dirty: Instances are managed by XUnit,
    // but to easily access the connection string from everywhere we expose it again as static member
    let msSqlConnection =
        MsSqlContainer.MsSqlConfiguration.Default
        |> MsSqlContainer.initializeDocker

    static member ConnectionString = MsSqlContainer.MsSqlConfiguration.buildConnectionString MsSqlContainer.MsSqlConfiguration.Default

    interface IDisposable with
        member this.Dispose() = (msSqlConnection:>IDisposable).Dispose()

[<CollectionDefinition("MsSqlCollection")>]
type MsSqlCollection() =
    interface ICollectionFixture<MsSqlFixture>
