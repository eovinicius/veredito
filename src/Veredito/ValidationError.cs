using System.Text;

namespace Veredito;

/// <summary>
/// Aggregates several per-field failures into a single <see cref="Error"/>, so that
/// request validation and domain validation surface in the same shape.
/// </summary>
/// <remarks>
/// Each item in <see cref="Errors"/> uses <see cref="Error.Code"/> as the field name
/// and <see cref="Error.Message"/> as the message for that field.
/// Nested validation errors are flattened into their per-field errors (their summary message is dropped).
/// Two instances are equal when their code, message and per-field errors (in order) are equal.
/// </remarks>
public sealed record ValidationError : Error
{
    /// <summary>The code carried by every <see cref="ValidationError"/>.</summary>
    public const string DefaultCode = "Validation.Failed";

    /// <summary>The message used when none is provided.</summary>
    public const string DefaultMessage = "One or more validation errors occurred.";

    private readonly Error[] _errors;

    /// <summary>Creates a validation error from the given per-field errors.</summary>
    /// <param name="errors">
    /// The per-field errors; at least one is required. The list is copied, and nested
    /// <see cref="ValidationError"/>s are replaced by their own per-field errors.
    /// </param>
    /// <param name="message">An optional summary message; defaults to <see cref="DefaultMessage"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> or <paramref name="message"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errors"/> is empty, or contains <see langword="null"/> or <see cref="Error.None"/>.</exception>
    public ValidationError(IReadOnlyList<Error> errors, string message = DefaultMessage)
        : base(DefaultCode, message, ErrorType.Validation)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException("A validation error must carry at least one per-field error.", nameof(errors));
        }

        _errors = Flatten(errors);
        Errors = Array.AsReadOnly(_errors);
    }

    /// <summary>The per-field errors. Never contains another <see cref="ValidationError"/>.</summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>Whether <paramref name="other"/> has the same code, message and per-field errors, in order.</summary>
    public bool Equals(ValidationError? other) =>
        other is not null && base.Equals(other) && _errors.AsSpan().SequenceEqual(other._errors);

    /// <summary>Returns a hash code consistent with <see cref="Equals(ValidationError)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());

        foreach (var error in _errors)
        {
            hash.Add(error);
        }

        return hash.ToHashCode();
    }

    /// <summary>Appends the field names; their messages are left out because they may carry user data.</summary>
    protected override bool PrintMembers(StringBuilder builder)
    {
        if (base.PrintMembers(builder))
        {
            builder.Append(", ");
        }

        builder.Append("Errors = [");
        builder.AppendJoin(", ", _errors.Select(error => error.Code));
        builder.Append(']');

        return true;
    }

    // Nested validation errors are already flat by construction, so one level of unwrapping is enough.
    private static Error[] Flatten(IReadOnlyList<Error> errors)
    {
        var flattened = new List<Error>(errors.Count);

        foreach (var error in errors)
        {
            if (error is null)
            {
                throw new ArgumentException("A validation error cannot carry a null per-field error.", nameof(errors));
            }

            if (ReferenceEquals(error, None))
            {
                throw new ArgumentException("A validation error cannot carry Error.None.", nameof(errors));
            }

            if (error is ValidationError nested)
            {
                flattened.AddRange(nested._errors);
            }
            else
            {
                flattened.Add(error);
            }
        }

        return flattened.ToArray();
    }
}
