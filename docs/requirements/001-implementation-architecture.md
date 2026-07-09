# ArchitectLuna

ArchitectLuna is a CLI code generator for production-grade .NET APIs — think **Intent Architect Lite** for backend systems.

It generates a clean, opinionated backend foundation with the things we build in almost every serious .NET API:

* Clean Architecture structure
* Vertical feature slices
* Result pattern
* Base entity model
* Exception handling middleware
* Serilog logging setup
* User context service
* Validation pipeline
* Mapping layer for DTOs
* Persistence setup
* Health checks
* Endpoint registration
* Dependency injection extensions
* Environment-based configuration
* Test project structure

A small YAML **Intent Model** at `.architect/model.yaml` describes the solution's features and entities.
`architect-luna generate` turns that model into a working backend API with commands, handlers, validators, endpoints, mappings, and persistence wiring.

ArchitectLuna is not an AI code generator.

It does not try to write business logic for you.

It generates the production foundation and repetitive structure so developers can focus on implementing business rules.

---

## Core Philosophy

ArchitectLuna follows a simple rule:

```text
Generate the boring structure.
Let developers write the business logic.
```

Every generated backend should be:

* Clean
* Predictable
* Production-ready
* Easy to understand
* Easy to extend
* Safe to regenerate
* Consistent across projects

The generated code should not feel magical.

A developer should be able to open the solution and immediately understand where things belong.

---

## Production-Ready by Default

ArchitectLuna should scaffold everything that belongs in almost every production .NET backend.

The initial project should not be an empty API with a few endpoints.

It should already include the foundation required for a real system.

### Generated production foundation

A new project includes:

```text
Clean Architecture projects
Application abstractions
Domain base types
Result pattern
Validation pipeline
Exception handling middleware
Serilog logging
User context service
Date/time service abstraction
Current user abstraction
Mapping layer
DTO structure
Persistence setup
Health checks
Swagger/OpenAPI
Environment configuration
Dependency injection extensions
Docker/local development setup
Test projects
```

The goal is that after scaffolding, the team only needs to add:

```text
Entities
Feature slices
Business logic
Integration logic
Project-specific rules
```

---

## Clean Architecture by Default

Clean Architecture is the default generated structure.

ArchitectLuna should generate a solution shaped like this:

```text
BillingService/

  .architect/
    model.yaml
    architect.json

  src/
    BillingService.Api/
    BillingService.Application/
    BillingService.Domain/
    BillingService.Infrastructure/
    BillingService.Contracts/

  tests/
    BillingService.Application.Tests/
    BillingService.Api.Tests/
    BillingService.Infrastructure.Tests/

  docs/
    architecture.md
    local-development.md

  BillingService.sln
  README.md
  docker-compose.yml
  .editorconfig
  .gitignore
  Directory.Build.props
  Directory.Packages.props
```

---

## Project Responsibilities

### Api

The API project owns HTTP concerns only.

It contains:

* Program startup
* Middleware pipeline
* Endpoint or controller registration
* Swagger/OpenAPI setup
* Health checks
* HTTP request/response mapping
* Authentication and authorization hooks
* API-specific configuration

The API project should not contain business logic.

---

### Application

The Application project owns use cases.

It contains:

* Feature slices
* Requests
* Results
* Handlers
* Validators
* Mapping contracts
* Application interfaces
* Result pattern
* Pipeline behaviours
* Application exceptions

The Application layer should not know whether the project uses MediatR or Wolverine.

The dispatcher is an implementation detail.

---

### Domain

The Domain project owns the business model.

It contains:

* Entities
* BaseEntity
* Value objects
* Domain events
* Domain exceptions
* Enums
* Business rules

The Domain project should be as dependency-free as possible.

It should not reference:

* API
* Infrastructure
* EF Core
* Marten
* MediatR
* Wolverine

---

### Infrastructure

The Infrastructure project owns technical implementations.

It contains:

