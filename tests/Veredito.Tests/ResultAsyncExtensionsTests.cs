namespace Veredito.Tests;

public class ResultAsyncExtensionsTests
{
    private static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found.");
    private static readonly Error Negative = Error.Validation("Number.Negative", "Must not be negative.");

    private static Task<Result> AsTask(Result result) => Task.FromResult(result);

    private static Task<Result<int>> AsTask(Result<int> result) => Task.FromResult(result);

    private static async Task<T> Later<T>(T value)
    {
        await Task.Yield();
        return value;
    }

    [Fact]
    public async Task BindAsync_OnResult_RunsNextOnlyOnSuccess()
    {
        var calls = 0;
        Task<Result> Next() { calls++; return Later(Result.Success()); }

        Assert.True((await Result.Success().BindAsync(Next)).IsSuccess);
        Assert.Equal(NotFound, (await Result.Failure(NotFound).BindAsync(Next)).Error);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task BindAsync_OnResultToValue_PropagatesTheFailure()
    {
        Assert.Equal(42, (await Result.Success().BindAsync(() => Later(Result.Success(42)))).Value);
        Assert.Equal(NotFound, (await Result.Failure(NotFound).BindAsync(() => Later(Result.Success(42)))).Error);
    }

    [Fact]
    public async Task MapAsync_TransformsTheValueAndPropagatesTheFailure()
    {
        Assert.Equal("42", (await Result.Success(42).MapAsync(value => Later(value.ToString()))).Value);
        Assert.Equal(NotFound, (await Result.Failure<int>(NotFound).MapAsync(value => Later(value.ToString()))).Error);
    }

    [Fact]
    public async Task MapAsync_ToNull_BecomesNullValueFailure()
    {
        var result = await Result.Success(42).MapAsync(_ => Later((string?)null));

        Assert.Equal(Error.NullValue, result.Error);
    }

    [Fact]
    public async Task BindAsync_OnResultOfTValue_ChainsAndShortCircuitsOnFirstFailure()
    {
        var calls = 0;
        Task<Result<int>> Double(int value) { calls++; return Later(Result.Success(value * 2)); }

        Assert.Equal(84, (await Result.Success(42).BindAsync(Double)).Value);
        Assert.Equal(NotFound, (await Result.Failure<int>(NotFound).BindAsync(Double)).Error);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task BindAsync_OnResultOfTValueToResult_PropagatesTheFailure()
    {
        Assert.True((await Result.Success(42).BindAsync(_ => Later(Result.Success()))).IsSuccess);
        Assert.Equal(NotFound, (await Result.Failure<int>(NotFound).BindAsync(_ => Later(Result.Success()))).Error);
    }

    [Fact]
    public async Task EnsureAsync_FailsWhenThePredicateDoesNotHold()
    {
        var result = Result.Success(42);

        Assert.Same(result, await result.EnsureAsync(value => Later(value >= 0), Negative));
        Assert.Equal(Negative, (await Result.Success(-1).EnsureAsync(value => Later(value >= 0), Negative)).Error);
        Assert.Equal(NotFound, (await Result.Failure<int>(NotFound).EnsureAsync(value => Later(value >= 0), Negative)).Error);
    }

    [Fact]
    public async Task MatchAsync_PicksTheBranchMatchingTheOutcome()
    {
        Assert.Equal("ok", await Result.Success().MatchAsync(() => Later("ok"), error => Later(error.Code)));
        Assert.Equal("User.NotFound", await Result.Failure(NotFound).MatchAsync(() => Later("ok"), error => Later(error.Code)));

        Assert.Equal(84, await Result.Success(42).MatchAsync(value => Later(value * 2), _ => Later(-1)));
        Assert.Equal(-1, await Result.Failure<int>(NotFound).MatchAsync(value => Later(value * 2), _ => Later(-1)));
    }

    [Fact]
    public async Task TaskOfResult_BindAsync_AcceptsSyncAndAsyncDelegates()
    {
        Assert.True((await AsTask(Result.Success()).BindAsync(() => Result.Success())).IsSuccess);
        Assert.True((await AsTask(Result.Success()).BindAsync(() => Later(Result.Success()))).IsSuccess);
        Assert.Equal(42, (await AsTask(Result.Success()).BindAsync(() => Result.Success(42))).Value);
        Assert.Equal(42, (await AsTask(Result.Success()).BindAsync(() => Later(Result.Success(42)))).Value);
        Assert.Equal(NotFound, (await AsTask(Result.Failure(NotFound)).BindAsync(() => Result.Success(42))).Error);
    }

    [Fact]
    public async Task TaskOfResult_MatchAsync_AcceptsSyncAndAsyncDelegates()
    {
        Assert.Equal("ok", await AsTask(Result.Success()).MatchAsync(() => "ok", error => error.Code));
        Assert.Equal("User.NotFound", await AsTask(Result.Failure(NotFound)).MatchAsync(() => Later("ok"), error => Later(error.Code)));
    }

    [Fact]
    public async Task TaskOfResult_TapAndMapError_BehaveLikeTheirSyncCounterparts()
    {
        var taps = 0;
        Error? seen = null;
        var translated = Error.Failure("Order.Failed", "Order failed.");

        await AsTask(Result.Success()).TapAsync(() => taps++);
        await AsTask(Result.Failure(NotFound)).TapAsync(() => taps++);
        await AsTask(Result.Failure(NotFound)).TapErrorAsync(error => seen = error);

        Assert.Equal(1, taps);
        Assert.Equal(NotFound, seen);
        Assert.Equal(translated, (await AsTask(Result.Failure(NotFound)).MapErrorAsync(_ => translated)).Error);
    }

    [Fact]
    public async Task TaskOfResultOfTValue_MapAsync_AcceptsSyncAndAsyncDelegates()
    {
        Result<int> fromSync = await AsTask(Result.Success(42)).MapAsync(value => value * 2);
        Result<int> fromAsync = await AsTask(Result.Success(42)).MapAsync(async value =>
        {
            await Task.Yield();
            return value * 2;
        });

        Assert.Equal(84, fromSync.Value);
        Assert.Equal(84, fromAsync.Value);
        Assert.Equal(NotFound, (await AsTask(Result.Failure<int>(NotFound)).MapAsync(value => value * 2)).Error);
    }

    [Fact]
    public async Task TaskOfResultOfTValue_BindAsync_AcceptsSyncAndAsyncDelegates()
    {
        Assert.Equal(84, (await AsTask(Result.Success(42)).BindAsync(value => Result.Success(value * 2))).Value);
        Assert.Equal(84, (await AsTask(Result.Success(42)).BindAsync(value => Later(Result.Success(value * 2)))).Value);
        Assert.True((await AsTask(Result.Success(42)).BindAsync(_ => Result.Success())).IsSuccess);
        Assert.True((await AsTask(Result.Success(42)).BindAsync(_ => Later(Result.Success()))).IsSuccess);
        Assert.Equal(NotFound, (await AsTask(Result.Failure<int>(NotFound)).BindAsync(value => Result.Success(value * 2))).Error);
    }

    [Fact]
    public async Task TaskOfResultOfTValue_EnsureAsync_AcceptsSyncAndAsyncDelegates()
    {
        Assert.Equal(Negative, (await AsTask(Result.Success(-1)).EnsureAsync(value => value >= 0, Negative)).Error);
        Assert.Equal(Negative, (await AsTask(Result.Success(-1)).EnsureAsync(value => Later(value >= 0), Negative)).Error);
        Assert.Equal(42, (await AsTask(Result.Success(42)).EnsureAsync(value => value >= 0, Negative)).Value);
    }

    [Fact]
    public async Task TaskOfResultOfTValue_MatchAsync_AcceptsSyncAndAsyncDelegates()
    {
        Assert.Equal(84, await AsTask(Result.Success(42)).MatchAsync(value => value * 2, _ => -1));
        Assert.Equal(-1, await AsTask(Result.Failure<int>(NotFound)).MatchAsync(value => Later(value * 2), _ => Later(-1)));
    }

    [Fact]
    public async Task TaskOfResultOfTValue_TapAndMapError_KeepTheValueType()
    {
        var seen = 0;
        var taps = 0;
        Error? error = null;
        var translated = Error.Failure("Order.Failed", "Order failed.");

        Result<int> tapped = await AsTask(Result.Success(42)).TapAsync(value => seen = value);
        Result<int> tappedWithoutValue = await AsTask(Result.Success(42)).TapAsync(() => taps++);
        Result<int> tappedError = await AsTask(Result.Failure<int>(NotFound)).TapErrorAsync(e => error = e);
        Result<int> mapped = await AsTask(Result.Failure<int>(NotFound)).MapErrorAsync(_ => translated);

        Assert.Equal(42, tapped.Value);
        Assert.Equal(42, seen);
        Assert.Equal(1, taps);
        Assert.Equal(NotFound, error);
        Assert.Equal(translated, mapped.Error);
    }

    [Fact]
    public async Task BindAsync_OnResultOfTValueToResult_OnFailure_ReturnsTheSameInstance()
    {
        var calls = 0;
        var failure = Result.Failure<int>(NotFound);

        var result = await failure.BindAsync(_ => { calls++; return Later(Result.Success()); });

        Assert.Same(failure, result);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task TapAsync_OnResult_AwaitsTheActionOnlyOnSuccess()
    {
        var taps = 0;
        var success = Result.Success();
        var failure = Result.Failure(NotFound);

        Assert.Same(success, await success.TapAsync(async () => { await Task.Yield(); taps++; }));
        Assert.Same(failure, await failure.TapAsync(async () => { await Task.Yield(); taps++; }));
        Assert.Equal(1, taps);
    }

    [Fact]
    public async Task TapAsync_OnResultOfTValue_AwaitsTheActionWithTheValue()
    {
        var seen = 0;
        var taps = 0;
        var success = Result.Success(42);

        Result<int> tapped = await success.TapAsync(async value => { await Task.Yield(); seen = value; });
        Result<int> tappedWithoutValue = await success.TapAsync(async () => { await Task.Yield(); taps++; });
        await Result.Failure<int>(NotFound).TapAsync(async _ => { await Task.Yield(); seen = -1; });

        Assert.Same(success, tapped);
        Assert.Same(success, tappedWithoutValue);
        Assert.Equal(42, seen);
        Assert.Equal(1, taps);
    }

    [Fact]
    public async Task TapErrorAsync_OnResult_AwaitsTheActionOnlyOnFailure()
    {
        Error? seen = null;
        var failure = Result.Failure<int>(NotFound);

        Result<int> returned = await failure.TapErrorAsync(async error => { await Task.Yield(); seen = error; });
        await Result.Success(42).TapErrorAsync(async _ => { await Task.Yield(); seen = Negative; });

        Assert.Same(failure, returned);
        Assert.Equal(NotFound, seen);
    }

    [Fact]
    public async Task TaskOfResult_TapAsync_AwaitsAsyncActions()
    {
        var taps = 0;
        Error? seen = null;

        await AsTask(Result.Success()).TapAsync(async () => { await Task.Delay(10); taps++; });
        await AsTask(Result.Failure(NotFound)).TapAsync(async () => { await Task.Delay(10); taps++; });
        await AsTask(Result.Failure(NotFound)).TapErrorAsync(async error => { await Task.Delay(10); seen = error; });

        Assert.Equal(1, taps);
        Assert.Equal(NotFound, seen);
    }

    [Fact]
    public async Task TaskOfResultOfTValue_TapAsync_AwaitsAsyncActions()
    {
        var seen = 0;
        var taps = 0;
        Error? error = null;

        Result<int> tapped = await AsTask(Result.Success(42)).TapAsync(async value => { await Task.Delay(10); seen = value; });
        await AsTask(Result.Success(42)).TapAsync(async () => { await Task.Delay(10); taps++; });
        await AsTask(Result.Failure<int>(NotFound)).TapErrorAsync(async e => { await Task.Delay(10); error = e; });

        Assert.Equal(42, tapped.Value);
        Assert.Equal(42, seen);
        Assert.Equal(1, taps);
        Assert.Equal(NotFound, error);
    }

    [Fact]
    public async Task TapAsync_PropagatesExceptionsFromTheAction()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AsTask(Result.Success(42)).TapAsync(async _ =>
            {
                await Task.Yield();
                throw new InvalidOperationException();
            }));
    }

    private sealed record Order(int Id, decimal Total);

    private static readonly Error Declined = Error.Failure("Payment.Declined", "Payment declined.");

    private static Task<Result<string>> Charge(Order order) =>
        Later<Result<string>>(order.Total > 100 ? Declined : $"tx-{order.Id}");

    [Theory]
    [InlineData(50, "tx-1", null)]
    [InlineData(500, null, "Payment.Declined")]
    public async Task Pipeline_ComposesSyncAndAsyncSteps(int total, string? transaction, string? loggedCode)
    {
        string? logged = null;
        Order? found = new(1, total);

        var result = await Result.Create(found, NotFound)
            .Ensure(order => order.Total > 0, Negative)
            .BindAsync(Charge)
            .TapErrorAsync(error => logged = error.Code);

        Assert.Equal(transaction, result.ValueOrDefault);
        Assert.Equal(loggedCode, logged);
    }
}
