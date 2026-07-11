# ArchitectLuna — Standard API Response Envelope Specification

## 1. Purpose

ArchitectLuna must generate APIs that return a consistent response shape across all generated projects, adapters, and persistence providers.

The goal is to make generated APIs easy to consume from frontends, mobile apps, integrations, and other backend services.

Regardless of whether the generated project uses:

```text
MediatR
Wolverine
EF Core
Marten
Minimal APIs
Controllers
```

the external API response contract should remain consistent.

Adapters may change how requests are dispatched internally, but they must not change how API responses are shaped.

---

## 2. Core Rule

All non-empty API responses must use a standard response envelope.

The response envelope should contain:

```text
success
payload
error
```

This gives every API consumer a predictable structure.

---

## 3. Standard Success Response

For successful responses with data:

```json
{
  "success": true,
  "payload": {
    "id": "4f8f0a4d-9e7e-4af1-909d-557d91f82455",
    "amountCents": 50000,
    "currency": "ZAR"
  },
  "error": null
}
```

Generated C# concept:

```csharp
public sealed record ApiResponse<T>(
    bool Success,
    T? Payload,
    ApiError? Error
);
```

Recommended factory methods:

```csharp
public static class ApiResponse
{
    public static ApiResponse<T> Success<T>(T payload)
    {
        return new ApiResponse<T>(
            Success: true,
            Payload: payload,
            Error: null);
    }

    public static ApiResponse<T> Failure<T>(ApiError error)
    {
        return new ApiResponse<T>(
            Success: false,
            Payload: default,
            Error: error);
    }
}
```

---

## 4. Standard Failure Response

For failed responses:

```json
{
  "success": false,
  "payload": null,
  "error": {
    "code": "payment_request_not_found",
    "message": "Payment request was not found.",
    "type": "not_found"
  }
}
```

Generated C# concept:

```csharp
public sealed record ApiError(
    string Code,
    string Message,
    string Type,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null
);
```

The error object should support:

```text
code
message
type
validationErrors
```

`validationErrors` should be null unless the error type is validation.

---

## 5. Validation Failure Response

Validation failures should use the same envelope.

Example:

```json
{
  "success": false,
  "payload": null,
  "error": {
    "code": "validation_failed",
    "message": "One or more validation errors occurred.",
    "type": "validation",
    "validationErrors": {
      "amountCents": [
        "Amount must be greater than 0."
      ],
      "currency": [
        "Currency must be 3 characters or less."
      ]
    }
  }
}
```

Validation failures should still return:

```text
400 Bad Request
```

The body should remain the standard response envelope.

---

## 6. Internal Result vs External ApiResponse

ArchitectLuna should distinguish between internal application results and external API responses.

### Internal Result

Used inside the Application layer.

Example:

```csharp
Result<CreatePaymentRequestResult>
```

Purpose:

```text
Represents application use-case outcome.
Used by handlers and dispatcher pipelines.
Should not be exposed directly as the API contract.
```

### External ApiResponse

Used at the HTTP boundary.

Example:

```csharp
ApiResponse<CreatePaymentRequestResponse>
```

Purpose:

```text
Represents the stable client-facing API response.
Returned from endpoints/controllers.
Should be consistent across generated APIs.
```

Flow:

```text
Payload
  -> Command/Query
  -> Result<TApplicationResult>
  -> ApiResponse<TApiResponse>
```

Example:

```text
CreatePaymentRequestPayload
  -> CreatePaymentRequestCommand
  -> Result<CreatePaymentRequestResult>
  -> ApiResponse<CreatePaymentRequestResponse>
```

---

## 7. HTTP Status Codes Still Matter

The response envelope does not replace HTTP status codes.

Generated APIs must still use correct HTTP status codes.

Expected mapping:

```text
200 OK                  -> success true with payload
201 Created             -> success true with payload
204 No Content          -> no body by default
400 Bad Request          -> success false with validation/error envelope
401 Unauthorized         -> success false with error envelope
403 Forbidden            -> success false with error envelope
404 Not Found            -> success false with error envelope
409 Conflict             -> success false with error envelope
500 Internal ServerError -> success false with error envelope
```

Default rule for delete operations:

```text
DELETE success -> 204 No Content
```

Optional future configuration:

```text
DELETE success -> 200 OK with ApiResponse<object?>
```

For V1, use:

```text
204 No Content
```

for successful deletes.

---

## 8. Centralized Result-to-Response Mapping

Endpoints and controllers must not manually construct response envelopes repeatedly.

