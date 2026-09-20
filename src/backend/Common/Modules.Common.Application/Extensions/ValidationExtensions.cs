using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using Modules.Common.Application.Logging;

namespace Modules.Common.Application.Extensions;

/// <summary>
/// Helpers for turning FluentValidation output into log-friendly text.
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Flattens a validation result into "PropertyName: message" strings.
    /// </summary>
    /// <param name="validationResult">The result to flatten.</param>
    /// <returns>One string per validation failure.</returns>
    public static IReadOnlyList<string> ToFormattedErrorMessages(this ValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(validationResult);

        return validationResult.Errors
            .Select(error => $"{error.PropertyName}: {error.ErrorMessage}")
            .ToList();
    }

    /// <summary>
    /// Logs validation failures at Warning level with a caller-supplied context message.
    /// </summary>
    /// <typeparam name="T">The logger's category type.</typeparam>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="validationResult">The failed validation result.</param>
    /// <param name="contextMessage">What was being validated, e.g. "Register user".</param>
    public static void LogValidationErrors<T>(
        this ILogger<T> logger,
        ValidationResult validationResult,
        string contextMessage)
    {
        ArgumentNullException.ThrowIfNull(logger);

        var validationErrors = validationResult.ToFormattedErrorMessages();

        logger.ValidationFailed(contextMessage, string.Join(", ", validationErrors));
    }
}
