namespace Veredito.Tests;

public class ResultOfTValueTests
{
    private static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found.");

    [Fact]
    public void Success_ExposesTheValue()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Equal(42, result.ValueOrDefault);
    }

    [Fact]
    public void Success_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
    }

    [Fact]
    public void ToString_DescribesTheOutcome()
    {
        Assert.Equal("Success", Result.Success(42).ToString());
        Assert.Equal("Failure(User.NotFound)", Result.Failure<int>(NotFound).ToString());
    }

    [Fact]
    public void ToString_DoesNotExposeTheValue()
    {
        Assert.DoesNotContain("s3cr3t", Result.Success("s3cr3t").ToString());
    }

    [Fact]
    public void Type_IsSealed()
    {
        Assert.True(typeof(Result<int>).IsSealed);
    }

    [Fact]
    public void Failure_AccessingValue_Throws()
    {
        var result = Result.Failure<int>(NotFound);

        var exception = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("User.NotFound", exception.Message);
    }

    [Fact]
    public void Failure_ValueOrDefault_ReturnsDefault()
    {
        Assert.Equal(0, Result.Failure<int>(NotFound).ValueOrDefault);
        Assert.Null(Result.Failure<string>(NotFound).ValueOrDefault);
    }

    [Fact]
    public void Value_ConvertsImplicitlyIntoSuccessfulResult()
    {
        Result<string> result = "veredito";

        Assert.True(result.IsSuccess);
        Assert.Equal("veredito", result.Value);
    }

    [Fact]
    public void NullValue_ConvertsImplicitlyIntoNullValueFailure()
    {
        Result<string> result = (string)null!;

        Assert.True(result.IsFailure);
        Assert.Equal(Error.NullValue, result.Error);
    }

    [Fact]
    public void Error_ConvertsImplicitlyIntoFailedResult()
    {
        Result<int> result = NotFound;

        Assert.True(result.IsFailure);
        Assert.Equal(NotFound, result.Error);
    }

    [Fact]
    public void FailureThroughTheGenericType_ReturnsAResultOfTValue()
    {
        Result<int> result = Result<int>.Failure(NotFound);

        Assert.True(result.IsFailure);
        Assert.Equal(NotFound, result.Error);
    }

    [Fact]
    public void FailureThroughTheGenericType_WithNoneOrNull_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Result<int>.Failure(Error.None));
        Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!));
    }

    [Fact]
    public void Create_ReturnsSuccessForValueAndNullValueFailureForNull()
    {
        Assert.Equal("x", Result.Create("x").Value);
        Assert.Equal(Error.NullValue, Result.Create<string>(null).Error);
    }

    [Fact]
    public void CreateWithError_UsesTheGivenErrorForNull()
    {
        Assert.Equal("x", Result.Create("x", NotFound).Value);
        Assert.Equal(NotFound, Result.Create<string>(null, NotFound).Error);
    }

    [Fact]
    public void TryGetValue_ReturnsTheValueOnlyOnSuccess()
    {
        Assert.True(Result.Success(42).TryGetValue(out var value));
        Assert.Equal(42, value);

        Assert.False(Result.Failure<int>(NotFound).TryGetValue(out _));
    }

    [Fact]
    public void TryGetValueWithError_ReturnsTheErrorOnFailure()
    {
        Assert.True(Result.Success(42).TryGetValue(out var value, out var none));
        Assert.Equal(42, value);
        Assert.Same(Error.None, none);

        Assert.False(Result.Failure<int>(NotFound).TryGetValue(out _, out var error));
        Assert.Equal(NotFound, error);
    }

    [Fact]
    public void ImplicitConversions_WorkAsReturnValues()
    {
        static Result<int> Parse(string input) =>
            int.TryParse(input, out var number)
                ? number
                : Error.Validation("Input.NotANumber", "Not a number.");

        Assert.Equal(7, Parse("7").Value);
        Assert.Equal("Input.NotANumber", Parse("seven").Error.Code);
    }

    [Fact]
    public void Match_PassesTheValueOnSuccess()
    {
        Assert.Equal(84, Result.Success(42).Match(value => value * 2, _ => -1));
        Assert.Equal(-1, Result.Failure<int>(NotFound).Match(value => value * 2, _ => -1));
    }

    [Fact]
    public void Map_TransformsTheValueAndPropagatesTheFailure()
    {
        Assert.Equal("42", Result.Success(42).Map(value => value.ToString()).Value);
        Assert.Equal(NotFound, Result.Failure<int>(NotFound).Map(value => value.ToString()).Error);
    }

    [Fact]
    public void Map_ToNull_BecomesNullValueFailure()
    {
        var result = Result.Success(42).Map(_ => (string?)null);

        Assert.Equal(Error.NullValue, result.Error);
    }

    [Fact]
    public void Bind_ChainsAndShortCircuitsOnFirstFailure()
    {
        var calls = 0;
        Result<int> Double(int value) { calls++; return value * 2; }

        Assert.Equal(84, Result.Success(42).Bind(Double).Value);
        Assert.Equal(NotFound, Result.Failure<int>(NotFound).Bind(Double).Error);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void BindToResult_PropagatesTheFailure()
    {
        var conflict = Error.Conflict("User.Taken", "Taken.");

        Assert.True(Result.Success(42).Bind(_ => Result.Success()).IsSuccess);
        Assert.Equal(conflict, Result.Success(42).Bind(_ => Result.Failure(conflict)).Error);
        Assert.Equal(NotFound, Result.Failure<int>(NotFound).Bind(_ => Result.Success()).Error);
    }

    [Fact]
    public void BindToResult_OnFailure_ReturnsTheSameInstanceWithoutRunningNext()
    {
        var calls = 0;
        var failure = Result.Failure<int>(NotFound);

        var result = failure.Bind(_ => { calls++; return Result.Success(); });

        Assert.Same(failure, result);
        Assert.Equal(0, calls);
    }
}
