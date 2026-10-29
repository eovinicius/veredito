namespace Veredito;

/// <summary>
/// The category of an <see cref="Error"/>. Integrations translate it into their own vocabulary,
/// e.g. an HTTP status code.
/// </summary>
public enum ErrorType
{
    /// <summary>A generic failure that fits no other category.</summary>
    Failure,

    /// <summary>The input is invalid.</summary>
    Validation,

    /// <summary>The requested resource does not exist.</summary>
    NotFound,

    /// <summary>The caller is not authenticated.</summary>
    Unauthorized,

    /// <summary>The caller is authenticated but not allowed to perform the operation.</summary>
    Forbidden,

    /// <summary>The operation conflicts with the current state of the resource.</summary>
    Conflict
}
