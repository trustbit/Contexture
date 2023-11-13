# Contexture - the Bounded Context Canvas Wizard

The Bounded-Context-Canvas (BCC) was introduced by [Nick Tune](https://medium.com/nick-tune-tech-strategy-blog/bounded-context-canvas-v2-simplifications-and-additions-229ed35f825f) as a tool to document and visualize contexts and their connections in a system.
The canvas can be used to certain document business aspects, the most important behaviors and interactions of a bounded context with other parts of the system.
Reading and understanding an existing canvas is simple, even for people who are not familiar with concepts from Domain Driven Design.
In order to create a new BCC, you need to understand a lot of concepts from DDD and filling in all the fields is not a simple task.

While other ways to build a [BCC exist](https://github.com/ddd-crew/bounded-context-canvas), we are building an application to support this modeling process with a dedicated tool developed with an DDD mindset.
You can read about the ideas of Contexture in the [concept](./concept.md) and you can view the current status of the application at <https://contexture.azurewebsites.net/> (be careful: don't store any sensitive data there; everything you store will be deleted upon the next deployment.)

**A short remark on using Contexture**

We think that most of the domain modelling should happen in a collaborative way, by using whitepaper, Post-ITs or online collaboration tools.
Contexture is and will not be the right tool or a replacement for these interactive modelling sessions!
But Contexture might be useful to capture, document and structure some of the insights *after* a modelling session and make them accessible and shareable with other people!

## Example

Imagine you work with a company that owns a restaurant chain which cares about giving the guest a great experience.
The restaurants are equipped with different IT systems, which support the staff to execute their tasks.
The example can be seen at <https://contexture.azurewebsites.net/> and the following screenshots give a short summary / explanation

Domains of the example
![Overview on the domains of the example](example/DomainsOverview.png)

An overview on the Bounded Contexts of the "Restaurant Experience" domain
![An overview on the Bounded Contexts of a domain](example/DomainOverview.png)

A detailed view of the "Billing" Bounded Context with the help of the Bounded-Context-Canvas-v4
![A detailed view on the Bounded-Context-Canvas, v4](example/CanvasV4Overview.png)

## Contexture backend

The Contexture server provides the API to store data and serves static assets through a Giraffe F# application.
Currently two storage engines are supported:

- a simple, file based engine that persists data in a single specified file with a JSON-based format.
   This engine supports no versioning or change dates at the moment
- a SQL-Server based engine that uses an event-sourced storage with support of database base version information and change dates.

Make sure the [.NET Framework](https://dotnet.microsoft.com/) is installed on your system!

To run the basic default configuration with the file based engine use the following command

```bash
cd backend
dotnet run --project Contexture.Api

```

More details about configuring and running the backend can be read in this [Readme](./backend/README.md)

## Contexture frontend application

The application is developed with [Vue.js](https://vuejs.org/) and connects to the backend via the API.

Make sure [Node](https://nodejs.org/en/) is installed and NPM is in your path.

### Run the frontend

```bash
cd frontend-vue
npm install
npm run dev

```

Make sure the backend part is reachable with its default url <http://localhost:3000>

## Environment Variables

- `CONTEXTURE_MAX_SUBDOMAINS_NESTING_LEVEL`: This environment variable defines the maximum level of subdomain nesting that the application will support. It should be set to a positive integer. If not set, the application defaults to a nesting level of 1.

### Setting Up Environment Variables

Create a `.env` file in the root directory of your project and specify the environment variables like so:

```.env
CONTEXTURE_MAX_SUBDOMAINS_NESTING_LEVEL=1
```


## Running with Docker

To build the Docker image use the `Makefile` via `make build-image` or execute the commands manually.

To run the `softwarepark/contexture` image use `make run-app` and browse to <http://localhost:3000>.

Your data will be stored in the `/data/db.json` file on the volume `/data`.

### Liveness & Readiness Probes

Contexture provides HTTP based [Liveness & Readiness Probes](https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/) to determine if the container is health & operational

#### Liveness

A `GET` request to `$BASE_URL/meta/health` will return `200 OK` if the application is healthy and currently processing requests.

#### Readiness

A `GET` request to `$BASE_URL/meta/readiness` will return `200 OK` if the application is ready to receive and process new requests.
It will return a `503 ServiceUnavailable` if the application cannot keep up with the load or is not yet done initializing after startup.

In addition the readiness check will return context information about internal processing to get an indication what parts of the application are lagging.

## Importing and Exporting data

The following endpoints to export and import snapshots of data exist:

```bash
# gets a snapshot of the data from a Contexture instance at $BASE_URL and saves content to $FILENAME.json
# Note: the result does not contain historic / versioned data at the moment
curl -X GET $BASE_URL/api/all -o $FILENAME.json

```

```bash
# Replaces all data in Contexture with the content of the file - this DELETES all existing data!
# Returns the content previous the the restore and stores it in $OLD_FILENAME.json
# Note: after the request the application is terminated and needs to be restarted (by Kubernetes)
curl -X PUT \
   -o $OLD_FILENAME.JSON \
   -H "Content-Type: application/json" \
   -d @$FILENAME.json \
   $BASE_URL/api/all

```

## Contributors

Thanks to all [existing and future contributors](https://github.com/Softwarepark/Contexture/graphs/contributors) and to the following individuals who have contributed with ideas, feedback or testing:

- [Nick Tune](https://github.com/NTCoding)
- Peter Rosner
