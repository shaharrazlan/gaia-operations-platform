namespace Gaia.Core.Errors;

/// <summary>
/// Machine-readable error codes returned to API clients in the "code" field (R2.AC6).
/// External applications switch on these values, never on the human-readable message.
/// </summary>
public static class ErrorCodes
{
    public const string Validation = "validation_error";
    public const string OperationNotFound = "operation_not_found";
    public const string FormulaInvalid = "formula_invalid";
    public const string DuplicateId = "duplicate_id";
    public const string DuplicateName = "duplicate_name";
    public const string SystemLogicReadonly = "system_logic_readonly";
    public const string SystemCannotDelete = "system_cannot_delete";
    public const string EditConflict = "edit_conflict";
    public const string Unauthorized = "unauthorized";
    public const string Domain = "domain_error";
    public const string ExternalService = "external_service_error";
    public const string ExternalServiceTimeout = "external_service_timeout";
    public const string DatabaseUnavailable = "database_unavailable";
    public const string RouteNotFound = "route_not_found";
    public const string MethodNotAllowed = "method_not_allowed";
    public const string HttpError = "http_error";
    public const string Internal = "internal_error";
}
