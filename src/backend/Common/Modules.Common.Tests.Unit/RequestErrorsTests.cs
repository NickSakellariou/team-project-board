using Modules.Common.Domain.Errors;

namespace Modules.Common.Tests.Unit;

/// <summary>
/// Pins the code and message of every error in <see cref="RequestErrors"/>.
/// </summary>
/// <remarks>
/// An error code is a published contract: clients branch on it, and logs and dashboards
/// are filtered by it. Renaming a factory method silently changes the code, because the
/// code is built from <c>nameof</c>. These tests make that a failing build instead of a
/// frontend that stops recognising a failure.
/// </remarks>
public class RequestErrorsTests
{
    [Fact]
    public void NotAuthenticated_HasTheExpectedCodeAndMessage()
    {
        var error = RequestErrors.NotAuthenticated();

        Assert.Equal("Request.NotAuthenticated", error.Code);
        Assert.Equal("The request is not authenticated.", error.Description);
    }
}
