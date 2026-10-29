using System.Text;

namespace Veredito;

/// <summary>
/// Describes why an operation failed.
/// </summary>
/// <param name="Code">A stable, machine-readable identifier (e.g. <c>"User.NotFound"</c>) that callers can branch on.</param>
/// <param name="Message">A human-readable description of the failure.</param>
/// <param name="Type">The category of the failure.</param>
/// <remarks>
/// Not sealed so that <see cref="ValidationError"/> can extend it.
/// <see cref="ToString"/> prints <see cref="Code"/> and <see cref="Type"/> but never <see cref="Message"/>,
/// so logging an error cannot leak data embedded in its message.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="Code"/> or <paramref name="Message"/> is <see langword="null"/>.</exception>
public record Error(string Code, string Message, ErrorType Type)
{
    private readonly string _code = Code ?? throw new ArgumentNullException(nameof(Code));
    private readonly string _message = Message ?? throw new ArgumentNullException(nameof(Message));

    /// <summary>The absence of an error. Carried by every successful <see cref="Result"/>.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    /// <summary>
    /// Produced when a <see langword="null"/> value is converted into a <see cref="Result{TValue}"/>.
    /// A generic <see cref="ErrorType.Failure"/>: an unhandled <see langword="null"/> is a bug, not invalid input.
    /// </summary>
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided.", ErrorType.Failure);

    /// <summary>A stable, machine-readable identifier (e.g. <c>"User.NotFound"</c>) that callers can branch on.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string Code
    {
        get => _code;
        init => _code = value ?? throw new ArgumentNullException(nameof(Code));
    }

    /// <summary>A human-readable description of the failure. Keep user input out of it.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public string Message
    {
        get => _message;
        init => _message = value ?? throw new ArgumentNullException(nameof(Message));
    }

    /// <summary>Creates an error of type <see cref="ErrorType.Failure"/>.</summary>
    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    /// <summary>Creates an error of type <see cref="ErrorType.Validation"/>.</summary>
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    /// <summary>Creates an error of type <see cref="ErrorType.NotFound"/>.</summary>
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    /// <summary>Creates an error of type <see cref="ErrorType.Unauthorized"/>.</summary>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    /// <summary>Creates an error of type <see cref="ErrorType.Forbidden"/>.</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    /// <summary>Creates an error of type <see cref="ErrorType.Conflict"/>.</summary>
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>
    /// Whether <paramref name="other"/> identifies the same failure: same <see cref="Code"/> (ordinal) and
    /// <see cref="Type"/>, whatever the <see cref="Message"/>. Equality (<c>==</c>) still compares every member.
    /// </summary>
    /// <example>
    /// <code>
    /// if (result.Error.Is(UserErrors.NotFound))
    ///     return NotFound();
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
    public bool Is(Error other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return string.Equals(Code, other.Code, StringComparison.Ordinal) && Type == other.Type;
    }

    /// <summary>Prints <see cref="Code"/> and <see cref="Type"/>; <see cref="Message"/> is left out because it may carry user data.</summary>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append("Code = ").Append(Code).Append(", Type = ").Append(Type);

        return true;
    }
}
