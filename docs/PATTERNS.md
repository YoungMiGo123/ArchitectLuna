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
public Task<CreateInvoiceResult> Handle(CreateInvoiceCommand request, CancellationToken cancellationToken)
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

## Adapter parity

MediatR and Wolverine render the *same* endpoint/validator templates (`Templates/Shared`) and the
*same* route shape (`ArchitectLuna.Core/Naming/RouteInference.cs`). The only differences are:

| | MediatR | Wolverine |
|---|---|---|
| Message | `record ... : IRequest<TResult>` | plain `record`, no marker interface |
| Handler | `class : IRequestHandler<TMessage, TResult>` | `static class` with a `static Handle` method (Wolverine's naming-convention dispatch) |
| Dispatcher | `ISender.Send(message, ct)` | `IMessageBus.InvokeAsync<TResult>(message, ct)` |
| Endpoint HTTP mapping | minimal API via `IEndpointDefinition` | identical — **not** Wolverine.Http's `[WolverinePost]`/`[WolverineGet]` attributes |

Switching `--adapter` at `new api` time changes implementation, never the HTTP contract a client
sees.
