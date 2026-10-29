using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Veredito;

BenchmarkSwitcher.FromAssembly(typeof(CreationBenchmarks).Assembly).Run(args);

[MemoryDiagnoser]
public class CreationBenchmarks
{
    private static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found.");

    [Benchmark]
    public Result Success() => Result.Success();

    [Benchmark]
    public Result<int> SuccessWithValue() => Result.Success(42);

    [Benchmark]
    public Result Failure() => Result.Failure(NotFound);

    [Benchmark]
    public Result<int> FailureWithValue() => Result.Failure<int>(NotFound);
}

// Compares a parse → transform → handle flow written with exceptions against the same flow with results.
[MemoryDiagnoser]
public class FlowBenchmarks
{
    private static readonly Error Invalid = Error.Validation("Input.Invalid", "Invalid input.");

    [Params("42", "x")]
    public string Input { get; set; } = "42";

    [Benchmark(Baseline = true)]
    public int WithExceptions()
    {
        try
        {
            return (ParseOrThrow(Input) + 1) * 2;
        }
        catch (FormatException)
        {
            return -1;
        }
    }

    [Benchmark]
    public int WithResults() =>
        Parse(Input)
            .Bind(static value => Result.Success(value + 1))
            .Map(static value => value * 2)
            .Match(static value => value, static _ => -1);

    private static int ParseOrThrow(string input) =>
        int.TryParse(input, out var value) ? value : throw new FormatException("Invalid input.");

    private static Result<int> Parse(string input) =>
        int.TryParse(input, out var value) ? value : Invalid;
}

// How much propagating an existing failure costs, chained and imperative.
[MemoryDiagnoser]
public class FailurePropagationBenchmarks
{
    private static readonly Error NotFound = Error.NotFound("User.NotFound", "User not found.");

    private readonly Result<int> _failure = Result.Failure<int>(NotFound);

    [Benchmark]
    public Result BindToResult() => _failure.Bind(static _ => Result.Success());

    [Benchmark]
    public Result<string> BindToOtherType() => _failure.Bind(static value => Result.Success(value.ToString()));

    [Benchmark]
    public Result<int> Map() => _failure.Map(static value => value + 1);

    [Benchmark]
    public Result Imperative()
    {
        if (_failure.TryGetError(out var error))
            return error;

        return Result.Success();
    }
}

[MemoryDiagnoser]
public class ValidationErrorBenchmarks
{
    private ValidationError _first = null!;
    private ValidationError _second = null!;

    [Params(1, 10, 100)]
    public int Fields { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _first = Create(Fields);
        _second = Create(Fields);
    }

    [Benchmark]
    public bool Equals() => _first == _second;

    [Benchmark]
    public int HashCode() => _first.GetHashCode();

    private static ValidationError Create(int fields) =>
        new(Enumerable.Range(0, fields).Select(i => Error.Validation($"Field{i}", "Invalid.")).ToArray());
}
