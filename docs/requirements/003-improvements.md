# ArchitectLuna — Updated Requirements Specification

## 1. Purpose

This document defines the next set of requirements for ArchitectLuna.

ArchitectLuna is a CLI code generator for production-ready .NET backend APIs. The focus of this milestone is to improve generated project structure, entity evolution, formatting, validation, database readiness, Docker readiness, and feature generation behaviour.

The goal is to make generated projects feel immediately usable for real backend systems, with less manual cleanup after generation.

---

# 2. Main Requirements

## 2.1 Entity Field Updates and Sync

ArchitectLuna must support adding new fields to an existing entity.

When a field is added to an entity, all generated artifacts that depend on that entity must be updated automatically during generation.

Example:

```bash
architect-luna add field Payments PaymentRequest Reference:string
```

or:

```bash
architect-luna update entity Payments PaymentRequest \
  --add-field Reference:string
```

Either command style is acceptable, but the behaviour must be supported.

When a field is added, ArchitectLuna must update:

```text
Entity class
Persistence entity/document
Create command
Update command
Request DTOs
Response DTOs
Result models
Mappings
Validators
Handlers
CRUD operations
EF Core configuration
Marten document configuration
Generated tests/snapshots where applicable
```

The model remains the source of truth.

```text
.architect/model.yaml
```

Generated code is output and should be synchronized from the model.

---

## 2.2 Remove the Contracts Project

The separate `Contracts` project must be removed.

The generated solution should no longer include:

```text
src/<ServiceName>.Contracts/
```

The generated project structure should be:

```text
src/
  <ServiceName>.Api/
  <ServiceName>.Application/
  <ServiceName>.Domain/
  <ServiceName>.Infrastructure/
```

Reason:

The Contracts layer is unnecessary at this stage and adds complexity without enough value.

---

## 2.3 Move Contracts Into Feature Slices

Request/response DTOs and contracts must live inside the relevant Application feature slice, within a dedicated `Contracts` folder.

Example structure:

```text
src/FeedbackService.Application/Features/Payments/CreatePaymentRequest/
  Contracts/
    CreatePaymentRequestPayload.cs
    CreatePaymentRequestResponse.cs

  CreatePaymentRequestHandler.cs
  CreatePaymentRequestValidator.cs
  CreatePaymentRequestMappings.cs
```

For a query:

```text
src/FeedbackService.Application/Features/Payments/GetPaymentRequestById/
  Contracts/
    GetPaymentRequestByIdResponse.cs

  GetPaymentRequestByIdHandler.cs
  GetPaymentRequestByIdValidator.cs
  GetPaymentRequestByIdMappings.cs
```

The API project should contain only HTTP-specific concerns, such as endpoints or controllers.

The API layer may reference the Application feature contracts, but DTO definitions must live in the Application feature slice under the `Contracts` folder.

---

# 3. Feature Slice Structure

## 3.1 Keep the Existing Slice Folder Structure

The default feature slice folder structure must remain as-is.

Do not move to a separated `Commands/` and `Queries/` folder layout by default.

Default structure:

```text
Application/
  Features/
    Payments/
      CreatePaymentRequest/
      UpdatePaymentRequest/
      DeletePaymentRequest/
      GetPaymentRequestById/
      GetAllPaymentRequests/
```

Each operation remains in its own folder.

---

## 3.2 Group Command and Handler in the Same File by Default

The command and its handler should be in the same file by default.

This only applies to the file itself.

It does not mean changing the slice folder structure.

The file must be named after the handler.

Example:

```text
src/FeedbackService.Application/Features/Payments/CreatePaymentRequest/
  Contracts/
    CreatePaymentRequestPayload.cs
    CreatePaymentRequestResponse.cs

  CreatePaymentRequestHandler.cs
  CreatePaymentRequestValidator.cs
  CreatePaymentRequestMappings.cs
```

`CreatePaymentRequestHandler.cs` should contain:

```text
CreatePaymentRequestCommand
CreatePaymentRequestResult
CreatePaymentRequestHandler
```

Example shape:

```csharp
public sealed record CreatePaymentRequestCommand(
    Guid CustomerId,
    long AmountCents,
    string Currency
);

public sealed record CreatePaymentRequestResult(
    Guid Id,
    Guid CustomerId,
    long AmountCents,
    string Currency
);

public sealed class CreatePaymentRequestHandler
{
    public async Task<Result<CreatePaymentRequestResult>> HandleAsync(
        CreatePaymentRequestCommand command,
        CancellationToken cancellationToken)
    {
        // <architect:region name="handler-body">
        //
        // Business logic goes here.
        //
        // </architect:region>
    }
}
```