Result-to-response mapping must be centralized.

Recommended location:

```text
src/<ServiceName>.Api/Results/ResultExtensions.cs
```

or equivalent.

Generated endpoints should call simple extension methods, for example:

```csharp
return result.ToOkResponse(value => value.ToResponse());
```

```csharp
return result.ToCreatedResponse(
    value => $"/api/payment-requests/{value.Id}",
    value => value.ToResponse());
```

```csharp
return result.ToNoContentResponse();
```

Do not generate endpoint code like this:

```csharp
if (result.IsSuccess)
{
    return Results.Ok(ApiResponse.Success(result.Value.ToResponse()));
}

return Results.Problem(...);
```

That logic belongs in the centralized result mapping layer.

---

## 9. Required Result Mapping Extensions

ArchitectLuna should generate standard response mapping extensions.

Required methods:

```text
ToOkResponse
ToCreatedResponse
ToNoContentResponse
ToErrorResponse
```

Optional but useful:

```text
ToAcceptedResponse
ToPagedResponse
```

Expected behaviour:

### ToOkResponse

```text
Success -> 200 OK with ApiResponse<TResponse>
Failure -> mapped error response
```

### ToCreatedResponse

```text
Success -> 201 Created with ApiResponse<TResponse>
Failure -> mapped error response
```

### ToNoContentResponse

```text
Success -> 204 No Content
Failure -> mapped error response
```

### ToErrorResponse

```text
ValidationFailure -> 400 Bad Request with ApiResponse<object?>
NotFound          -> 404 Not Found with ApiResponse<object?>
Conflict          -> 409 Conflict with ApiResponse<object?>
Unauthorized      -> 401 Unauthorized with ApiResponse<object?>
Forbidden         -> 403 Forbidden with ApiResponse<object?>
Unexpected        -> 500 Internal Server Error with ApiResponse<object?>
Failure           -> 500 or 400 depending on error type
```

---

## 10. Minimal API Endpoint Example

Generated Minimal API endpoints should return the standard envelope through centralized extensions.

Example:

```csharp
public static class CreatePaymentRequestEndpoint
{
    public static IEndpointRouteBuilder MapCreatePaymentRequestEndpoint(
        this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payment-requests", HandleAsync)
            .WithName("CreatePaymentRequest")
            .WithTags("Payments")
            .WithOpenApi()
            .Produces<ApiResponse<CreatePaymentRequestResponse>>(
                StatusCodes.Status201Created)
            .Produces<ApiResponse<object>>(
                StatusCodes.Status400BadRequest)
            .Produces<ApiResponse<object>>(
                StatusCodes.Status409Conflict)
            .Produces<ApiResponse<object>>(
                StatusCodes.Status500InternalServerError);

        return app;
    }

    private static async Task<IResult> HandleAsync(
        CreatePaymentRequestPayload payload,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<CreatePaymentRequestResult>>(
            payload.ToCommand(),
            cancellationToken);

        return result.ToCreatedResponse(
            value => $"/api/payment-requests/{value.Id}",
            value => value.ToResponse());
    }
}
```

For MediatR, only the dispatcher changes:

```csharp
private static async Task<IResult> HandleAsync(
    CreatePaymentRequestPayload payload,
    ISender sender,
    CancellationToken cancellationToken)
{
    var result = await sender.Send(
        payload.ToCommand(),
        cancellationToken);

    return result.ToCreatedResponse(
        value => $"/api/payment-requests/{value.Id}",
        value => value.ToResponse());
}
```

The response shape must remain the same.

---

## 11. Controller Endpoint Example

If controller output is enabled, controller actions must also use the same response envelope.

Example:

```csharp
[ApiController]
[Route("api/payment-requests")]
public sealed class PaymentRequestsController : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<CreatePaymentRequestResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create(
        CreatePaymentRequestPayload payload,
        CancellationToken cancellationToken)
    {
        var result = await _dispatcher.Send(
            payload.ToCommand(),
            cancellationToken);

        return result.ToCreatedActionResponse(
            this,
            nameof(GetById),
            routeValues: new { id = result.Value?.Id },
            map: value => value.ToResponse());
    }
}
```

Controllers must not bypass `ApiResponse<T>`.

---

## 12. Adapter Consistency Rule

All adapters must return the same API response shape.

### MediatR

```text
ISender.Send(...)
  -> Result<TApplicationResult>
  -> ApiResponse<TApiResponse>
```

### Wolverine

```text
IMessageBus.InvokeAsync(...)
  -> Result<TApplicationResult>
  -> ApiResponse<TApiResponse>
```

