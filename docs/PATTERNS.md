# Generated code shape

ArchitectLuna generates one style: a **vertical slice per command/query**, inside a single
ASP.NET Core minimal-API project. There is no Clean Architecture layer split (Domain/Application/
Infrastructure/Api projects) yet — see `docs/ROADMAP.md` for where that and other patterns fit.

## Solution shape (`new api`)

```
{Solution}/
  {Solution}.slnx
  .architect/
    model.yaml          # the Intent Model
    manifest.json        # every path ever generated, for future cleanup tooling
  src/{Solution}.Api/
    {Solution}.Api.csproj
    Program.cs            # adapter bootstrap + reflection-based IEndpointDefinition mapping
    Common/
      IEndpointDefinition.cs
    Features/
      {Feature}/
        {Command}/
          {Command}Command.cs
          {Command}Handler.cs
          {Command}Validator.cs      # omitted for Delete — nothing to validate but the route id
          {Command}Endpoint.cs
        {Query}/
          {Query}Query.cs
          {Query}Handler.cs
          {Query}Endpoint.cs
```

`Program.cs` is written once by `new api` and never needs to be regenerated: it discovers every
`IEndpointDefinition` in the assembly by reflection and maps it, so adding a new feature/entity
never touches it.

## Entity-driven CRUD (`add entity`)

`add entity <Feature> <Entity> --field Name:Type [--rule Field:Rule]` is the primary way to
populate a feature. It expands one entity definition into:

- `Create{Entity}` — `POST /api/{feature}`, body = entity fields, validated, returns `{ Id }`.
- `Update{Entity}` — `PUT /api/{feature}/{id}`, body = entity fields (+ `Id`), the route `id`
  overrides whatever `Id` was in the body (`command with { Id = id }`), validated.
- `Delete{Entity}` — `DELETE /api/{feature}/{id}`, no body, no validator.
- `Get{Entity}ById` — `GET /api/{feature}/{id}`, returns the entity's full field set (not just the
  id — a plain `add query ... --param Id:Guid` still defaults to echoing the id back, since it has
  no entity to draw a result shape from).
- `GetAll{Entities}` — `GET /api/{feature}`, same per-item shape as GetById, wrapped as
  `IReadOnlyList<T>`.

This is deliberately the *only* place CRUD gets scaffolded automatically. `add command`/`add
query` stay available, unchanged, for anything outside standard CRUD (a `VoidInvoice` command, a
`SearchInvoices` query with multiple filter params, etc.) — including `add command --kind
update|delete` if you want the PUT/DELETE-to-`{id}` shape without going through an entity.

## Handler protected regions

Every generated handler wraps its body in a named marker:

```csharp
public async Task<CreateInvoiceResult> Handle(CreateInvoiceCommand message, CancellationToken cancellationToken)
{
    // <architect:region name="handler-body">
    throw new NotImplementedException();
    // </architect:region>
}
```

Replace the body with real logic, then run `generate` again after changing the model (adding a
field, renaming the command) — everything between the markers is preserved verbatim; everything
outside them (usings, signatures, class names) regenerates from the model. If the markers are
removed entirely, the file is treated as fully hand-owned and `generate` overwrites it — there is
no partial merge without markers present.

With `--persistence` set to anything but `none`, the *initial* content of that region (the first
time a file is generated — after that it's yours, per the rule above) is real storage code instead
of the placeholder, e.g. for `efcore-postgres`:

```csharp
public async Task<CreateInvoiceResult> Handle(CreateInvoiceCommand message, CancellationToken cancellationToken)
{
    // <architect:region name="handler-body">
    var entity = new Invoice
    {
        Id = Guid.NewGuid(),
        CustomerId = message.CustomerId,
        AmountCents = message.AmountCents,
        Currency = message.Currency,
    };
    dbContext.Invoices.Add(entity);
    await dbContext.SaveChangesAsync(cancellationToken);
    return new CreateInvoiceResult(entity.Id);
    // </architect:region>
}
```

## Adapter parity

MediatR and Wolverine render the *same* endpoint/validator templates (`Templates/Shared`) and the
*same* route shape (`ArchitectLuna.Core/Naming/RouteInference.cs`). The only differences are:

| | MediatR | Wolverine |
|---|---|---|
| Message | `record ... : IRequest<TResult>` | plain `record`, no marker interface |
| Handler | `class : IRequestHandler<TMessage, TResult>` | `static class` with a `static Handle` method (Wolverine's naming-convention dispatch) |
| Dispatcher | `ISender.Send(message, ct)` | `IMessageBus.InvokeAsync<TResult>(message, ct)` |
| Persistence dependency injection | constructor-injected, stored in a field | extra `static Handle` method parameter (Wolverine's own convention) |
| Endpoint HTTP mapping | minimal API via `IEndpointDefinition` | identical — **not** Wolverine.Http's `[WolverinePost]`/`[WolverineGet]` attributes |

Switching `--adapter` at `new api` time changes implementation, never the HTTP contract a client
sees.

## Persistence provider parity

In-Memory (`in-memory`), EF Core (`efcore-postgres`/`efcore-sqlserver`), and Marten (`marten`) all
implement `IPersistenceGenerator` and all use the same message-field-in / entity-field-out naming
convention in generated handler bodies (`message.{Field}` for input, `entity.{Field}` for output,
`entity`/`entities` as local variable names, `KeyNotFoundException` with the same message format
for a missing Update/GetById target) — but their storage shape differs:

| | In-Memory | EF Core | Marten |
|---|---|---|---|
| Per-entity file | plain POCO class only | domain class + `IEntityTypeConfiguration<T>` | plain document class only (Marten auto-detects `Guid Id` as the document identity, no separate config needed) |
| Solution-level file | one `InMemoryStore` shared by every entity | one `DbContext` with a `DbSet<T>` per entity | none — nothing aggregates across entities |
| Injected dependency | the generated `InMemoryStore` (registered as a singleton) | the generated `DbContext` | `IDocumentSession` (covers both reads and writes, since it also implements `IQuerySession`) |
| Write | `Save`/`Remove` (dictionary keyed by entity type + id) | `Add`/`Remove` + `SaveChangesAsync` | `Store`/`Delete` + `SaveChangesAsync` |
| Read | `Find`/`GetAll` | `FirstOrDefaultAsync`/`ToListAsync` (`AsNoTracking()`) | `LoadAsync`/`Query<T>().ToListAsync()` |
| External dependency | none — no NuGet package, no connection string, no database process | Postgres or SQL Server must be reachable at runtime | Postgres (Marten is Postgres-native) must be reachable at runtime |
| Durability | process lifetime only — reset on restart | durable | durable |

In-Memory is the `new api` default specifically because it has no external dependency: a freshly
scaffolded solution has real, runnable CRUD without a database to stand up first. Switching to a
durable provider later is a `--persistence` model change, not a rewrite — the entity shape and
handler call sites look the same, only the injected dependency and its backing store differ. Like
the messaging adapters, switching `--persistence` never changes a command/query's route, only how
its handler talks to storage.