Queries should follow the same rule.

Example:

```text
src/FeedbackService.Application/Features/Payments/GetPaymentRequestById/
  Contracts/
    GetPaymentRequestByIdResponse.cs

  GetPaymentRequestByIdHandler.cs
  GetPaymentRequestByIdValidator.cs
  GetPaymentRequestByIdMappings.cs
```

`GetPaymentRequestByIdHandler.cs` should contain:

```text
GetPaymentRequestByIdQuery
GetPaymentRequestByIdResult
GetPaymentRequestByIdHandler
```

---

## 3.3 Optional Split File Mode

ArchitectLuna should support a flag/config option to split command/query and handler into separate files.

Example option:

```bash
architect-luna new api FeedbackService --operation-layout split
```

or model configuration:

```yaml
generation:
  operationLayout: split
```

Supported values:

```text
grouped
split
```

Default:

```text
grouped
```

Grouped mode:

```text
CreatePaymentRequestHandler.cs
CreatePaymentRequestValidator.cs
CreatePaymentRequestMappings.cs
Contracts/
  CreatePaymentRequestPayload.cs
  CreatePaymentRequestResponse.cs
```

Split mode:

```text
CreatePaymentRequestCommand.cs
CreatePaymentRequestResult.cs
CreatePaymentRequestHandler.cs
CreatePaymentRequestValidator.cs
CreatePaymentRequestMappings.cs
Contracts/
  CreatePaymentRequestPayload.cs
  CreatePaymentRequestResponse.cs
```

---

# 4. Auto Formatting

ArchitectLuna must automatically format generated code.

The current unformatted output is not acceptable.

Bad generated output example:

```csharp
public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequestCommand>  
{  
    public CreatePaymentRequestValidator()  
    {  
          
                  
                  
                  
                RuleFor(x => x.AmountCents).GreaterThan(0);  
                  
                  
                  
                RuleFor(x => x.Currency).MaximumLength(3);  
                  
                  
                  
                  
    }  
}
```

Expected output:

```csharp
public sealed class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequestCommand>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.AmountCents).GreaterThan(0);
        RuleFor(x => x.Currency).MaximumLength(3);
    }
}
```

Formatting should happen automatically after generation.

Default behaviour:

```text
format enabled
```

Optional flag:

```bash
architect-luna generate --no-format
```

Formatting should apply at minimum to:

```text
C# files
Project files where practical
Generated YAML where practical
```

At minimum, generated C# files must be clean and consistent.

---

# 5. Validation Requirements

## 5.1 Field-Type-Based Validation

ArchitectLuna should generate sensible validation based on field type.

Validation should not rely only on explicitly supplied rules.

Generated validators should include reasonable defaults.

Examples:

```text
string       -> NotEmpty by default unless nullable/optional
Guid         -> NotEmpty
int/long     -> sensible numeric checks where context is known
decimal      -> sensible numeric checks where context is known
DateTime     -> valid date checks where applicable
bool         -> no default validation unless specified
pageSize     -> GreaterThan(0)
pageNumber   -> GreaterThan(0)
email        -> EmailAddress if field name indicates email
currency     -> MaximumLength(3) if field name indicates currency
```

Explicit model rules should still be supported.

Example:

```bash
--rule "AmountCents:GreaterThan(0)"
--rule "Currency:MaximumLength(3)"
```

Generated output:

```csharp
public sealed class CreatePaymentRequestValidator
    : AbstractValidator<CreatePaymentRequestCommand>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.AmountCents).GreaterThan(0);
        RuleFor(x => x.Currency).MaximumLength(3);
    }
}
```

---

## 5.2 Proper Validation Pipeline

Generated projects must include a validation pipeline.

Validation should run before handlers execute.

If validation fails, the system should return a consistent validation response.

Expected behaviour:

```text
Validation failure -> 400 Bad Request
```

The exact implementation can vary by adapter, but the generated system must consistently support validation.

---

# 6. Normal Case Handling

Generated projects should include proper patterns for normal backend cases.

These are not always unexpected exceptions. They are common business/application outcomes.

ArchitectLuna should generate support for handling:

```text
Not found
Validation failure
Conflict
Unauthorized
Forbidden
Divide by zero / invalid calculation input
Invalid state
Concurrency conflict
External dependency failure
Unexpected error
```

---

## 6.1 Not Found Handling

Entity-backed operations should include not-found handling.

Operations that should include not-found checks:

```text
GetById
Update
Delete
Entity-backed bespoke commands with an Id field
```

Example:

```csharp
var paymentRequest = await db.PaymentRequests
    .FindAsync([query.Id], cancellationToken);

if (paymentRequest is null)
{
    return Result.NotFound<GetPaymentRequestByIdResult>(
        "Payment request was not found.");
}
```

---

## 6.2 Invalid Calculation Handling

Generated code should provide clear guidance for invalid calculations.

Examples:

```text
Divide by zero
Negative amounts
Invalid percentage
Invalid date ranges
Overflow-risk inputs
```

ArchitectLuna should not generate arbitrary business calculations, but it should include a consistent `Result` pattern for these cases.

Example:

```csharp
if (command.Divisor == 0)
{
    return Result.ValidationFailure<CalculationResult>(
        "Divisor cannot be zero.");
}
```

This should be treated as validation or business-rule failure, not an unhandled exception.

---

## 6.3 Result Pattern

Generated projects must include a Result pattern.

Required result concepts:

```text
Success
ValidationFailure
NotFound
Conflict
Unauthorized
Forbidden
Failure
Unexpected
```

Expected HTTP mapping:

```text
Success with value      -> 200 OK
Created                 -> 201 Created
Deleted/no value        -> 204 No Content
Validation failure      -> 400 Bad Request
Not found               -> 404 Not Found
Conflict                -> 409 Conflict
Unauthorized            -> 401 Unauthorized
Forbidden               -> 403 Forbidden
Unexpected failure      -> 500 Internal Server Error
```

Result-to-HTTP mapping should be centralized.

Do not duplicate result mapping logic in every endpoint.

Expected location:

```text
src/<ServiceName>.Api/Results/ResultExtensions.cs
```

or equivalent.

---

## 6.4 Exception Middleware

Generated projects must include exception handling middleware.

The middleware should act as a last safety net.

Normal cases should use `Result`.

Unexpected cases should be caught by middleware.

Middleware should handle:

```text
Unhandled exceptions
Validation exceptions
Database update exceptions
Concurrency exceptions
Request parsing failures
```

Production responses must not expose stack traces.

Unexpected errors should be logged.

---

# 7. Entity/Table Update Operations

ArchitectLuna should support running updates against an existing entity/table and syncing related CRUD operations.

Command concept:

```bash
architect-luna sync entity Payments PaymentRequest
```

or the same behaviour can be part of:

```bash
architect-luna generate
```

Required behaviour:

```text
Read current entity model
Identify dependent CRUD operations
Regenerate dependent artifacts
Preserve protected regions
Update persistence configuration
Update validators
Update mappings
Update DTOs/results
Format output
```

Recommendation:

`architect-luna generate` should always perform sync automatically.

A separate `sync entity` command can exist for clarity, but should not be required for correctness.

---

# 8. Compound Commands and Automatic Feature Creation

ArchitectLuna should not unnecessarily block users when a missing feature can be safely created.

If the user tries to add an entity or CRUD operation to a missing feature, ArchitectLuna should offer to create the feature.

Example:

```bash
architect-luna add entity Payments PaymentRequest
```

If `Payments` does not exist:

```text
Feature 'Payments' does not exist.
Create it now? [Y/n]
```

If yes:

```text
Created feature 'Payments'.
Added entity 'PaymentRequest'.
```

If no:

```text
Operation cancelled.
```

---

## 8.1 Non-Interactive Mode

For CI or scripted usage, ArchitectLuna should support:

```bash
architect-luna add entity Payments PaymentRequest --yes
```

or:

```bash
architect-luna add entity Payments PaymentRequest --create-missing
```

Expected behaviour:

```text
Missing feature is created automatically.
Entity creation continues.
```

If running in non-interactive mode without permission to create missing dependencies, the command should fail clearly.

---

## 8.2 CRUD and Missing Entity

If the user runs:

```bash
architect-luna add crud Payments PaymentRequest
```

and the feature does not exist, ArchitectLuna may create the feature after confirmation.

If the entity does not exist, ArchitectLuna should not generate meaningless CRUD.

It should ask for the entity to be created first.

Expected message:

```text
Entity 'PaymentRequest' does not exist.
Create it first with:
architect-luna add entity Payments PaymentRequest --field ...
```

Reason:

CRUD generation requires a useful entity definition.

---

# 9. Database Change Application

ArchitectLuna must support configurable automatic database change application.

This should work for both:

```text
EF Core
Marten
```

Database application should be configurable upfront during project creation and changeable later.

---

## 9.1 Database Apply Modes

Supported modes:

```text
manual
on-generate
on-startup
```

Default:

```text
manual
```

### manual

