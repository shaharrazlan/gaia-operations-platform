namespace Gaia.Core.Errors;

/// <summary>
/// Base class for every expected business error. The API maps each subclass to an HTTP status
/// and returns <see cref="Code"/> and <see cref="Field"/> so callers can react without parsing text.
/// Anything that is not a GaiaException is an unexpected (system) error.
/// </summary>
public abstract class GaiaException(string code, string message, string? field = null) : Exception(message)
{
    public string Code { get; } = code;

    /// <summary>The input field the error is about (e.g. "fieldA"), when there is one.</summary>
    public string? Field { get; } = field;
}

/// <summary>The request is invalid: a missing or malformed input, a bad formula, a forbidden change. → 400</summary>
public sealed class ValidationException(string message, string? field = null, string code = ErrorCodes.Validation)
    : GaiaException(code, message, field);

/// <summary>The operation doesn't exist, is disabled, or was deleted. → 404</summary>
public sealed class NotFoundException(string message)
    : GaiaException(ErrorCodes.OperationNotFound, message);

/// <summary>The inputs are valid but the operation can't produce a result (e.g. division by zero). → 422</summary>
public sealed class DomainException(string message)
    : GaiaException(ErrorCodes.Domain, message);

/// <summary>An external service (e.g. the weather provider) failed or timed out. → 502 / 504</summary>
public sealed class ExternalServiceException(string message, string code = ErrorCodes.ExternalService)
    : GaiaException(code, message);

/// <summary>The change clashes with the current state: duplicate id, or someone else saved first. → 409</summary>
public sealed class ConflictException(string message, string code, string? field = null)
    : GaiaException(code, message, field);
