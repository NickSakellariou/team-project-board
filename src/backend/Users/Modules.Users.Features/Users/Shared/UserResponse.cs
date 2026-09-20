namespace Modules.Users.Features.Users.Shared;

/// <summary>
/// A user as returned by this module's HTTP endpoints.
/// </summary>
/// <param name="Id">The user's id.</param>
/// <param name="Email">The user's email address.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Roles">The user's system roles.</param>
/// <remarks>
/// <para>
/// A response DTO, deliberately separate from the <c>User</c> entity. Returning the entity
/// would serialize <c>PasswordHash</c>, <c>SecurityStamp</c> and the lockout fields
/// straight to the client — and would make every future change to the entity a breaking
/// API change.
/// </para>
/// <para>
/// This is different again from <c>UserSummary</c> in the PublicApi project: that one is
/// the contract with other <i>modules</i>, this one with HTTP <i>clients</i>. They happen
/// to look similar today; keeping them separate means the frontend and the Boards module
/// can evolve without dragging each other along.
/// </para>
/// </remarks>
public sealed record UserResponse(string Id, string Email, string DisplayName, IReadOnlyList<string> Roles);
