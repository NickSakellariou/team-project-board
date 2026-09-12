namespace Modules.Common.Domain.Handlers;

/// <summary>
/// Marker interface identifying a use-case handler.
/// </summary>
/// <remarks>
/// <para>
/// It declares no members. Its only job is to be findable: at startup
/// <c>RegisterHandlersFromAssemblyContaining</c> scans a module's assembly for types
/// implementing this interface and registers each one against its own specific handler
/// interface. That is what lets a new slice work by existing, with no line added to a DI
/// file.
/// </para>
/// <para>
/// Each slice declares its own narrow interface deriving from this one, for example:
/// <code>
/// internal interface IRegisterUserHandler : IHandler
/// {
///     Task&lt;Result&lt;UserResponse&gt;&gt; HandleAsync(RegisterUserRequest request, CancellationToken ct);
/// }
/// </code>
/// The endpoint then depends on <c>IRegisterUserHandler</c> and nothing wider, so a test
/// can substitute exactly one use case. This is the role a mediator library would
/// otherwise play — see docs/adr/0003-cqrs-with-hand-written-handlers.md.
/// </para>
/// </remarks>
public interface IHandler;