### Persistence Providers

Persistence providers must not affect API response shape.

```text
EF Core
Marten
None
```

All must map to the same `ApiResponse<T>` output.

---

## 13. OpenAPI Metadata

Generated OpenAPI metadata must describe the response envelope, not the raw response DTO.

Correct:

```csharp
.Produces<ApiResponse<CreatePaymentRequestResponse>>(
    StatusCodes.Status201Created)
```

Incorrect:

```csharp
.Produces<CreatePaymentRequestResponse>(
    StatusCodes.Status201Created)
```

Failure responses should also document the envelope:

```csharp
.Produces<ApiResponse<object>>(StatusCodes.Status400BadRequest)
.Produces<ApiResponse<object>>(StatusCodes.Status404NotFound)
.Produces<ApiResponse<object>>(StatusCodes.Status409Conflict)
.Produces<ApiResponse<object>>(StatusCodes.Status500InternalServerError)
```

---

## 14. Validation Output

Validation failures must use the response envelope.

Do not return raw `Results.ValidationProblem(...)` as the response body.

Instead, validation should eventually produce:

```csharp
ApiResponse<object>.Failure(validationError)
```

or equivalent.

The generated validation pipeline/filter must map validation failures into the standard envelope.

Expected JSON:

```json
{
  "success": false,
  "payload": null,
  "error": {
    "code": "validation_failed",
    "message": "One or more validation errors occurred.",
    "type": "validation",
    "validationErrors": {
      "field": [
        "message"
      ]
    }
  }
}
```

---

## 15. Error Type Standard

Generated projects should standardize error types.

Required error types:

```text
validation
not_found
conflict
unauthorized
forbidden
failure
unexpected
```

Optional future error types:

```text
external_dependency
timeout
rate_limited
concurrency
invalid_state
```

The Result pattern should carry enough information to map these error types to HTTP responses.

---

## 16. Generated File Locations

Recommended generated files:

```text
src/<ServiceName>.Application/Common/Results/
  Result.cs
  Error.cs
  ErrorType.cs
  ValidationError.cs

src/<ServiceName>.Api/Responses/
  ApiResponse.cs
  ApiError.cs

src/<ServiceName>.Api/Results/
  ResultExtensions.cs
```

Alternative locations are acceptable if consistent.

The important rule:

```text
Application Result is internal.
API ApiResponse is external.
Mapping between them is centralized.
```

---

## 17. Tests Required

ArchitectLuna must include tests to verify the standard response envelope.

Required tests:

```text
Generated project includes ApiResponse<T>
Generated project includes ApiError
Generated endpoints produce ApiResponse<T> metadata
Generated validation failures map to ApiResponse<object>
Generated success responses wrap payload
Generated failure responses wrap error
Minimal API output uses ApiResponse<T>
Controller output uses ApiResponse<T>
MediatR-generated projects use standard response envelope
Wolverine-generated projects use standard response envelope
EF Core persistence does not alter response shape
Marten persistence does not alter response shape
ResultExtensions centralize response mapping
Generated endpoints do not manually construct repeated response bodies
OpenAPI metadata does not expose raw response DTO as the top-level response
```

Snapshot tests should assert that generated endpoint files contain:

```text
Produces<ApiResponse<
ToOkResponse
ToCreatedResponse
ToNoContentResponse
```

Snapshot tests should assert that generated endpoint files do not contain repeated manual response construction.

---

## 18. Acceptance Criteria

This requirement is complete when:

* Every non-empty API response is wrapped in `ApiResponse<T>`.
* Success responses contain `success: true`, `payload`, and `error: null`.
* Failure responses contain `success: false`, `payload: null`, and `error`.
* Validation failures use the same response envelope.
* HTTP status codes remain correct.
* Minimal API endpoints use the envelope.
* Controller endpoints use the envelope.
* MediatR and Wolverine outputs are consistent.
* EF Core, Marten, and `none` persistence outputs are consistent.
* OpenAPI metadata documents the envelope.
* Result-to-response mapping is centralized.
* Tests verify the envelope across adapters, persistence providers, and API styles.

---

## 19. Final Principle

The client should never need to guess the response shape.

ArchitectLuna-generated APIs should always return predictable output:

```json
{
  "success": true,
  "payload": {},
  "error": null
}
```

or:

```json
{
  "success": false,
  "payload": null,
  "error": {}
}
```

Adapters, persistence providers, and endpoint styles may change internally.

The API response contract must stay the same.
