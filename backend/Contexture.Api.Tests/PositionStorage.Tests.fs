namespace Contexture.Api.Tests.PositionStorage

open System.Data.SqlClient
open System.Threading.Tasks
open Contexture.Api.Infrastructure
open Contexture.Api.Infrastructure.Subscriptions.PositionStorage
open Contexture.Api.Tests
open FsToolkit.ErrorHandling
open Xunit

[<AbstractClass>]
type PositionStorageBehavior() =
    abstract member EmptyStorage: unit -> IStorePositions
    abstract member StorageWith : (string * Position) list -> Task<IStorePositions>
    
    [<Fact>]
    member this.CanReadNoneFromEmptyStorage() =
        task {
            let storage = this.EmptyStorage()

            let! result = storage.LastPosition "name"

            Assert.Equal(None, result)
        }

    [<Fact>]
    member this.CanReadExistingFromStorage()=
        task {
            let key = "key"
            let position = Position.from 1234
            let! storage = this.StorageWith [key,position]
            
            let! result = storage.LastPosition key
            
            Assert.Equal(Some position, result)
        }
        
    [<Fact>]
    member this.CanWriteIntoEmptyStorage()=
        task {
            let key = "key"
            let position = Position.from 1234
            let storage = this.EmptyStorage()
            
            do! storage.SavePosition key position
            
            let! result = storage.LastPosition key
            
            Assert.Equal(Some position, result)
        }
        
    [<Fact>]
    member this.CanUpdateExistingEntry()=
        task {
            let key = "key"
            let existingPosition = Position.from 1234
            let! storage = this.StorageWith [key,existingPosition]
            
            let newPosition = Position.from 4321
            do! storage.SavePosition key newPosition
            
            let! result = storage.LastPosition key
            
            Assert.Equal(Some newPosition, result)
        }
        

type InMemoryPositionStorage() =
    inherit PositionStorageBehavior()

    override this.EmptyStorage() = InMemory.PositionStorage.Empty
    override this.StorageWith items =
        items
        |> Map.ofList
        |> InMemory.PositionStorage
        :> IStorePositions
        |> Task.FromResult


type SqlServerPositionStorage(msSql: MsSqlFixture) =
    inherit PositionStorageBehavior()

    override this.EmptyStorage() =
        SqlServer.PositionStorage(msSql.Container.ConnectionString)
        
    override this.StorageWith items = task {
        use client = new SqlConnection(msSql.Container.ConnectionString)
        do! client.OpenAsync()
        
        // using string concatenation over parameters here is OK because it's just (unit) test code
        let values =
            items
            |> List.map (fun (name,position) -> $"('{name}', {Position.value position})")
            |> String.concat ","
        let command = client.CreateCommand()
        command.CommandText <-
            $"INSERT INTO Subscriptions (name, last_position) VALUES {values}" 
        let! result = command.ExecuteNonQueryAsync()
        Assert.Equal(items |> List.length, result)
        
        return SqlServer.PositionStorage(msSql.Container.ConnectionString) :> IStorePositions
        }

    interface IAsyncLifetime with
        member this.DisposeAsync() =
            SqlServer.PositionStorage.RemoveSchema(msSql.Container.ConnectionString)

        member this.InitializeAsync() =
            SqlServer.PositionStorage.CreateSchema(msSql.Container.ConnectionString)

    interface IClassFixture<MsSqlFixture> with

