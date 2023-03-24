# Contexture backend

This Readme provides configuration and runtime information for the Contexture backend component. 

## Run and configure the backend

With the default settings the Contexture server will listen on port `5000` and use the file based engine with  `data/db.json` as database file.
If you want to have the server listening on any other port, set the environment variable `ASPNETCORE_URLS=http://*:8080` to the desired port.

To choose and configure a different database engine use the following configurations (the following samples use environment variables but default ASP.NET Core patterns for configurations applies!):
- `FileBased__Path=/data/db.json` to use the file based engine with the file `db.json` located in the `/data` directory or volume.
  At the moment the legacy configuration value `DatabasePath=/data/db.json` is still supported!
- `SqlBased__ConnectionString=Server=localhost;User Id=sa;Password=development(!)Password` to use the Sql Server engine with the database on `localhost` accessed by the `sa` user and it's (unsafe!) password

The default command to run Contexture from CLI

```bash
cd backend
dotnet run --project Contexture.Api
```
Note: when running Contexture you might need to exclude current launch-profiles via `--no-launch-profile`.

## Publish and running the backend manually

```bash
cd backend
dotnet run --configuration Release Contexture.Api/Contexture.Api.fsproj --output artifacts
```

Run the published version of the backend:
```
cd artifacts
ASPNETCORE_URLS=http://*:8080 FileBased__Path=data/mydb.json dotnet Contexture.Api.App.dll
```

## Caveats

- `cors` is configured to allow all origins
- no authentication and authorization is built into the application
- no UI for Namespace-template administration.
  Use the following `curl` commands to manage templates:
    - get templates
      ```bash
      curl http://localhost:5000/api/namespaces/templates
      ```

    - create a template
      ```bash
      curl -X POST \
         -H "Content-Type: application/json" \
         -d '{ "name":"barfoo", "description":"my awesome namespace", "labels": [ { "name": "first label", "description":"some description", "placeholder": "some placeholder value"}]}' \
         http://localhost:5000/api/namespaces/templates
        ```
    - add a label template to an existing namespace
      ```bash
      curl -X POST \
         -H "Content-Type: application/json" \
         -d '{ "name":"barfoo", "description":"some description", "placeholder": "some placeholder value"}' \
         http://localhost:5000/api/namespaces/templates/<template-id>
      ```
    - delete a label from a template
      ```bash
      curl -X DELETE http://localhost:5000/api/namespaces/templates/<template-id>/labels/<label-id>
      ```
    - delete a namespace
      ```bash
      curl -X DELETE http://localhost:5000/api/namespaces/templates/<template-id>
      ```

## Testing the backend

The integration tests for the SQL-Server based engine are supported by a Docker image using MS-SQL-2019.
On an ARM-based Mac (M1/M2) [this Docker-Feature](https://github.com/microsoft/mssql-docker/issues/668#issuecomment-1412206521) must be enabled for now.
