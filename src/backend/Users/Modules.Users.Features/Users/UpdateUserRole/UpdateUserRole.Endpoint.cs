using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Common.API.Abstractions;
using Modules.Common.API.Extensions;
using Modules.Users.Domain.Errors;
using Modules.Users.Domain.Policies;
using Modules.Users.Features.Users.Shared;
using Modules.Users.Features.Users.Shared.Routes;

namespace Modules.Users.Features.Users.UpdateUserRole;

/// <summary>The body of a role change.</summary>
/// <param name="Role">The system role to grant. Replaces any existing role.</param>
public sealed record UpdateUserRoleRequest(string Role);

/// <summary>
/// <c>PUT /api/users/{userId}/role</c> — changes a user's system role.
/// Requires <c>users:update</c>.
/// </summary>
/// <remarks>
/// A separate slice from <c>UpdateUser</c> rather than another field on it, because it is
/// a different operation with a different risk profile: granting a role is privilege
/// escalation, and keeping it separate means it is separately auditable and separately
/// authorizable if the permissions ever diverge.
/// </remarks>
public sealed class UpdateUserRoleEndpoint : IApiEndpoint
{
    /// <inheritdoc />
    public void MapEndpoint(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(RouteConsts.UpdateRole, HandleAsync)
            .RequireAuthorization(UserPolicyConsts.Update)
            .WithTags(RouteConsts.Tag)
            .WithSummary("Change a user's system role.")
            .Produces<UserResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleAsync(
        string userId,
        [FromBody] UpdateUserRoleRequest request,
        ClaimsPrincipal principal,
        IValidator<UpdateUserRoleRequest> validator,
        IUpdateUserRoleHandler handler,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var callerId = principal.GetUserId();
        if (string.IsNullOrEmpty(callerId))
        {
            return UserErrors.NotAuthenticated().ToProblemResult();
        }

        var result = await handler.HandleAsync(userId, callerId, request, cancellationToken);

        return result.IsError
            ? result.Errors.ToProblem()
            : Results.Ok(result.Value);
    }
}
