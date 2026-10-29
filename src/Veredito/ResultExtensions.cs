using System.ComponentModel;

namespace Veredito;

/// <summary>
/// Combinators that return the same kind of result they receive, so a <see cref="Result{TValue}"/>
/// keeps its value type through the chain.
/// </summary>
/// <remarks>
/// They are extensions rather than instance methods because an instance method declared on <see cref="Result"/>
/// would return a plain <see cref="Result"/> when called on a <see cref="Result{TValue}"/>, losing the value.
/// </remarks>
public static class ResultExtensions
{
    /// <summary>
    /// Keeps the result when <paramref name="predicate"/> holds for its value, and fails with
    /// <paramref name="error"/> otherwise. An existing failure is propagated without running the predicate.
    /// </summary>
    public static Result<TValue> Ensure<TValue>(this Result<TValue> result, Func<TValue, bool> predicate, Error error)
    {
        if (result.IsFailure || predicate(result.Value))
        {
            return result;
        }

        return Result.Failure<TValue>(error);
    }

    /// <summary>Runs <paramref name="action"/> on success, then returns the same result.</summary>
    public static TResult Tap<TResult>(this TResult result, Action action)
        where TResult : Result
    {
        if (result.IsSuccess)
        {
            action();
        }

        return result;
    }

    /// <summary>Runs <paramref name="action"/> with the value on success, then returns the same result.</summary>
    public static Result<TValue> Tap<TValue>(this Result<TValue> result, Action<TValue> action)
    {
        if (result.IsSuccess)
        {
            action(result.Value);
        }

        return result;
    }

    /// <summary>Runs <paramref name="action"/> with the error on failure, then returns the same result.</summary>
    public static TResult TapError<TResult>(this TResult result, Action<Error> action)
        where TResult : Result
    {
        if (result.IsFailure)
        {
            action(result.Error);
        }

        return result;
    }

    // The overloads below exist only to turn an asynchronous delegate passed to Tap/TapError into a compile error:
    // without them it would bind to the Action overload and run as fire-and-forget (async void).

    /// <summary>Not supported: <c>Tap</c> cannot await <paramref name="action"/>. Use <c>TapAsync</c> instead.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    [Obsolete("Tap does not await asynchronous actions; use TapAsync instead.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TResult Tap<TResult>(this TResult result, Func<Task> action)
        where TResult : Result =>
        throw new NotSupportedException("Tap does not await asynchronous actions; use TapAsync instead.");

    /// <summary>Not supported: <c>Tap</c> cannot await <paramref name="action"/>. Use <c>TapAsync</c> instead.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    [Obsolete("Tap does not await asynchronous actions; use TapAsync instead.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Result<TValue> Tap<TValue>(this Result<TValue> result, Func<TValue, Task> action) =>
        throw new NotSupportedException("Tap does not await asynchronous actions; use TapAsync instead.");

    /// <summary>Not supported: <c>TapError</c> cannot await <paramref name="action"/>. Use <c>TapErrorAsync</c> instead.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    [Obsolete("TapError does not await asynchronous actions; use TapErrorAsync instead.", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static TResult TapError<TResult>(this TResult result, Func<Error, Task> action)
        where TResult : Result =>
        throw new NotSupportedException("TapError does not await asynchronous actions; use TapErrorAsync instead.");

    /// <summary>Replaces the error on failure with <paramref name="map"/>'s output; returns the same result on success.</summary>
    public static Result MapError(this Result result, Func<Error, Error> map) =>
        result.IsSuccess ? result : Result.Failure(map(result.Error));

    /// <summary>Replaces the error on failure with <paramref name="map"/>'s output; returns the same result on success.</summary>
    public static Result<TValue> MapError<TValue>(this Result<TValue> result, Func<Error, Error> map) =>
        result.IsSuccess ? result : Result.Failure<TValue>(map(result.Error));
}