* EF Core setup
* Marten setup
* Database configuration
* External services
* User context implementation
* Date/time implementation
* Storage implementations
* Dispatcher adapters
* Logging integrations where needed
* Persistence-specific configuration

Infrastructure depends on Application and Domain.

---

### Contracts

The Contracts project owns shared DTOs and public API contracts.

It contains:

* Request DTOs
* Response DTOs
* Shared API models
* Versionable external contracts

The Contracts project should not contain business logic.

---

## The Core Idea: Entity Outwards

An **entity** is the source of truth for standard CRUD generation.

Everything downstream is generated outward from the entity:

```text
entity
  ├── Create command
  ├── Update command
  ├── Delete command
  ├── GetById query
  ├── GetAll query
  ├── handlers
  ├── validators
  ├── DTOs
  ├── mappings
  ├── endpoints/controllers
  └── persistence wiring
```

One `add entity` call gives ArchitectLuna enough information to generate a complete CRUD feature set.

You can still add bespoke commands and queries when the operation does not fit standard CRUD.

---

## Generation Order Matters

ArchitectLuna enforces a clean generation workflow.

You cannot safely generate entity-backed feature slices before the entity exists.

The intended order is:

```text
1. Scaffold the solution
2. Add a feature group
3. Add entities
4. Generate CRUD slices from entities
5. Add bespoke commands or queries if needed
6. Implement business logic
```

---

## Required Workflow

### 1. Create the API solution

```bash
architect-luna new api BillingService --adapter wolverine --persistence efcore-postgres
```

This creates the production-ready backend foundation.

The generated solution should build immediately.

---

### 2. Add a feature group

```bash
architect-luna add feature Invoices
```

Feature groups organize related entities and operations.

Examples:

```text
Invoices
Customers
Payments
Users
Orders
```

---

### 3. Add an entity

```bash
architect-luna add entity Invoices Invoice \
  --field CustomerId:Guid \
  --field AmountCents:long \
  --field Currency:string \
  --rule "AmountCents:GreaterThan(0)" \
  --rule "Currency:MaximumLength(3)"
```

The entity becomes the source of truth for generated CRUD.

---

### 4. Generate the code

```bash
architect-luna generate
```

ArchitectLuna renders the model into real C# files.

If persistence is configured, generated handlers include real persistence calls.

---

### 5. Add bespoke operations only when needed

```bash
architect-luna add command Invoices VoidInvoice --field Id:Guid --kind update
architect-luna add query Invoices SearchInvoices --param CustomerId:Guid --param Status:string
```

Bespoke commands and queries are for use cases that are not covered by standard CRUD.

---

## Ordering Rules

### Rule 1: A solution must exist first

You cannot add features, entities, commands, or queries outside an ArchitectLuna project.

ArchitectLuna expects:

```text
.architect/model.yaml
```

---

### Rule 2: A feature must exist before entities can be added to it

Invalid:

```bash
architect-luna add entity Invoices Invoice
```

if the `Invoices` feature does not exist.

Correct:

```bash
architect-luna add feature Invoices
architect-luna add entity Invoices Invoice
```

---

### Rule 3: CRUD requires an entity

Entity-backed CRUD is generated from an entity.

Invalid:

```bash
architect-luna add crud Invoices Invoice
```

if `Invoice` does not exist.

Correct:

```bash
architect-luna add entity Invoices Invoice
architect-luna generate
```

---

### Rule 4: Bespoke commands and queries may exist without an entity

Some operations are not entity-backed.

Examples:

```text
SendWelcomeEmail
GenerateMonthlyReport
CalculateQuote
SyncExternalSystem
```

These are allowed.

However, entity-backed operations should require the relevant entity to exist first.

---

### Rule 5: Adapter and persistence are chosen at project creation

The adapter and persistence provider should not be changed casually per feature.

They are project-level choices.

```text
adapter: mediatr | wolverine
persistence: none | efcore-postgres | efcore-sqlserver | marten
```

