using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Veredito;

/// <summary>
/// The outcome of an operation that returns no value: either a success or a failure carrying an <see cref="Veredito.Error"/>.
/// </summary>
/// <remarks>
/// An <see cref="Veredito.Error"/> converts implicitly into a failed <see cref="Result"/>, so a method can simply
/// <c>return UserErrors.NotFound;</c>.
/// </remarks>
public class Result
{
    private static readonly Result SuccessResult = new(true, Error.None);

    /// <summary>
    /// Initializes a result. A success must carry <see cref="Error.None"/>; a failure must carry anything else.
    /// </summary>
    /// <remarks>
    /// <see cref="Error.None"/> is matched by reference, so an error that merely equals it by value is still a failure.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The success flag and the error disagree.</exception>
    private protected Result(bool isSuccess, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var isNone = ReferenceEquals(error, Error.None);

        if (isSuccess && !isNone)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && isNone)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The reason for the failure, or <see cref="Error.None"/> on success.</summary>
    public Error Error { get; }

    /// <summary>Returns a successful result. The instance is shared, since results are immutable.</summary>
    public static Result Success() => SuccessResult;

    /// <summary>Creates a failed result.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    /// <remarks>Use <see cref="Create{TValue}(TValue)"/> when <paramref name="value"/> may be <see langword="null"/>.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static Result<TValue> Success<TValue>(TValue value) =>
        value is not null ? new(value, true, Error.None) : throw new ArgumentNullException(nameof(value));

    /// <summary>Creates a failed result for an operation that would have returned a <typeparamref name="TValue"/>.</summary>
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>
    /// Creates a successful result when <paramref name="value"/> is not <see langword="null"/>,
    /// and a failure carrying <see cref="Error.NullValue"/> otherwise.
    /// </summary>
    public static Result<TValue> Create<TValue>(TValue? value) => Create(value, Error.NullValue);

    /// <summary>
    /// Creates a successful result when <paramref name="value"/> is not <see langword="null"/>,
    /// and a failure carrying <paramref name="errorIfNull"/> otherwise.
    /// </summary>
    /// <example>
    /// <code>
    /// Result&lt;Order&gt; order = Result.Create(repository.Find(id), OrderErrors.NotFound);
    /// </code>
    /// </example>
    public static Result<TValue> Create<TValue>(TValue? value, Error errorIfNull) =>
        value is not null ? new(value, true, Error.None) : Failure<TValue>(errorIfNull);

    /// <summary>
    /// Combines several results, inspecting all of them instead of stopping at the first failure.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Every result succeeded (or none was given): the shared <see cref="Success()"/>.</item>
    /// <item>Some failed with a type other than <see cref="ErrorType.Validation"/>: the first such error, in argument order.</item>
    /// <item>Otherwise: a single <see cref="ValidationError"/> holding every validation error, flattened and in argument order.
    /// Each error's <see cref="Error.Code"/> is used as its field name.</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var email = Email.Create(command.Email);
    /// var name = Name.Create(command.Name);
    ///
    /// if (Result.Combine(email, name).TryGetError(out var error))
    ///     return error;
    /// </code>
    /// </example>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="results"/> contains <see langword="null"/>.</exception>
    public static Result Combine(params Result[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        Error? firstNonValidation = null;
        List<Error>? validationErrors = null;

        foreach (var result in results)
        {
            if (result is null)
            {
                throw new ArgumentException("Cannot combine a null result.", nameof(results));
            }

            if (result.IsSuccess || firstNonValidation is not null)
            {
                continue;
            }

            if (result.Error.Type == ErrorType.Validation)
            {
                (validationErrors ??= []).Add(result.Error);
            }
            else
            {
                firstNonValidation = result.Error;
            }
        }

        if (firstNonValidation is not null)
        {
            return Failure(firstNonValidation);
        }

        return validationErrors is null ? SuccessResult : Failure(new ValidationError(validationErrors));
    }

    /// <summary>Gets the error when the result is a failure.</summary>
    /// <param name="error">The error on failure; <see cref="Error.None"/> on success. Never <see langword="null"/>.</param>
    /// <returns><see langword="true"/> on failure.</returns>
    /// <example>
    /// <code>
    /// if (order.Confirm().TryGetError(out var error))
    ///     return error;
    /// </code>
    /// </example>
    public bool TryGetError(out Error error)
    {
        error = Error;
        return IsFailure;
    }

    /// <summary>Returns <paramref name="onSuccess"/>'s output on success, or <paramref name="onFailure"/>'s output on failure.</summary>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess() : onFailure(Error);

    /// <summary>Runs <paramref name="next"/> on success; propagates the failure otherwise.</summary>
    public Result Bind(Func<Result> next) =>
        IsSuccess ? next() : this;

    /// <summary>Runs <paramref name="next"/> on success; propagates the failure otherwise.</summary>
    public Result<TOut> Bind<TOut>(Func<Result<TOut>> next) =>
        IsSuccess ? next() : Failure<TOut>(Error);

    /// <summary>Converts an <see cref="Veredito.Error"/> into a failed result.</summary>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Describes the outcome, e.g. <c>Success</c> or <c>Failure(User.NotFound)</c>.</summary>
    public override string ToString() => IsSuccess ? "Success" : $"Failure({Error.Code})";
}

/// <summary>
/// The outcome of an operation that returns a <typeparamref name="TValue"/> on success,
/// or a failure carrying an <see cref="Veredito.Error"/>.
/// </summary>
/// <remarks>
/// Both a <typeparamref name="TValue"/> and an <see cref="Veredito.Error"/> convert implicitly into a
/// <see cref="Result{TValue}"/>, so a method can <c>return user;</c> or <c>return UserErrors.NotFound;</c>.
/// C# forbids user-defined conversions from an interface type, so when <typeparamref name="TValue"/> is an
/// interface (e.g. <c>IReadOnlyList&lt;T&gt;</c>) use <see cref="Result.Success{TValue}(TValue)"/> instead.
/// </remarks>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>Creates a failed result, so that <c>Result&lt;User&gt;.Failure(error)</c> yields a <see cref="Result{TValue}"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="error"/> is <see cref="Error.None"/>.</exception>
    public static new Result<TValue> Failure(Error error) => Failure<TValue>(error);

