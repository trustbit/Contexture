namespace Contexture.Api.Tests

#nowarn "44" // ContainerBuilder<MsSqlTestcontainer>() is deprecated but does not provide a clear guidance yet

open System
open DotNet.Testcontainers.Builders
open DotNet.Testcontainers.Configurations
open DotNet.Testcontainers.Containers
open Xunit

type MsSqlFixture() =
    let container =
        let containerConfiguration =
            ContainerBuilder<MsSqlTestcontainer>()
                .WithDatabase(new MsSqlTestcontainerConfiguration(Password = "localdevpassword#123"))
                .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
                .WithName("MS-SQL-Integration-Tests")
                .WithCleanUp(false)
                .WithAutoRemove(true)

        let instance = containerConfiguration.Build()
        instance

    member _.Container = container

    interface IAsyncLifetime with
        member this.DisposeAsync() = container.StopAsync()
        member this.InitializeAsync() = container.StartAsync()

    interface IAsyncDisposable with
        member this.DisposeAsync() = container.DisposeAsync()