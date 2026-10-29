namespace Veredito;

/// <summary>
/// Asynchronous counterparts of <see cref="Result"/>'s composition API, for chains that await I/O.
/// </summary>
/// <remarks>
/// Two families of overloads:
/// <list type="bullet">
/// <item>On a <see cref="Result"/> or <see cref="Result{TValue}"/>, taking an asynchronous delegate.</item>
/// <item>On a <see cref="Task{TResult}"/> of a result, taking either a synchronous or an asynchronous delegate,
/// so a chain started by an asynchronous step keeps flowing without intermediate <see langword="await"/>s.</item>
/// </list>
/// Every method is suffixed with <c>Async</c> and awaits with <c>ConfigureAwait(false)</c>.
/// </remarks>
public static class ResultAsyncExtensions
{
    // ---- Result / Result<TValue> with an asynchronous delegate ----

    /// <summary>Runs <paramref name="next"/> on success; propagates the failure otherwise.</summary>
    public static Task<Result> BindAsync(this Result result, Func<Task<Result>> next) =>
        result.IsSuccess ? next() : Task.FromResult(result);

    /// <summary>Runs <paramref name="next"/> on success; propagates the failure otherwise.</summary>
    public static Task<Result<TOut>> BindAsync<TOut>(this Result result, Func<Task<Result<TOut>>> next) =>
        result.IsSuccess ? next() : Task.FromResult(Result.Failure<TOut>(result.Error));

    /// <summary>Returns <paramref name="onSuccess"/>'s output on success, or <paramref name="onFailure"/>'s output on failure.</summary>
    public static Task<TOut> MatchAsync<TOut>(this Result result, Func<Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure) =>
        result.IsSuccess ? onSuccess() : onFailure(result.Error);

    /// <summary>
    /// Transforms the value on success; propagates the failure otherwise.
    /// A <see langword="null"/> output becomes a failure carrying <see cref="Error.NullValue"/>.
    /// </summary>
    public static async Task<Result<TOut>> MapAsync<TValue, TOut>(this Result<TValue> result, Func<TValue, Task<TOut>> map) =>
        result.IsSuccess
            ? Result.Create(await map(result.Value).ConfigureAwait(false))
            : Result.Failure<TOut>(result.Error);

    /// <summary>Runs <paramref name="next"/> with the value on success; returns this same failure otherwise.</summary>
    public static Task<Result> BindAsync<TValue>(this Result<TValue> result, Func<TValue, Task<Result>> next) =>
        result.IsSuccess ? next(result.Value) : Task.FromResult<Result>(result);

    /// <summary>Runs <paramref name="next"/> with the value on success; propagates the failure otherwise.</summary>
    public static Task<Result<TOut>> BindAsync<TValue, TOut>(this Result<TValue> result, Func<TValue, Task<Result<TOut>>> next) =>
        result.IsSuccess ? next(result.Value) : Task.FromResult(Result.Failure<TOut>(result.Error));

    /// <summary>
    /// Keeps the result when <paramref name="predicate"/> holds for its value, and fails with
    /// <paramref name="error"/> otherwise. An existing failure is propagated without running the predicate.
    /// </summary>
    public static async Task<Result<TValue>> EnsureAsync<TValue>(this Result<TValue> result, Func<TValue, Task<bool>> predicate, Error error)
    {
        if (result.IsFailure || await predicate(result.Value).ConfigureAwait(false))
        {
            return result;
        }

        return Result.Failure<TValue>(error);
    }