Future versions may support explicit adapter switching, but normal generation should use the project configuration.

---

## Supported Backend Adapters

ArchitectLuna supports two interchangeable backend adapters:

```text
mediatr
wolverine
```

The adapter controls how requests are dispatched.

Switching adapters should not change:

* Entity model
* Feature model
* Route shape
* DTO shape
* Validation rules
* HTTP surface

The adapter may change:

* Dispatch implementation
* Handler invocation style
* Dependency registration
* Framework-specific setup

The Application layer should remain clean and framework-agnostic.

---

## Supported Persistence Providers

ArchitectLuna supports:

```text
none
efcore-postgres
efcore-sqlserver
marten
```

Persistence controls how generated handlers talk to storage.

### EF Core

EF Core generation should include:

* Entity classes
* DbContext
* DbSet registration
* Entity configurations
* Provider setup
* CRUD handler logic

### Marten

Marten generation should include:

* Document classes
* Document/session setup
* Store/load/delete/query handler logic

### None

When persistence is `none`, handlers should contain protected placeholders for business logic.

---

## Result Pattern

Every production backend should avoid throwing exceptions for normal business outcomes.

ArchitectLuna should include a simple Result pattern by default.

The generated Application layer should include concepts like:

```text
Result
Result<T>
Error
ValidationError
PagedResult<T>
```

Handlers should return explicit results where appropriate.

Examples of expected outcomes:

```text
Success
Validation failure
Not found
Conflict
Unauthorized
Forbidden
Unexpected error
```

The API layer should map results into consistent HTTP responses.

---

## Base Entity

The Domain project should include a simple `BaseEntity`.

It should provide common production fields such as:

```text
Id
CreatedAt
CreatedBy
UpdatedAt
UpdatedBy
IsDeleted
```

The exact fields can evolve, but the initial scaffold should include a practical base model used across most backend systems.

Generated entities should inherit from `BaseEntity` unless explicitly configured otherwise in the future.

---

## User Context

Production systems usually need to know who is performing an action.

ArchitectLuna should include a `UserContextService` by default.

The generated project should include abstractions for:

```text
Current user id
Current user email
Current user roles/claims
Correlation id
Tenant id, if needed later
```

The Application layer should depend on an abstraction, not directly on `HttpContext`.

The Infrastructure or API layer should provide the implementation.

---

## Date and Time Abstraction

The generated project should include a date/time abstraction.

Application code should not call `DateTime.UtcNow` directly.

A service such as `IDateTimeProvider` or similar should be available.

This makes application code easier to test.

---

## Exception Handling Middleware

The API project should include centralized exception handling middleware.

The middleware should translate known exception types into consistent HTTP responses.

It should handle:

```text
Validation errors
Not found
Conflict
Unauthorized
Forbidden
Unhandled exceptions
```

Unhandled exceptions should be logged and returned as safe, generic responses.

---

## Serilog Logging

Serilog should be configured in the initial scaffold.

The project should include:

* Structured logging
* Request logging
* Environment-specific logging configuration
* Console sink by default
* Extension method registration

The goal is not to create an overly complex logging setup.

The goal is to ensure every generated backend starts with structured logging.

---

## Validation

ArchitectLuna should include FluentValidation by default.

Every generated command/query should include a validator when validation is relevant.

Validation should be wired into the request pipeline.

Handlers should not be responsible for basic request validation.

---

## Mapping Layer

Production systems should not expose persistence entities directly.

ArchitectLuna should include a mapping layer by default.

Generated features should include mapping between:

```text
Request DTOs
Application requests
Domain entities
Persistence entities/documents
Response DTOs
```

The mapping approach should be simple and explicit.

Avoid unnecessary mapping packages in V1 unless there is a strong reason.

Manual extension methods are preferred for generated code.

Example structure:

