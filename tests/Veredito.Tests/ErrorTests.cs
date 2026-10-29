namespace Veredito.Tests;

public class ErrorTests
{
    public static TheoryData<Func<string, string, Error>, ErrorType> Factories => new()
    {
        { Error.Failure, ErrorType.Failure },
        { Error.Validation, ErrorType.Validation },
        { Error.NotFound, ErrorType.NotFound },
        { Error.Unauthorized, ErrorType.Unauthorized },
        { Error.Forbidden, ErrorType.Forbidden },
        { Error.Conflict, ErrorType.Conflict },
    };

    [Theory]
    [MemberData(nameof(Factories))]
    public void Factory_SetsCodeMessageAndType(Func<string, string, Error> factory, ErrorType expectedType)
    {
        var error = factory("Some.Code", "Some message.");

        Assert.Equal("Some.Code", error.Code);
        Assert.Equal("Some message.", error.Message);
        Assert.Equal(expectedType, error.Type);
    }

    [Fact]
    public void Errors_WithSameValues_AreEqual()
    {
        Assert.Equal(
            Error.NotFound("User.NotFound", "User not found."),
            Error.NotFound("User.NotFound", "User not found."));
    }

    [Fact]
    public void Constructor_WithNullCodeOrMessage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Error.Failure(null!, "Some message."));
        Assert.Throws<ArgumentNullException>(() => new Error("Some.Code", null!, ErrorType.Failure));
    }

    [Fact]
    public void With_NullCodeOrMessage_Throws()
    {
        var error = Error.Failure("Some.Code", "Some message.");

        Assert.Throws<ArgumentNullException>(() => error with { Code = null! });
        Assert.Throws<ArgumentNullException>(() => error with { Message = null! });
    }

    [Fact]
    public void Constructor_WithEmptyStrings_IsAllowed()
    {
        var error = Error.Failure(string.Empty, string.Empty);

        Assert.Equal(string.Empty, error.Code);
        Assert.Equal(string.Empty, error.Message);
    }

    [Fact]
    public void NullValue_IsAGenericFailure()
    {
        Assert.Equal("Error.NullValue", Error.NullValue.Code);
        Assert.Equal(ErrorType.Failure, Error.NullValue.Type);
    }

    [Fact]
    public void ToString_ShowsCodeAndTypeButNotTheMessage()
    {
        var text = Error.Conflict("User.EmailTaken", "Email joao@exemplo.com is already in use.").ToString();

        Assert.Equal("Error { Code = User.EmailTaken, Type = Conflict }", text);
        Assert.DoesNotContain("joao@exemplo.com", text);
    }

    [Fact]
    public void Is_MatchesOnCodeAndTypeIgnoringTheMessage()
    {
        var expected = Error.NotFound("User.NotFound", "User not found.");

        Assert.True(Error.NotFound("User.NotFound", "User 42 not found.").Is(expected));
        Assert.False(Error.Conflict("User.NotFound", "User not found.").Is(expected));
        Assert.False(Error.NotFound("Order.NotFound", "User not found.").Is(expected));
    }

    [Fact]
    public void Is_DoesNotChangeRecordEquality()
    {
        Assert.NotEqual(
            Error.NotFound("User.NotFound", "User 42 not found."),
            Error.NotFound("User.NotFound", "User not found."));
    }

    [Fact]
    public void Is_WithNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Error.Failure("Some.Code", "Some message.").Is(null!));
    }

    [Fact]
    public void ValidationError_AggregatesFieldErrors()
    {
        var fields = new[]
        {
            Error.Validation("Email", "Email is required."),
            Error.Validation("Name", "Name is too long."),
        };

        var error = new ValidationError(fields);

        Assert.Equal(ValidationError.DefaultCode, error.Code);
        Assert.Equal(ValidationError.DefaultMessage, error.Message);
        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal(fields, error.Errors);
    }

    [Fact]
    public void ValidationError_AcceptsCustomMessage()
    {
        var error = new ValidationError([Error.Validation("Email", "Email is required.")], "Invalid request.");

        Assert.Equal("Invalid request.", error.Message);
    }

    [Fact]
    public void ValidationError_WithEmptyErrors_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ValidationError([]));
        Assert.Throws<ArgumentNullException>(() => new ValidationError(null!));
    }

    [Fact]
    public void ValidationError_CopiesErrors()
    {
        var fields = new List<Error> { Error.Validation("Email", "Email is required.") };

        var error = new ValidationError(fields);
        fields.Add(Error.Validation("Name", "Name is too long."));

        Assert.Single(error.Errors);
    }

    [Fact]
    public void ValidationError_ErrorsCannotBeMutated()
    {
        var error = new ValidationError([Error.Validation("Email", "Email is required.")]);

        var list = Assert.IsAssignableFrom<IList<Error>>(error.Errors);
        Assert.Throws<NotSupportedException>(() => list[0] = Error.Validation("Name", "Name is too long."));
    }

    [Fact]
    public void ValidationError_WithSameErrors_AreEqual()
    {
        var first = new ValidationError([Error.Validation("Email", "Email is required.")]);
        var second = new ValidationError([Error.Validation("Email", "Email is required.")]);

        Assert.Equal(first, second);
        Assert.True(first == second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ValidationError_WithDifferentErrors_AreNotEqual()
    {
        var first = new ValidationError([Error.Validation("Email", "Email is required.")]);
        var second = new ValidationError([Error.Validation("Name", "Name is too long.")]);

        Assert.NotEqual(first, second);
        Assert.NotEqual<Error>(first, Error.Validation(ValidationError.DefaultCode, ValidationError.DefaultMessage));
    }

    [Fact]
    public void ValidationError_ToString_ListsFieldNamesWithoutMessages()
    {
        var error = new ValidationError([
            Error.Validation("Email", "joao@x is not a valid email."),
            Error.Validation("Name", "Name is too long."),
        ]);

        var text = error.ToString();

        Assert.Equal("ValidationError { Code = Validation.Failed, Type = Validation, Errors = [Email, Name] }", text);
        Assert.DoesNotContain("joao@x", text);
    }

    [Fact]
    public void ValidationError_FlattensNestedValidationErrors()
    {
        var inner = new ValidationError([Error.Validation("Email", "Email is required.")]);

        var error = new ValidationError([inner, Error.Validation("Name", "Name is too long.")]);

        Assert.Equal(["Email", "Name"], error.Errors.Select(field => field.Code));
    }

    [Fact]
    public void ValidationError_FlattensSeveralLevels()
    {
        var innermost = new ValidationError([Error.Validation("Email", "Email is required.")]);
        var middle = new ValidationError([innermost, Error.Validation("Name", "Name is too long.")]);

        var error = new ValidationError([middle, Error.Validation("Age", "Must be positive.")]);

        Assert.Equal(["Email", "Name", "Age"], error.Errors.Select(field => field.Code));
        Assert.DoesNotContain(error.Errors, field => field is ValidationError);
    }

    [Fact]
    public void ValidationError_WithNullOrNoneItem_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ValidationError([null!]));
        Assert.Throws<ArgumentException>(() => new ValidationError([Error.None]));
    }

    [Fact]
    public void ValidationError_HashCode_DoesNotAllocate()
    {
        var error = new ValidationError(
            Enumerable.Range(0, 10).Select(i => Error.Validation($"Field{i}", "Invalid.")).ToArray());
        error.GetHashCode();

        var before = GC.GetAllocatedBytesForCurrentThread();
        error.GetHashCode();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ValidationError_ConvertsIntoFailedResult()
    {
        Result<int> result = new ValidationError([Error.Validation("Age", "Must be positive.")]);

        var validation = Assert.IsType<ValidationError>(result.Error);
        Assert.Single(validation.Errors);
    }
}