    /// <summary>Returns <paramref name="onSuccess"/>'s output on success, or <paramref name="onFailure"/>'s output on failure.</summary>
    public static Task<TOut> MatchAsync<TValue, TOut>(this Result<TValue> result, Func<TValue, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure) =>
        result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);

    /// <summary>Awaits <paramref name="action"/> on success, then returns the same result.</summary>
    public static async Task<TResult> TapAsync<TResult>(this TResult result, Func<Task> action)
        where TResult : Result
    {
        if (result.IsSuccess)
        {
            await action().ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Awaits <paramref name="action"/> with the value on success, then returns the same result.</summary>
    public static async Task<Result<TValue>> TapAsync<TValue>(this Result<TValue> result, Func<TValue, Task> action)
    {
        if (result.IsSuccess)
        {
            await action(result.Value).ConfigureAwait(false);
        }

        return result;
    }

    /// <summary>Awaits <paramref name="action"/> with the error on failure, then returns the same result.</summary>
    public static async Task<TResult> TapErrorAsync<TResult>(this TResult result, Func<Error, Task> action)
        where TResult : Result
    {
        if (result.IsFailure)
        {
            await action(result.Error).ConfigureAwait(false);
        }

        return result;
    }

    // ---- Task<Result> ----

    /// <summary>Awaits the result, then runs <paramref name="next"/> on success; propagates the failure otherwise.</summary>
    public static async Task<Result> BindAsync(this Task<Result> resultTask, Func<Result> next) =>
        (await resultTask.ConfigureAwait(false)).Bind(next);

    /// <summary>Awaits the result, then runs <paramref name="next"/> on success; propagates the failure otherwise.</summary>
    public static async Task<Result> BindAsync(this Task<Result> resultTask, Func<Task<Result>> next) =>
        await (await resultTask.ConfigureAwait(false)).BindAsync(next).ConfigureAwait(false);

    /// <summary>Awaits the result, then runs <paramref name="next"/> on success; propagates the failure otherwise.</summary>
    public static async Task<Result<TOut>> BindAsync<TOut>(this Task<Result> resultTask, Func<Result<TOut>> next) =>
        (await resultTask.ConfigureAwait(false)).Bind(next);

    /// <summary>Awaits the result, then runs <paramref name="next"/> on success; propagates the failure otherwise.</summary>
    public static async Task<Result<TOut>> BindAsync<TOut>(this Task<Result> resultTask, Func<Task<Result<TOut>>> next) =>
        await (await resultTask.ConfigureAwait(false)).BindAsync(next).ConfigureAwait(false);

    /// <summary>Awaits the result, then returns the output of the branch matching the outcome.</summary>
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<TOut> onSuccess, Func<Error, TOut> onFailure) =>
        (await resultTask.ConfigureAwait(false)).Match(onSuccess, onFailure);

    /// <summary>Awaits the result, then returns the output of the branch matching the outcome.</summary>
    public static async Task<TOut> MatchAsync<TOut>(this Task<Result> resultTask, Func<Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure) =>
        await (await resultTask.ConfigureAwait(false)).MatchAsync(onSuccess, onFailure).ConfigureAwait(false);

    /// <summary>Awaits the result, runs <paramref name="action"/> on success, then returns the same result.</summary>
    public static async Task<Result> TapAsync(this Task<Result> resultTask, Action action) =>
        (await resultTask.ConfigureAwait(false)).Tap(action);

    /// <summary>Awaits the result, awaits <paramref name="action"/> on success, then returns the same result.</summary>
    public static async Task<Result> TapAsync(this Task<Result> resultTask, Func<Task> action) =>
        await (await resultTask.ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

    /// <summary>Awaits the result, runs <paramref name="action"/> with the error on failure, then returns the same result.</summary>
    public static async Task<Result> TapErrorAsync(this Task<Result> resultTask, Action<Error> action) =>
        (await resultTask.ConfigureAwait(false)).TapError(action);

    /// <summary>Awaits the result, awaits <paramref name="action"/> with the error on failure, then returns the same result.</summary>
    public static async Task<Result> TapErrorAsync(this Task<Result> resultTask, Func<Error, Task> action) =>
        await (await resultTask.ConfigureAwait(false)).TapErrorAsync(action).ConfigureAwait(false);

    /// <summary>Awaits the result, then replaces the error on failure with <paramref name="map"/>'s output.</summary>
    public static async Task<Result> MapErrorAsync(this Task<Result> resultTask, Func<Error, Error> map) =>
        (await resultTask.ConfigureAwait(false)).MapError(map);

    // ---- Task<Result<TValue>> ----

    /// <summary>
    /// Awaits the result, then transforms the value on success; propagates the failure otherwise.
    /// A <see langword="null"/> output becomes a failure carrying <see cref="Error.NullValue"/>.
    /// </summary>
    public static async Task<Result<TOut>> MapAsync<TValue, TOut>(this Task<Result<TValue>> resultTask, Func<TValue, TOut> map) =>
        (await resultTask.ConfigureAwait(false)).Map(map);

    /// <summary>
    /// Awaits the result, then transforms the value on success; propagates the failure otherwise.
    /// A <see langword="null"/> output becomes a failure carrying <see cref="Error.NullValue"/>.
    /// </summary>
    public static async Task<Result<TOut>> MapAsync<TValue, TOut>(this Task<Result<TValue>> resultTask, Func<TValue, Task<TOut>> map) =>
        await (await resultTask.ConfigureAwait(false)).MapAsync(map).ConfigureAwait(false);

    /// <summary>Awaits the result, then runs <paramref name="next"/> with the value on success; propagates the failure otherwise.</summary>
    public static async Task<Result> BindAsync<TValue>(this Task<Result<TValue>> resultTask, Func<TValue, Result> next) =>
        (await resultTask.ConfigureAwait(false)).Bind(next);

    /// <summary>Awaits the result, then runs <paramref name="next"/> with the value on success; propagates the failure otherwise.</summary>
    public static async Task<Result> BindAsync<TValue>(this Task<Result<TValue>> resultTask, Func<TValue, Task<Result>> next) =>
        await (await resultTask.ConfigureAwait(false)).BindAsync(next).ConfigureAwait(false);

    /// <summary>Awaits the result, then runs <paramref name="next"/> with the value on success; propagates the failure otherwise.</summary>
    public static async Task<Result<TOut>> BindAsync<TValue, TOut>(this Task<Result<TValue>> resultTask, Func<TValue, Result<TOut>> next) =>
        (await resultTask.ConfigureAwait(false)).Bind(next);

    /// <summary>Awaits the result, then runs <paramref name="next"/> with the value on success; propagates the failure otherwise.</summary>
    public static async Task<Result<TOut>> BindAsync<TValue, TOut>(this Task<Result<TValue>> resultTask, Func<TValue, Task<Result<TOut>>> next) =>
        await (await resultTask.ConfigureAwait(false)).BindAsync(next).ConfigureAwait(false);

    /// <summary>
    /// Awaits the result, then keeps it when <paramref name="predicate"/> holds for its value,
    /// and fails with <paramref name="error"/> otherwise.
    /// </summary>
    public static async Task<Result<TValue>> EnsureAsync<TValue>(this Task<Result<TValue>> resultTask, Func<TValue, bool> predicate, Error error) =>
        (await resultTask.ConfigureAwait(false)).Ensure(predicate, error);

    /// <summary>
    /// Awaits the result, then keeps it when <paramref name="predicate"/> holds for its value,
    /// and fails with <paramref name="error"/> otherwise.
    /// </summary>
    public static async Task<Result<TValue>> EnsureAsync<TValue>(this Task<Result<TValue>> resultTask, Func<TValue, Task<bool>> predicate, Error error) =>
        await (await resultTask.ConfigureAwait(false)).EnsureAsync(predicate, error).ConfigureAwait(false);

    /// <summary>Awaits the result, then returns the output of the branch matching the outcome.</summary>
    public static async Task<TOut> MatchAsync<TValue, TOut>(this Task<Result<TValue>> resultTask, Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        (await resultTask.ConfigureAwait(false)).Match(onSuccess, onFailure);

    /// <summary>Awaits the result, then returns the output of the branch matching the outcome.</summary>
    public static async Task<TOut> MatchAsync<TValue, TOut>(this Task<Result<TValue>> resultTask, Func<TValue, Task<TOut>> onSuccess, Func<Error, Task<TOut>> onFailure) =>
        await (await resultTask.ConfigureAwait(false)).MatchAsync(onSuccess, onFailure).ConfigureAwait(false);

    /// <summary>Awaits the result, runs <paramref name="action"/> on success, then returns the same result.</summary>
    public static async Task<Result<TValue>> TapAsync<TValue>(this Task<Result<TValue>> resultTask, Action action) =>
        (await resultTask.ConfigureAwait(false)).Tap(action);

    /// <summary>Awaits the result, runs <paramref name="action"/> with the value on success, then returns the same result.</summary>
    public static async Task<Result<TValue>> TapAsync<TValue>(this Task<Result<TValue>> resultTask, Action<TValue> action) =>
        (await resultTask.ConfigureAwait(false)).Tap(action);

    /// <summary>Awaits the result, awaits <paramref name="action"/> on success, then returns the same result.</summary>
    public static async Task<Result<TValue>> TapAsync<TValue>(this Task<Result<TValue>> resultTask, Func<Task> action) =>
        await (await resultTask.ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

    /// <summary>Awaits the result, awaits <paramref name="action"/> with the value on success, then returns the same result.</summary>
    public static async Task<Result<TValue>> TapAsync<TValue>(this Task<Result<TValue>> resultTask, Func<TValue, Task> action) =>
        await (await resultTask.ConfigureAwait(false)).TapAsync(action).ConfigureAwait(false);

    /// <summary>Awaits the result, runs <paramref name="action"/> with the error on failure, then returns the same result.</summary>
    public static async Task<Result<TValue>> TapErrorAsync<TValue>(this Task<Result<TValue>> resultTask, Action<Error> action) =>
        (await resultTask.ConfigureAwait(false)).TapError(action);

    /// <summary>Awaits the result, awaits <paramref name="action"/> with the error on failure, then returns the same result.</summary>
    public static async Task<Result<TValue>> TapErrorAsync<TValue>(this Task<Result<TValue>> resultTask, Func<Error, Task> action) =>
        await (await resultTask.ConfigureAwait(false)).TapErrorAsync(action).ConfigureAwait(false);

    /// <summary>Awaits the result, then replaces the error on failure with <paramref name="map"/>'s output.</summary>
    public static async Task<Result<TValue>> MapErrorAsync<TValue>(this Task<Result<TValue>> resultTask, Func<Error, Error> map) =>
        (await resultTask.ConfigureAwait(false)).MapError(map);
}
