namespace Veredito.Tests;

public class ResultExtensionsTests
{
    private static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found.");
    private static readonly Error Negative = Error.Validation("Number.Negative", "Must not be negative.");

    [Fact]
    public void Ensure_KeepsTheSameResultWhenThePredicateHolds()
    {
        var result = Result.Success(42);

        Assert.Same(result, result.Ensure(value => value >= 0, Negative));
    }

    [Fact]
    public void Ensure_FailsWhenThePredicateDoesNotHold()
    {
        Assert.Equal(Negative, Result.Success(-1).Ensure(value => value >= 0, Negative).Error);
    }

    [Fact]
    public void Ensure_PropagatesAnExistingFailureWithoutRunningThePredicate()
    {
        var calls = 0;

        var result = Result.Failure<int>(NotFound).Ensure(_ => { calls++; return true; }, Negative);

        Assert.Equal(NotFound, result.Error);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Tap_RunsOnlyOnSuccessAndKeepsTheSameResult()
    {
        var calls = 0;
        var success = Result.Success();
        var failure = Result.Failure(NotFound);

        Assert.Same(success, success.Tap(() => calls++));
        Assert.Same(failure, failure.Tap(() => calls++));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Tap_OnResultOfTValue_KeepsTheValueType()
    {
        var calls = 0;

        Result<int> result = Result.Success(42).Tap(() => calls++);

        Assert.Equal(42, result.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void TapWithValue_PassesTheValueOnSuccess()
    {
        var seen = 0;

        Result.Success(42).Tap(value => seen = value);
        Result.Failure<int>(NotFound).Tap(_ => seen = -1);

        Assert.Equal(42, seen);
    }

    [Fact]
    public void TapError_RunsOnlyOnFailureAndKeepsTheSameResult()
    {
        Error? seen = null;
        var failure = Result.Failure<int>(NotFound);

        Result<int> returned = failure.TapError(error => seen = error);
        Result.Success(42).TapError(_ => seen = Negative);

        Assert.Same(failure, returned);
        Assert.Equal(NotFound, seen);
    }

    [Fact]
    public void MapError_TranslatesTheErrorOnFailure()
    {
        var translated = Error.Failure("Order.Failed", "Order failed.");

        Assert.Equal(translated, Result.Failure(NotFound).MapError(_ => translated).Error);
        Assert.Equal(translated, Result.Failure<int>(NotFound).MapError(_ => translated).Error);
    }

    [Fact]
    public void MapError_KeepsTheSameResultOnSuccess()
    {
        var success = Result.Success(42);

        Result<int> returned = success.MapError(_ => NotFound);

        Assert.Same(success, returned);
    }
}