```text
Features/Invoices/CreateInvoice/
  CreateInvoiceRequest.cs
  CreateInvoiceCommand.cs
  CreateInvoiceResult.cs
  CreateInvoiceResponse.cs
  CreateInvoiceMappings.cs
```

Mappings should be generated as simple extension methods.

---

## Middleware and Extensions

Project setup should be hidden behind clean extension methods.

`Program.cs` should stay small.

Avoid directly registering everything inside the builder.

Preferred style:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseApiLogging(builder.Configuration);

builder.Services
    .AddApi(builder.Configuration)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseApiMiddleware();
app.MapApiEndpoints();

app.Run();
```

Each project should own its own extension methods.

Example:

```text
Api/
  DependencyInjection.cs
  Middleware/
    MiddlewareExtensions.cs
  Endpoints/
    EndpointExtensions.cs

Application/
  DependencyInjection.cs

Infrastructure/
  DependencyInjection.cs
```

This keeps startup clean and makes every generated service feel consistent.

---

## Package Philosophy

ArchitectLuna should avoid unnecessary packages.

Only include packages that provide clear production value.

Preferred core packages:

```text
FluentValidation
Serilog
EF Core
Npgsql EF Core provider
SQL Server EF Core provider, when selected
Marten, when selected
Wolverine, when selected
MediatR, when selected
Swagger/OpenAPI packages
Health checks packages where needed
```

Avoid adding packages for things that can be generated clearly and simply.

For example:

* Prefer generated mapping extension methods over adding a mapping library by default.
* Prefer simple Result types over adding a heavy result library.
* Prefer explicit code over hidden magic.

---

## Endpoint Registration

Every endpoint should implement a shared endpoint definition pattern.

Example concept:

```text
IEndpointDefinition
```

`Program.cs` should not be regenerated every time a feature is added.

Endpoints should be discovered and mapped through a central extension.

Example:

```csharp
app.MapApiEndpoints();
```

Adding a feature should not require manually editing startup code.

---

## Generated Feature Structure

For an `Invoice` entity in an `Invoices` feature, ArchitectLuna should generate one vertical slice per operation.

Example:

```text
src/BillingService.Api/Features/Invoices/CreateInvoice/
  CreateInvoiceRequest.cs
  CreateInvoiceCommand.cs
  CreateInvoiceResult.cs
  CreateInvoiceResponse.cs
  CreateInvoiceValidator.cs
  CreateInvoiceHandler.cs
  CreateInvoiceMappings.cs
  CreateInvoiceEndpoint.cs
```

Standard CRUD operations:

| Operation | Route                       | Purpose                    |
| --------- | --------------------------- | -------------------------- |
| Create    | `POST /api/invoices`        | Create a new invoice       |
| Update    | `PUT /api/invoices/{id}`    | Update an existing invoice |
| Delete    | `DELETE /api/invoices/{id}` | Delete an invoice          |
| GetById   | `GET /api/invoices/{id}`    | Fetch one invoice          |
| GetAll    | `GET /api/invoices`         | Fetch all invoices         |

---

## Protected Regions

Generated handler bodies should use protected regions.

Example:

```csharp
// <architect:region name="handler-body">
//
// Business logic goes here.
//
// </architect:region>
```

Regeneration must preserve developer-written code inside protected regions.

If ArchitectLuna cannot safely merge a file, it should fail clearly rather than overwrite user code.

---

## Model File as Source of Truth

The model file is the source of truth:

```text
.architect/model.yaml
```

Generated code is output.

Manual changes to generated files should not become the source of truth.

If an entity, field, rule, command, query, or route needs to be permanent, it should be represented in the model.

---

## Quick Start

```bash
# Scaffold a production-ready API solution
architect-luna new api BillingService --adapter wolverine --persistence efcore-postgres

cd BillingService

# Group related entities/commands/queries under a feature
architect-luna add feature Invoices