ArchitectLuna generates the required database configuration, but does not apply changes automatically.

Developers apply migrations or schema changes manually.

### on-generate

ArchitectLuna applies database changes when `architect-luna generate` runs.

Useful for local development and rapid prototyping.

### on-startup

The generated API applies database changes when the app starts.

Useful for internal systems and controlled environments.

This must be documented clearly because it may not be appropriate for all production environments.

---

## 9.2 Configuration Example

The model or configuration should include:

```yaml
database:
  applyMode: manual
```

or equivalent in:

```text
.architect/architect.json
```

The value should be changeable later.

Command concept:

```bash
architect-luna config set database.applyMode on-startup
```

or equivalent.

---

# 10. EF Core Requirements

Generated EF Core solutions must support migrations and database updates without manual fixes.

---

## 10.1 Correct Package References

ArchitectLuna must reference the correct EF Core packages from the correct projects.

The following error must not occur:

```text
Your startup project 'FeedbackService.Infrastructure' doesn't reference Microsoft.EntityFrameworkCore.Design.
This package is required for the Entity Framework Core Tools to work.
Ensure your startup project is correct, install the package, and try again.
```

Generated EF Core projects must include the required tooling references.

Required behaviour:

```text
Infrastructure contains EF Core persistence implementation.
API is the startup project for EF tooling.
EF Core Design package is referenced where tooling requires it.
Generated README documents the correct EF commands.
```

Expected commands should work:

```bash
dotnet ef migrations add Initial \
  --project src/FeedbackService.Infrastructure \
  --startup-project src/FeedbackService.Api
```

```bash
dotnet ef database update \
  --project src/FeedbackService.Infrastructure \
  --startup-project src/FeedbackService.Api
```

---

## 10.2 Design-Time DbContext Factory

Generated EF Core solutions must include a design-time DbContext factory.

The following error must not occur:

```text
Unable to create a 'DbContext' of type 'FeedbackServiceDbContext'.
The exception 'Unable to resolve service for type
'Microsoft.EntityFrameworkCore.DbContextOptions`1[FeedbackService.Infrastructure.FeedbackServiceDbContext]'
while attempting to activate 'FeedbackService.Infrastructure.FeedbackServiceDbContext'.'
was thrown while attempting to create an instance.
```

Expected generated file:

```text
src/FeedbackService.Infrastructure/Persistence/FeedbackServiceDbContextFactory.cs
```

The factory must allow EF tooling to create the DbContext without depending on runtime DI.

---

# 11. Marten Requirements

Generated Marten projects must support the configured database apply mode.

At minimum, generated Marten projects must:

```text
Connect to Postgres through generated configuration
Be ready to run locally
Document schema handling behaviour
Support startup-time schema application where configured
Support manual mode where configured
```

Marten-specific database application should follow the same high-level modes:

```text
manual
on-generate
on-startup
```

---

# 12. Docker Compose Readiness

Generated projects must be ready to run with Docker Compose.

After project generation, this should work:

```bash
docker compose up --build
```

Generated Docker setup should include:

```text
API service
Database service
Environment variables
Connection strings
Exposed API port
Dockerfile
docker-compose.yml
Health checks where practical
```

Expected services by persistence provider:

```text
efcore-postgres -> API + PostgreSQL
efcore-sqlserver -> API + SQL Server
marten -> API + PostgreSQL
none -> API only, unless configured otherwise
```

---

# 13. Generated Service README

Every generated service must include a project-specific `README.md`.

The README should explain:

```text
What the service is
How it was generated
Selected adapter
Selected architecture
Selected persistence provider
Selected database apply mode
How to run locally
How to run with Docker Compose
How to add a feature
How to add an entity
How to add fields to an entity
How to generate code
How to apply EF migrations
How Marten schema handling works
Where business logic belongs
How protected regions work
```

For EF Core projects, include:

```bash
dotnet ef migrations add Initial \
  --project src/<ServiceName>.Infrastructure \
  --startup-project src/<ServiceName>.Api
```

```bash
dotnet ef database update \
  --project src/<ServiceName>.Infrastructure \
  --startup-project src/<ServiceName>.Api
```

For Docker:

```bash
docker compose up --build
```

---

# 14. Extension-Based Startup

Generated startup must remain clean.

Do not place all registrations directly in `Program.cs`.

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

Setup should be hidden behind extension methods.

Expected extension files:

```text
src/<ServiceName>.Api/DependencyInjection.cs
src/<ServiceName>.Application/DependencyInjection.cs
src/<ServiceName>.Infrastructure/DependencyInjection.cs
```
