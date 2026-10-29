using System.Reflection;

namespace Veredito.Tests;

public class ResultTests
{
    private static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found.");

    [Fact]
    public void Success_IsSuccessWithNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_IsFailureCarryingTheError()
    {
        var result = Result.Failure(NotFound);

        Assert.True(result.IsFailure);
        Assert.Equal(NotFound, result.Error);
    }

    [Fact]
    public void Failure_WithErrorNone_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Result.Failure(Error.None));
        Assert.Throws<InvalidOperationException>(() => Result.Failure<int>(Error.None));
    }

    [Fact]
    public void Failure_WithNullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
        Assert.Throws<ArgumentNullException>(() => Result.Failure<int>(null!));
    }

    [Fact]
    public void Failure_WithErrorEqualToNoneButDistinctInstance_IsFailure()
    {
        var empty = Error.Failure(string.Empty, string.Empty);

        var result = Result.Failure(empty);

        Assert.True(result.IsFailure);
        Assert.Same(empty, result.Error);
    }

    [Fact]
    public void Success_ReturnsSameInstance()
    {
        Assert.Same(Result.Success(), Result.Success());
    }

    [Fact]
    public void Constructors_AreNotVisibleOutsideTheLibrary()
    {
        var constructors = typeof(Result).GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.All(constructors, constructor => Assert.True(constructor.IsFamilyAndAssembly || constructor.IsAssembly));
    }

    [Fact]
    public void ToString_DescribesTheOutcome()
    {
        Assert.Equal("Success", Result.Success().ToString());
        Assert.Equal("Failure(User.NotFound)", Result.Failure(NotFound).ToString());
    }

    [Fact]
    public void Error_ConvertsImplicitlyIntoFailedResult()
    {
        Result result = NotFound;

        Assert.True(result.IsFailure);
        Assert.Equal(NotFound, result.Error);
    }

    [Fact]
    public void TryGetError_ReturnsTheErrorOnFailure()
    {
        Assert.True(Result.Failure(NotFound).TryGetError(out var error));
        Assert.Equal(NotFound, error);
    }

    [Fact]
    public void TryGetError_ReturnsNoneOnSuccess()
    {
        Assert.False(Result.Success().TryGetError(out var error));
        Assert.Same(Error.None, error);
    }

    [Fact]
    public void TryGetError_IsAvailableOnResultOfTValue()
    {
        Assert.True(Result.Failure<int>(NotFound).TryGetError(out var error));
        Assert.Equal(NotFound, error);
    }

    [Fact]
    public void TryGetError_PropagatesTheFailureInOneLine()
    {
        static Result Confirm() => NotFound;

        static Result<int> Handle()
        {
            if (Confirm().TryGetError(out var error))
                return error;

            return 42;
        }

        Assert.Equal(NotFound, Handle().Error);
    }

    [Fact]
    public void Combine_WhenAllSucceed_ReturnsTheSharedSuccess()
    {
        Assert.Same(Result.Success(), Result.Combine(Result.Success(), Result.Success(42)));
        Assert.Same(Result.Success(), Result.Combine());
    }

    [Fact]
    public void Combine_WithNullArrayOrItem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Combine(null!));
        Assert.Throws<ArgumentException>(() => Result.Combine(Result.Success(), null!));
        Assert.Throws<ArgumentException>(() => Result.Combine(Result.Failure(NotFound), null!));
    }

    [Fact]
    public void Combine_AggregatesValidationFailuresInOrder()
    {
        var result = Result.Combine(
            Result.Failure<string>(Error.Validation("Email", "Invalid.")),
            Result.Success(),
            Result.Failure<int>(Error.Validation("Name", "Too long.")));

        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(["Email", "Name"], validation.Errors.Select(field => field.Code));
    }

    [Fact]
    public void Combine_FlattensAlreadyAggregatedValidation()
    {
        var aggregated = new ValidationError([
            Error.Validation("Email", "Invalid."),
            Error.Validation("Phone", "Invalid."),
        ]);

        var result = Result.Combine(Result.Failure(aggregated), Result.Failure(Error.Validation("Name", "Too long.")));

        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(["Email", "Phone", "Name"], validation.Errors.Select(field => field.Code));
    }

    [Fact]
    public void Combine_WrapsASingleValidationFailure()
    {
        var result = Result.Combine(Result.Success(), Result.Failure(Error.Validation("Email", "Invalid.")));

        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal("Email", Assert.Single(validation.Errors).Code);
    }

    [Fact]
    public void Combine_PrefersTheFirstNonValidationFailure()
    {
        var conflict = Error.Conflict("User.Taken", "Taken.");

        var result = Result.Combine(
            Result.Failure(Error.Validation("Email", "Invalid.")),
            Result.Failure<int>(NotFound),
            Result.Failure(conflict));

        Assert.Same(NotFound, result.Error);
    }

    [Fact]
    public void Match_PicksTheBranchMatchingTheOutcome()
    {
        Assert.Equal("ok", Result.Success().Match(() => "ok", error => error.Code));
        Assert.Equal("User.NotFound", Result.Failure(NotFound).Match(() => "ok", error => error.Code));
    }

    [Fact]
    public void Bind_RunsNextOnlyOnSuccess()
    {
        var calls = 0;
        Result Next() { calls++; return Result.Success(); }

        Assert.True(Result.Success().Bind(Next).IsSuccess);
        Assert.Equal(NotFound, Result.Failure(NotFound).Bind(Next).Error);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void BindToValue_PropagatesTheFailure()
    {
        var success = Result.Success().Bind(() => Result.Success(42));
        var failure = Result.Failure(NotFound).Bind(() => Result.Success(42));

        Assert.Equal(42, success.Value);
        Assert.Equal(NotFound, failure.Error);
    }
}
