## Testing ArchitectLuna Itself

ArchitectLuna is a code generator, so the most important thing to test is not only its internal logic, but the actual code it produces.

Every CI build should verify that ArchitectLuna can:

* scaffold a new solution
* add features
* add entities
* synthesize CRUD
* generate vertical slices
* generate Clean Architecture output
* preserve protected regions
* build the generated project successfully
* support every configured adapter and persistence combination

If generated code does not compile, the build should fail.

---

## Test Strategy

ArchitectLuna should have three layers of tests.

```text
1. Core unit tests
2. Template/output snapshot tests
3. End-to-end generated project tests
```

Each layer has a different responsibility.

---

## 1. Core Unit Tests

Core unit tests should verify the generator logic without shelling out to `dotnet build`.

These tests should be fast and run on every CI build.

They should cover:

* naming conventions
* pluralization/singularization
* route inference
* feature discovery
* entity validation
* field parsing
* rule parsing
* CRUD synthesis
* model validation
* YAML serialization
* YAML round-tripping
* protected-region merging
* duplicate detection
* invalid command ordering
* adapter selection rules
* persistence selection rules

Example test categories:

```text
ArchitectLuna.Core.Tests/
  Naming/
  Routing/
  ModelValidation/
  CrudSynthesis/
  ProtectedRegions/
  Manifest/
  GenerationOrdering/
```

Example behaviours to test:

```text
Invoice -> invoices
Customer -> customers
CreateInvoice -> POST /api/invoices
GetInvoiceById -> GET /api/invoices/{id}
GetAllInvoices -> GET /api/invoices
```

The Core test suite should be quick enough to run constantly during development.

---

## 2. Template and Snapshot Tests

ArchitectLuna should include template-level tests for every supported output architecture.

Supported architecture profiles:

```text
vertical-slice
clean-architecture
```

Both profiles must produce the important production-ready code required by ArchitectLuna.

Snapshot tests should verify the generated file output for:

* initial project scaffold
* feature group
* entity
* command
* query
* CRUD
* endpoint/controller generation
* persistence output
* adapter-specific output
* production foundation files

Snapshot tests should verify that important generated files exist and contain the expected structure.

---

## Vertical Slice Architecture Template Tests

For the `vertical-slice` profile, tests should confirm that generated features are grouped by feature and operation.

Example expected output:

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

Tests should verify:

* each operation gets its own slice
* handlers are generated
* validators are generated where applicable
* mappings are generated
* endpoints are generated
* route shape is correct
* persistence code is included when configured
* protected handler regions exist
* generated output builds

---

## Clean Architecture Template Tests

For the `clean-architecture` profile, tests should confirm that generated code is placed into the correct projects and layers.

Example expected output:

```text
src/BillingService.Domain/Invoices/
  Invoice.cs

src/BillingService.Application/Invoices/CreateInvoice/
  CreateInvoiceCommand.cs
  CreateInvoiceResult.cs
  CreateInvoiceValidator.cs
  CreateInvoiceHandler.cs
  CreateInvoiceMappings.cs

src/BillingService.Contracts/Invoices/
  CreateInvoiceRequest.cs
  CreateInvoiceResponse.cs
  InvoiceDto.cs

src/BillingService.Api/Features/Invoices/
  CreateInvoiceEndpoint.cs

src/BillingService.Infrastructure/Persistence/
  AppDbContext.cs
  Configurations/InvoiceConfiguration.cs
```

Tests should verify that:

* domain entities live in Domain
* application handlers live in Application
* DTOs/contracts live in Contracts
* HTTP endpoints/controllers live in Api
* EF Core/Marten setup lives in Infrastructure
* Application does not reference Api or Infrastructure
* Domain does not reference any other project
* dispatcher-specific code does not leak into Domain
* persistence-specific code does not leak into Domain
* the generated solution builds

---

## Production Foundation Tests

Both architecture profiles must include production-ready backend foundations.

Every generated project should be tested for the presence of:

```text
Result pattern
BaseEntity
Exception handling middleware
Serilog setup
UserContextService
Date/time provider
Validation pipeline
Mapping layer
Health checks
Dependency injection extensions
Endpoint registration extensions
Persistence setup
App configuration files
Test project structure
Docker/local development files
```

The tests should not only check that files exist.

They should also verify that the generated startup follows the expected clean extension style.

Expected `Program.cs` shape:

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

`Program.cs` should not become a dumping ground for low-level registrations.

Registrations should be hidden behind extension methods.

---

## Generation Ordering Tests

