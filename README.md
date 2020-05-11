# Bounded Context Canvas Wizard

The Bounded-Context-Canvas (BMC) was introduced by [Nick Tune](https://medium.com/nick-tune-tech-strategy-blog/bounded-context-canvas-v2-simplifications-and-additions-229ed35f825f) as a tool to document and visualize contexts and their connections in a system.
The canvas can be used to document business aspects, the most important behaviors and the interactions of a bounded context with other parts of the system.
Reading and understanding an existing canvas is simple, even for people who are not familiar with concepts from Domain Driven Design.
In order to create a new BMC, you need to understand a lot of concepts from DDD and filling in all the fields is not a simple task.

## Building an application

Typically a BMC is created with the help of Post-ITs on physical paper, while digital versions are usually just a mirror of the physical representation, e.g. with the help of Miro.
Meaning that the captured information is represented as free text on virtual Post-ITs and is not stored in a structured way.
This prohibits further data processing and visualization of the information.

Therefor we propose to design a small application which:

- stores information about a BMC in a structured way instead of just using free text on (virtual) Post-ITs,
- allows explicit connections between different bounded contexts,
- supports updating and versioning of the information over time,
- allows to export and visualize the information from the application,
- and helps people to input data for a BMC easier

### Features for a Prototype

MVP: "Mimicking the BMC with HTML forms"

- Have a form mirroring the BMC with free text fields
- Creating a new bounded context by submitting a new form
- Loading existing BMC into a form and updating them by (re)submitting it

Version 1: "Improving data quality"

- Use dropdowns + (conditional) free text where appropriate (strategic classification, model traits, relationships)
- Improve definition of ubiquitous language terms (allow Key-Value pairs)
- Provide Auto-complete boxes (free text search) for dependencies (search in already existing BMC names)
- Provide Auto-complete boxes (free text search) for consumed message contracts (search in produces message contracts)

Note: can be run without any external dependencies

Version 2: "Connecting Sructurizr for visualization"

- connect Structurizr as (additional?) persistence layer
- Visualize data from Structurizr as BMC

Note: needs Structurizr on-premise (via a docker-container)

Version 3: "Empower users to input data"

- Provide help text/additional information for each field
- Design a wizard for gradually/step-by-step creating a BMC
- Show proposed Bounded Context Canvases (from entered dependencies)

### Roadmap to a Prototype

- No tests needed
- No deployment needed (local/dev dependencies should be dockerized?)
- No authentication/authorization
- File based persistence is good enough
- No versioning needed

Guessed effort for the main parts:

- "Mimicking the BMC with HTML forms"
  - can be run without any external dependencies
  - effort: 3 days
- "Improving data quality"
  - can be run without any external dependencies
  - effort: 4 days
- "Connecting Sructurizr for visualization"
  - needs Structurizr on-premise version (via a docker-container)
  - effort: 4 days
- "Empower users to input data"
  - effort 4 days

## Connect the BMC with Structurizr

For visualization and exploration purposes Structurizr can be used to display information and the connections of bounded contexts.

Mapping BMC towards the Structurizr / C4 model:

DDD concepts:

- Domain -> Structurizr/Workspace
- Subdomain -> C4/SoftwareSystem
- BoundedContext -> C4/Container
- Messages/Contracts -> C4/Component

BMC concepts:

- Name -> C4/Container-name
- Description -> C4/Container-Description
- Strategic Classification -> C4/Container-TAGs / Container-Properties
- Business Decisions -> Markdown documentation
- Ubiquitous Language -> Markdown documentation
- Model Traits -> C4/Container-TAGs
- Messages Consumed & Produced ~> Component references (TODO: message level?)
- Dependencies/Relationships -> Structurizr/"uses" + tags for releationships

Visualizations:

- Domain / global context map -> Structurizr/Workspace linking + SystemLandscapeDiagram
- Context map within domain -> Structurizr/SystemLandscapeDiagram
