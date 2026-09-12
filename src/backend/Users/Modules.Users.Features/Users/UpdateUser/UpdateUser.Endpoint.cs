using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Common.API.Abstractions;
using Modules.Common.API.Extensions;
using Modules.Users.Domain.Policies;
using Modules.Users.Features.Users.Shared;
using Modules.Users.Features.Users.Shared.Routes;

namespace Modules.Users.Features.Users.UpdateUser;

/// <summary>The body of an update request.</summary>
/// <param name="DisplayName">The new display name.</param>
/// <remarks>
/// Only the display name. Email is the login identifier, so changing it needs a
/// confirmation flow rather than a PUT; password changes need the current password. Both
/// are slices of their own when they are needed.
/// </remarks>
public sealed record UpdateUserRequest(string DisplayName);

/// <summary>
/// <c>PUT /api/users/{userId}</c> — updates a user's profile. Requires <c>users:update</c>.
/// </summary>
public sealed class UpdateUserEndpoint : IApiEndpoint
{
    /// <inheritdoc />
    public void MapEndpoint(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(RouteConsts.Update, HandleAsync)
            .RequireAuthorization(UserPolicyConsts.Update)
            .WithTags(RouteConsts.Tag)
            .WithSummary("Update a user's profile.")
            .Produces<UserResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(
        string userId,
        [FromBody] UpdateUserRequest request,
        IValidator<UpdateUserRequest> validator,
        IUpdateUserHandler handler,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var result = await handler.HandleAsync(userId, request, cancellationToken);

        return result.IsError
            ? result.Errors.ToProblem()
            : Results.Ok(result.Value);
    }
}
