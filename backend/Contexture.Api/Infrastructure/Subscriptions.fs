namespace Contexture.Api.Infrastructure.Subscriptions

open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Contexture.Api.Infrastructure

type SubscriptionDefinition =
    | FromAll of SubscriptionStartingPosition
    | FromKind of StreamKind * SubscriptionStartingPosition
    | FromStream of StreamIdentifier * Version option

and SubscriptionStartingPosition =
    | Start
    | From of Position
    | End

type Subscription =
    inherit System.IAsyncDisposable
    abstract Name: string
    abstract Status: SubscriptionStatus

and SubscriptionStatus =
    | NotRunning
    | Processing of current: Position
    | CaughtUp of at: Position
    | Failed of exn * at: Position option
    | Stopped of at: Position


type SubscriptionHandler = Position -> EventEnvelope list -> Async<unit>

type SubscriptionHandler<'E> = Position -> EventEnvelope<'E> list -> Async<unit>

module SubscriptionHandler =
    let trackPosition tracker (subscription: SubscriptionHandler<_>) : SubscriptionHandler<_> =
        fun position events ->
            async {
                do! subscription position events
                do! tracker position
            }

type SubscriptionStatistics =
    { CaughtUp: (Position * string) list
      Processing: (Position * string) list
      Failed: (exn * Position option * string) list
      NotRunning: string list
      Stopped: (Position * string) list }

    static member Initial =
        { CaughtUp = []
          Processing = []
          Failed = []
          NotRunning = []
          Stopped = [] }

module Runtime =
    let calculateStatistics (subscriptions: Subscription List) =
        subscriptions
        |> List.fold
            (fun state item ->
                match item.Status with
                | CaughtUp p -> { state with CaughtUp = (p, item.Name) :: state.CaughtUp }
                | Failed(ex, pos) -> { state with Failed = (ex, pos, item.Name) :: state.Failed }
                | NotRunning -> { state with NotRunning = item.Name :: state.NotRunning }
                | Processing p -> { state with Processing = (p, item.Name) :: state.Processing }
                | Stopped p -> { state with Stopped = (p, item.Name) :: state.Stopped })
            SubscriptionStatistics.Initial

    let didAllSubscriptionsCatchup caughtUpSubscriptions subscriptions =
        let positions = caughtUpSubscriptions |> List.map fst |> List.distinct

        (positions |> List.length = 1)
        && (List.length caughtUpSubscriptions) = (List.length subscriptions)

    let waitUntilCaughtUp (subscriptions: Subscription List) =
        task {
            let initialStatus = calculateStatistics subscriptions
            let mutable lastStatus = initialStatus
            let mutable counter = 0

            while not (didAllSubscriptionsCatchup lastStatus.CaughtUp subscriptions) do
                do! Task.Delay(100)
                let calculatedStatus = calculateStatistics subscriptions
                // if nothing changed since the last iteration we increase the counter to fail eventually
                if calculatedStatus = lastStatus then
                    counter <- counter + 1
                lastStatus <- calculatedStatus
                if counter > 100 then
                    failwithf "No result after %i iterations. Last Status %A" counter lastStatus
        }

module PositionStorage =
    type IStorePositions =
        abstract LastPosition: string -> Async<Position option>
        abstract SavePosition: string -> Position -> Async<unit>

    module InMemory =
        type PositionStorage(initial: Map<string, Position>) =
            let mutable positions: Map<string, Position> = initial

            static member Empty = PositionStorage(Map.empty)

            interface IStorePositions with
                member _.LastPosition name = Async.retn (Map.tryFind name positions)

                member _.SavePosition name position =
                    positions <- positions |> Map.add name position
                    Async.retn ()

    module SqlServer =
        open System.Data.SqlClient

        [<Literal>]
        let private schemaScript =
            """
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Subscriptions' and xtype='U')
    create table Subscriptions
    (
        name          nvarchar(200) not null
            constraint Subscriptions_pk
                primary key,
        last_position bigint        not null
    )"""

        let private openConnection connectionString =
            task {
                let client = new SqlConnection(connectionString)
                do! client.OpenAsync()
                return client
            }

        let private executeNonQuery query parameters (client: SqlConnection) =
            task {
                let command = client.CreateCommand()
                command.CommandText <- query

                parameters
                |> List.iter (fun (name, value: obj) -> command.Parameters.AddWithValue(name, value) |> ignore)

                return! command.ExecuteNonQueryAsync()
            }

        let private executeQuery query parameters (client: SqlConnection) =
            task {
                let command = client.CreateCommand()
                command.CommandText <- query

                parameters
                |> List.iter (fun (name, value: obj) -> command.Parameters.AddWithValue(name, value) |> ignore)

                return! command.ExecuteReaderAsync()
            }

        type PositionStorage(connectionString: string) =
            let getLastPosition (name: string) =
                task {
                    use! client = openConnection connectionString
                    let query = "SELECT last_position FROM Subscriptions WHERE name=@name"
                    let! result = client |> executeQuery query [ "name", name ]

                    if result.HasRows && result.Read() then
                        let position = Position.create (result.GetInt64 0)
                        return position
                    else
                        return None
                }

            let savePosition (name: string) position =
                task {
                    use! client = openConnection connectionString

                    let query =
                        "MERGE Subscriptions AS [Target]
USING (SELECT name = @name, position=@position) AS [Source] 
    ON [Target].name = [Source].name --- specifies the condition
WHEN MATCHED THEN
  UPDATE SET [Target].last_position=[Source].position --UPDATE STATEMENT
WHEN NOT MATCHED THEN
  INSERT (name, last_position) VALUES ([Source].name,[Source].position); --INSERT STATEMENT"

                    let! _ =
                        client
                        |> executeNonQuery query [ "name", name; "position", Position.value position ]

                    return ()
                }

            static member CreateSchema(connectionString) =
                task {
                    use! client = openConnection connectionString
                    let! _ = client |> executeNonQuery schemaScript []
                    return ()
                }

            static member RemoveSchema(connectionString) =
                task {
                    use! client = openConnection connectionString
                    let! _ = client |> executeNonQuery "DROP TABLE Subscriptions" []
                    return ()
                }

            interface IStorePositions with
                member _.LastPosition name = Async.AwaitTask(getLastPosition name)

                member _.SavePosition name position =
                    Async.AwaitTask(savePosition name position)
