# Target Patterns

Invero Architect ships one template pack per architecture pattern. All packs
consume the same IR (see `ARCHITECTURE.md`); they differ only in how they lay
files out and which scaffolding they emit.

## Clean Architecture

Generates a full multi-project solution following the standard dependency
rule (dependencies point inward toward Domain):

```
src/
  {Namespace}.Domain/           # entities, value objects, domain events, no dependencies
  {Namespace}.Application/      # use cases (commands/queries), interfaces, DTOs — depends on Domain
  {Namespace}.Infrastructure/   # EF Core, external services — implements Application interfaces
  {Namespace}.Api/              # controllers/minimal API endpoints, DI composition — depends on all
tests/
  {Namespace}.Domain.Tests/
  {Namespace}.Application.Tests/
```

- One entity in the spec ⇒ a Domain entity/aggregate class + EF configuration
  stub in Infrastructure.
- One use case in the spec ⇒ a command/query + handler in Application, a
  matching endpoint in Api.
- Project references and `.sln` wiring are generated, not hand-maintained.

## Vertical Slice Architecture

Generates one self-contained folder per use case instead of horizontal
layers. Optimized for systems where a feature's request → handler → response
should live and change together:

```
src/{Namespace}/
  Features/
    PlaceOrder/
      PlaceOrderCommand.cs
      PlaceOrderHandler.cs
      PlaceOrderEndpoint.cs
      PlaceOrderValidator.cs
    Orders/
      Order.cs                  # shared entity, referenced by slices that touch it
```

- Each `useCases` entry in the spec produces one slice folder with a fixed
  internal shape (command/query, handler, endpoint, validator).
- Entities referenced by more than one slice are emitted once into a shared
  location and referenced, not duplicated.
- No Application/Infrastructure layer split; cross-cutting concerns (e.g.
  persistence) are injected per-handler via interfaces defined alongside the
  slice that first needs them.

## Entities / DDD building blocks

The smallest pack: generates domain building blocks only, with no
application or presentation layer, for use inside an existing solution:

- Entities and aggregates with identity, encapsulated state, and invariant
  enforcement generated from the `invariants` list in the spec.
- Value objects (equality by value, immutability).
- Domain events, raised from entity methods where the spec marks a state
  transition as event-worthy.

This pack is what `Clean Architecture` and `Vertical Slice` both build on
top of for their Domain/entity output — it is not a separate code path.

## Generic multi-target templates

A pack authoring contract for architecture styles not built into this repo.
A generic pack:

- Depends only on `InveroArchitect.Core` (IR) and `InveroArchitect.Generation`
  (rendering pipeline), never on another pattern pack.
- Implements the same `IR node → rendered files` contract as the built-in
  packs, using Scriban templates.
- Is versioned and content-addressed like any other pack (see
  `ARCHITECTURE.md` § Determinism guarantees), so a spec can pin a
  third-party or internal-only pack and still regenerate deterministically.

This is what makes "clean architecture, vertical slices, entities" a starting
set rather than the ceiling — teams with their own house style can write a
pack against the same contract.
