namespace Contexture.Api.Tests

open System
open System.Threading
open Testcontainers.MsSql
open Xunit

type MsSqlFixture() =
    static let mutable counter = ref 0
    let container =   
        let containerConfiguration =
            MsSqlBuilder()
                .WithName($"MS-SQL-Integration-Tests-{Interlocked.Increment counter}")
                .WithCleanUp(false)
                .WithAutoRemove(true)

        containerConfiguration.Build()

    member _.Container = container

    interface IAsyncLifetime with
        member this.DisposeAsync() = container.StopAsync()
        member this.InitializeAsync() = container.StartAsync()

    interface IAsyncDisposable with
        member this.DisposeAsync() = container.DisposeAsync()