using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Common.API.Abstractions;
using Modules.Common.API.Extensions;
using Modules.Users.Features.Users.Shared;
using Modules.Users.Features.Users.Shared.Routes;

namespace Modules.Users.Features.Users.RegisterUser;

/// <summary>
/// The body of a registration request.
/// </summary>
/// <param name="Email">The email address, which also becomes the username.</param>
/// <param name="Password">The plaintext password. Hashed immediately and never stored.</param>
/// <param name="DisplayName">The name to show in the UI.</param>
public sealed record RegisterUserRequest(string Email, string Password, string DisplayName);

/// <summary>
/// <c>POST /api/users/register</c> — creates an account.
/// </summary>
public sealed class RegisterUserEndpoint : IApiEndpoint
{
    /// <inheritdoc />
    public void MapEndpoint(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(RouteConsts.Register, HandleAsync)
            // Anonymous by design — you cannot be signed in before you have an account.
            .AllowAnonymous()
            .WithTags(RouteConsts.Tag)
            .WithSummary("Register a new user account.")
            // Declaring the response types is what makes Swagger show the shapes a client
            // should expect, and what generates correct types if the frontend ever
            // generates a client from the OpenAPI document.
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // Parameters are supplied by Minimal API parameter binding: [FromBody] deserializes
    // the JSON, and the remaining three are resolved from DI because their types are
    // registered services. There is no [FromServices] attribute — since .NET 7 the
    // framework infers it, and adding one is noise.
    private static async Task<IResult> HandleAsync(
        [FromBody] RegisterUserRequest request,
        IValidator<RegisterUserRequest> validator,
        IRegisterUserHandler handler,
        CancellationToken cancellationToken)
    {
        // Validation runs before the handler, so the handler can assume its input is
        // structurally sound and concern itself only with business rules. The two kinds of
        // check are deliberately separated: "email is not blank" is validation, "email is
        // not already taken" needs the database and belongs in the handler.
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var result = await handler.HandleAsync(request, cancellationToken);

        return result.IsError
            ? result.Errors.ToProblem()
            : Results.Created($"/api/users/{result.Value!.Id}", result.Value);
    }
}