ArchitectLuna must enforce generation order.

Tests should verify the following behaviours.

### Cannot add anything outside a project

Invalid:

```bash
architect-luna add feature Invoices
```

when no `.architect/model.yaml` exists.

Expected result:

```text
Command fails with a clear error.
```

---

### Cannot add an entity to a missing feature

Invalid:

```bash
architect-luna add entity Invoices Invoice
```

if `Invoices` does not exist.

Expected result:

```text
Command fails and tells the user to run:
architect-luna add feature Invoices
```

---

### Cannot generate CRUD without an entity

Invalid:

```bash
architect-luna add crud Invoices Invoice
```

if `Invoice` does not exist.

Expected result:

```text
Command fails and tells the user to create the entity first.
```

---

### Bespoke commands and queries may be added without entities

Valid:

```bash
architect-luna add command Reports GenerateMonthlyReport
architect-luna add query Reports GetReportStatus
```

These are not necessarily entity-backed and should be allowed.

---

### Duplicate entities/features should fail safely

Invalid:

```bash
architect-luna add entity Invoices Invoice
architect-luna add entity Invoices Invoice
```

Expected result:

```text
Second command should not corrupt the model.
Second command should produce a clear duplicate warning or error.
```

---

## Adapter Matrix Tests

Every supported adapter must be tested.

Supported adapters:

```text
mediatr
wolverine
```

Each adapter should be tested against each architecture profile:

```text
vertical-slice + mediatr
vertical-slice + wolverine
clean-architecture + mediatr
clean-architecture + wolverine
```

Tests should verify:

* project scaffolds successfully
* generated code compiles
* dispatching setup is correct
* endpoints use the correct dispatcher
* framework-specific code is isolated
* common feature code remains consistent

---

## Persistence Matrix Tests

Every supported persistence provider must be tested.

Supported persistence providers:

```text
none
efcore-postgres
efcore-sqlserver
marten
```

Each persistence provider should be tested against each adapter and architecture profile.

Minimum smoke matrix:

```text
vertical-slice + mediatr + none
vertical-slice + mediatr + efcore-postgres
vertical-slice + mediatr + efcore-sqlserver
vertical-slice + mediatr + marten

vertical-slice + wolverine + none
vertical-slice + wolverine + efcore-postgres
vertical-slice + wolverine + efcore-sqlserver
vertical-slice + wolverine + marten

clean-architecture + mediatr + none
clean-architecture + mediatr + efcore-postgres
clean-architecture + mediatr + efcore-sqlserver
clean-architecture + mediatr + marten

clean-architecture + wolverine + none
clean-architecture + wolverine + efcore-postgres
clean-architecture + wolverine + efcore-sqlserver
clean-architecture + wolverine + marten
```

Every combination should at least:

```text
scaffold
add feature
add entity
generate
dotnet restore
dotnet build
```

---

## Feature Generation Tests

For every architecture profile, adapter, and persistence combination, tests should verify the full feature workflow.

Required workflow:

```bash
architect-luna new api BillingService \
  --architecture vertical-slice \
  --adapter wolverine \
  --persistence efcore-postgres

cd BillingService

architect-luna add feature Invoices

architect-luna add entity Invoices Invoice \
  --field CustomerId:Guid \
  --field AmountCents:long \
  --field Currency:string \
  --rule "AmountCents:GreaterThan(0)" \
  --rule "Currency:MaximumLength(3)"

architect-luna generate

dotnet build
```

The same workflow should run for Clean Architecture:

```bash
architect-luna new api BillingService \
  --architecture clean-architecture \
  --adapter wolverine \
  --persistence efcore-postgres
```

---

## CRUD Generation Tests

CRUD generation must be tested from entity outward.

Given:

```bash
architect-luna add feature Invoices

architect-luna add entity Invoices Invoice \
  --field CustomerId:Guid \
  --field AmountCents:long \
  --field Currency:string
```

ArchitectLuna should generate:

```text
CreateInvoice
UpdateInvoice
DeleteInvoice
GetInvoiceById
GetAllInvoices
```

Tests should verify:

* all five operations are generated
* routes are correct
* validators exist for create/update
* delete does not generate unnecessary validation unless needed
* get-by-id route includes id
* get-all route is collection route
* persistence logic exists when persistence is enabled
* placeholder protected regions exist when persistence is none
* generated code builds

---

## Mapping Layer Tests

Every generated feature should include mapping code.

Tests should verify mappings exist between:

```text
Request DTO -> Application command/query
Application result -> Response DTO
Persistence entity/document -> Result/DTO
```