    /// <summary>The value of a successful result.</summary>
    /// <exception cref="InvalidOperationException">The result is a failure.</exception>
    [NotNull]
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"The value of a failed result cannot be accessed. Error: {Error.Code}.");

    /// <summary>The value on success, or <see langword="default"/> on failure. Never throws.</summary>
    public TValue? ValueOrDefault => _value;

    // Shows the value in the debugger only; ToString deliberately omits it so logs never leak it.
    private string DebuggerDisplay => IsSuccess ? $"Success({_value})" : $"Failure({Error.Code})";

    /// <summary>Returns <paramref name="onSuccess"/>'s output on success, or <paramref name="onFailure"/>'s output on failure.</summary>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error);

    /// <summary>
    /// Transforms the value on success; propagates the failure otherwise.
    /// A <see langword="null"/> output becomes a failure carrying <see cref="Error.NullValue"/>.
    /// </summary>
    public Result<TOut> Map<TOut>(Func<TValue, TOut> map) =>
        IsSuccess ? Create(map(_value!)) : Failure<TOut>(Error);

    /// <summary>Runs <paramref name="next"/> with the value on success; returns this same failure otherwise.</summary>
    public Result Bind(Func<TValue, Result> next) =>
        IsSuccess ? next(_value!) : this;

    /// <summary>Runs <paramref name="next"/> with the value on success; propagates the failure otherwise.</summary>
    public Result<TOut> Bind<TOut>(Func<TValue, Result<TOut>> next) =>
        IsSuccess ? next(_value!) : Failure<TOut>(Error);

    /// <summary>Gets the value when the result is a success.</summary>
    /// <param name="value">The value on success; <see langword="default"/> on failure.</param>
    /// <returns><see langword="true"/> on success.</returns>
    public bool TryGetValue([MaybeNullWhen(false)] out TValue value)
    {
        value = _value;
        return IsSuccess;
    }

    /// <summary>Gets the value on success, or the error on failure, in a single call.</summary>
    /// <param name="value">The value on success; <see langword="default"/> on failure.</param>
    /// <param name="error">The error on failure; <see cref="Error.None"/> on success. Never <see langword="null"/>.</param>
    /// <returns><see langword="true"/> on success.</returns>
    /// <example>
    /// <code>
    /// if (!FindUser(id).TryGetValue(out var user, out var error))
    ///     return error;
    /// </code>
    /// </example>
    public bool TryGetValue([MaybeNullWhen(false)] out TValue value, out Error error)
    {
        value = _value;
        error = Error;
        return IsSuccess;
    }

    /// <summary>
    /// Converts a value into a result: a success when it is not <see langword="null"/>,
    /// a failure carrying <see cref="Error.NullValue"/> otherwise.
    /// </summary>
    /// <remarks>
    /// The parameter is declared non-nullable so that converting a possibly-<see langword="null"/> value
    /// raises a nullable warning; use <see cref="Result.Create{TValue}(TValue, Error)"/> to choose the error instead.
    /// </remarks>
    public static implicit operator Result<TValue>(TValue value) => Create(value);

    /// <summary>Converts an <see cref="Veredito.Error"/> into a failed result.</summary>
    public static implicit operator Result<TValue>(Error error) => Failure<TValue>(error);
}