# Add the entity first
architect-luna add entity Invoices Invoice \
  --field CustomerId:Guid \
  --field AmountCents:long \
  --field Currency:string \
  --rule "AmountCents:GreaterThan(0)" \
  --rule "Currency:MaximumLength(3)"

# Generate the production-ready vertical slices
architect-luna generate

dotnet build
```

Need something outside standard CRUD?

```bash
architect-luna add command Invoices VoidInvoice --field Id:Guid --kind update
architect-luna add query Invoices SearchInvoices --param CustomerId:Guid --param Status:string

architect-luna generate
```

---

## What `generate` Produces

For an `Invoice` entity in an `Invoices` feature, `generate` renders one vertical slice per command/query.

| Operation | Route                       | Files                                                                      |
| --------- | --------------------------- | -------------------------------------------------------------------------- |
| Create    | `POST /api/invoices`        | Request, Command, Result, Response, Handler, Validator, Mappings, Endpoint |
| Update    | `PUT /api/invoices/{id}`    | Request, Command, Result, Response, Handler, Validator, Mappings, Endpoint |
| Delete    | `DELETE /api/invoices/{id}` | Command, Result, Handler, Endpoint                                         |
| GetById   | `GET /api/invoices/{id}`    | Query, Result, Response, Handler, Mappings, Endpoint                       |
| GetAll    | `GET /api/invoices`         | Query, Result, Response, Handler, Mappings, Endpoint                       |

If persistence is configured, handlers receive real persistence dependencies and generated CRUD logic.

If persistence is `none`, handlers contain protected placeholders.

---

## Generated Startup Style

Generated startup should be clean.

`Program.cs` should not become a dumping ground for registrations.

Preferred shape:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseApiLogging(builder.Configuration);

builder.Services
    .AddApi(builder.Configuration)
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseApiMiddleware();
app.MapApiEndpoints();

app.Run();
```

All detailed setup should live behind extension methods.

---

## Repo Layout

```text
src/
  ArchitectLuna.Cli
    Spectre.Console.Cli commands: new, add, generate

  ArchitectLuna.Core
    Intent Model, naming, route inference, validation,
    manifest, protected-region merging, generation rules

  ArchitectLuna.Templates
    Scriban templates and template engine

  ArchitectLuna.Adapters.MediatR
    MediatR adapter implementation

  ArchitectLuna.Adapters.Wolverine
    Wolverine adapter implementation

  ArchitectLuna.Persistence.EfCore
    EF Core persistence generation

  ArchitectLuna.Persistence.Marten
    Marten persistence generation

  ArchitectLuna.Ui
    Razor Pages model viewer/editor

tests/
  ArchitectLuna.Core.Tests
    Naming, routing, CRUD synthesis, protected regions,
    model validation, YAML round-tripping

  ArchitectLuna.EndToEnd.Tests
    Real scaffold/generate/build tests across adapter and persistence combinations
```

---

## CI Expectations

The repository should verify generated output continuously.

CI should:

* Build ArchitectLuna
* Run unit tests
* Scaffold sample projects
* Generate code
* Run `dotnet build` on generated output
* Test adapter × persistence combinations
* Verify protected-region regeneration

---

## Status

ArchitectLuna already supports:

* Model-driven generation
* MediatR adapter
* Wolverine adapter
* EF Core/Postgres persistence
* EF Core/SQL Server persistence
* Marten persistence
* Entity-driven CRUD synthesis
* Minimal API endpoint generation
* Protected-region regeneration
* End-to-end generated project build tests
* Razor Pages model editing UI

Next focus should be production hardening:

* Clean Architecture output refinement
* Result pattern integration
* Exception middleware standardization
* Serilog defaults
* User context service
* Mapping layer generation
* Test skeleton improvements
* Stronger generation ordering rules
* Cleaner extension method organization

---

## Final Principle

ArchitectLuna should make production backend generation boring, repeatable, and safe.

The model describes intent.

The generator creates the structure.

The developer writes the business logic.