For Clean Architecture, tests should verify that DTOs are not placed in Domain.

For Vertical Slice Architecture, tests should verify that feature-local DTOs and mappings remain inside the slice.

---

## Result Pattern Tests

Every generated backend should include a Result pattern.

Tests should verify that generated projects include:

```text
Result
Result<T>
Error
ValidationError
PagedResult<T>
```

Tests should also verify that endpoints map results to HTTP responses consistently.

Expected mappings:

```text
Success -> 200/201/204
Validation failure -> 400
Not found -> 404
Conflict -> 409
Unauthorized -> 401
Forbidden -> 403
Unhandled exception -> 500
```

---

## Middleware Tests

Generated projects should include middleware for production API concerns.

Tests should verify that generated projects include:

```text
ExceptionHandlingMiddleware
Request logging setup
Correlation ID support
Middleware extension methods
```

Tests should also verify that middleware is registered through extension methods, not directly scattered across `Program.cs`.

---

## Dependency Injection Extension Tests

Generated projects should expose clean extension methods.

Tests should verify that the generated startup uses:

```text
AddApi
AddApplication
AddInfrastructure
UseApiMiddleware
MapApiEndpoints
UseApiLogging
```

The generated `Program.cs` should remain small and predictable.

---

## Protected Region Tests

Protected region preservation is critical.

Tests should:

1. Generate a project.
2. Modify a handler body inside the protected region.
3. Run `architect-luna generate` again.
4. Verify the custom code is still present.
5. Verify the surrounding generated structure was updated.

Example protected region:

```csharp
// <architect:region name="handler-body">
var customCode = true;
// </architect:region>
```

After regeneration, `var customCode = true;` must still exist.

---

## CI Requirements

Every pull request must run the full ArchitectLuna verification pipeline.

CI must run:

```bash
dotnet restore ArchitectLuna.sln
dotnet build ArchitectLuna.sln
dotnet test tests/ArchitectLuna.Core.Tests
dotnet test tests/ArchitectLuna.Template.Tests
dotnet test tests/ArchitectLuna.EndToEnd.Tests
```

End-to-end tests should scaffold and build generated projects.

CI should fail if:

* ArchitectLuna does not build
* core tests fail
* template snapshots fail
* generated output does not compile
* protected regions are not preserved
* generation ordering rules are broken
* any adapter × persistence × architecture smoke test fails

---

## Suggested Test Project Layout

```text
tests/
  ArchitectLuna.Core.Tests/
    Naming/
    Routing/
    ModelValidation/
    CrudSynthesis/
    ProtectedRegions/
    GenerationOrdering/
    YamlRoundTripping/

  ArchitectLuna.Template.Tests/
    VerticalSlice/
      ProductionFoundationTests.cs
      FeatureGenerationSnapshotTests.cs
      CrudGenerationSnapshotTests.cs

    CleanArchitecture/
      ProductionFoundationTests.cs
      FeatureGenerationSnapshotTests.cs
      CrudGenerationSnapshotTests.cs

  ArchitectLuna.EndToEnd.Tests/
    ScaffoldBuildMatrixTests.cs
    AdapterMatrixTests.cs
    PersistenceMatrixTests.cs
    ProtectedRegionRegenerationTests.cs
```

---

## CI Smoke Matrix

The CI smoke matrix should include:

```text
architecture:
  - vertical-slice
  - clean-architecture

adapter:
  - mediatr
  - wolverine

persistence:
  - none
  - efcore-postgres
  - efcore-sqlserver
  - marten
```

This creates 16 generated-project smoke combinations.

Each smoke test should:

```text
1. scaffold a new API
2. add a feature
3. add an entity
4. generate code
5. run dotnet build
```

For at least one representative combination, CI should also run:

```text
dotnet test
```

on the generated project.

---

## Generated Project Acceptance Criteria

A generated project passes acceptance when:

* it restores successfully
* it builds successfully
* it contains the production foundation
* it contains the expected architecture shape
* it contains the expected feature slices
* it contains the expected persistence code
* it contains the expected adapter setup
* it preserves protected regions
* it keeps startup clean through extension methods
* it does not require manual edits to compile

---

## Final Testing Principle

ArchitectLuna should treat generated code as the product.

If the generated project does not compile, ArchitectLuna is broken.

If production foundation files are missing, ArchitectLuna is incomplete.

If regeneration overwrites business logic, ArchitectLuna is unsafe.

Every CI build must prove that the generator still produces clean, production-ready backend systems.
