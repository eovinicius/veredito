# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Veredito is a lightweight, **dependency-free** Result pattern library for .NET, published as the NuGet package `Veredito`. Keep the core package free of external dependencies.

## Commands

The solution uses the new `.slnx` format; `global.json` pins the .NET 10 SDK line (`rollForward: latestFeature`).

```sh
dotnet build                                   # builds src and tests (net8.0 + net10.0)
dotnet test                                    # runs all xUnit tests on net8.0 and net10.0 (needs both runtimes)
dotnet test -f net10.0                         # without the .NET 8 runtime installed
dotnet test --filter "FullyQualifiedName~ResultTests"                          # one test class
dotnet test --filter "FullyQualifiedName=Veredito.Tests.ResultTests.Bind_RunsNextOnlyOnSuccess"  # one test
dotnet pack src/Veredito -c Release            # produces .nupkg + .snupkg
dotnet run -c Release --project benchmarks/Veredito.Benchmarks -- --filter "*"  # BenchmarkDotNet (add --job short for a quick run)
```

`benchmarks/Veredito.Benchmarks` is the only project allowed to take a third-party dependency (BenchmarkDotNet); it is not packed and not run by `dotnet test`.

There is no separate linter: `TreatWarningsAsErrors` is on for every project, and `GenerateDocumentationFile` is on for `src/`, so **every public member needs an XML doc comment** or the build fails (CS1591).

CI (`.github/workflows/ci.yml`, GitHub Actions) runs restore → build (Release) → test on both TFMs → pack on every push to `main` and every pull request, with `permissions: contents: read` and third-party actions pinned by commit SHA (version in a comment; bump both together).

## Build configuration

- `Directory.Build.props` (root) — applies to all projects: `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`.
- `src/Directory.Build.props` — imports the root one and adds packaging metadata shared by **every package under `src/`**: target frameworks (`net8.0;net10.0`, supported LTS only), `VersionPrefix`, license, tags, symbols, `IsAotCompatible` (no reflection — the trim/AOT analyzers enforce it), and packs the root `README.md` as the package readme. It also enables SourceLink metadata (`PublishRepositoryUrl`, `EmbedUntrackedSources`; SourceLink ships with the SDK), `ContinuousIntegrationBuild` when `CI=true` (normalized PDB paths), and `EnablePackageValidation`. API-break detection uses the SDK's package validation, **not** `PublicApiAnalyzers`, which would be a third-party dependency under `src/`. Individual `.csproj` files only set `PackageId` and `Description`. New packages (e.g. integrations) go under `src/` and inherit this.
- Tests target `net8.0;net10.0`, the same TFMs as the package, and compile with each TFM's default C# version (C# 12 on `net8.0`).

## Release checklist

Publishing is done by `.github/workflows/release.yml` when a `v*` tag is pushed: it tests, packs with the tag as the version (`v0.1.0` → `0.1.0`) and pushes to nuget.org through Trusted Publishing (OIDC via `NuGet/login`, user `eovinicius`; no API key is stored). The nuget.org policy must match owner `eovinicius`, repository `veredito`, workflow `release.yml` — renaming the file breaks publishing. A published version can only be unlisted, never deleted.

1. Check that CI is green on the commit being released.
2. Bump `VersionPrefix` to the version being released, so local builds match.
3. `git tag vX.Y.Z && git push origin vX.Y.Z`, then follow the Release run.
4. After the first version is published, set `PackageValidationBaselineVersion` to it so `dotnet pack` fails on undeclared API breaks; keep it at the latest published version afterwards.

## Architecture

All types live in the `Veredito` namespace in `src/Veredito/`:

- `Error` — a non-sealed `record (Code, Message, ErrorType)` with factory methods per `ErrorType`. `Error.None` marks success; `Error.NullValue` (an `ErrorType.Failure`) is produced when `null` is converted into a `Result<T>`. It's non-sealed only so `ValidationError` can derive from it. `Code`/`Message` are declared explicitly over `readonly` fields so both the constructor and `with` reject `null` (no `field` keyword: `net8.0` compiles with C# 12); empty strings stay legal because `Error.None` uses them. `Is(other)` compares `Code` (ordinal) and `Type` only; `==` still compares every member. `PrintMembers` prints `Code` and `Type`, never `Message`.
- `ValidationError` — sealed record deriving from `Error` with fixed code `Validation.Failed`; aggregates per-field errors where each inner `Error.Code` is the field name. Requires at least one error, rejects `null` and `Error.None` items, flattens nested `ValidationError`s (one level suffices, since each is flat by construction), and keeps the copy in a private array. Overrides `Equals`/`GetHashCode` (allocation-free) so equality covers the list contents, and `PrintMembers` so `ToString` lists only field names.
- `ErrorType` — the category; the doc comment anticipates integrations (e.g. HTTP) mapping it to their own vocabulary such as status codes.
- `Result` / `Result<TValue>` (both in `Result.cs`) — `Result<TValue>` is `sealed` and derives from `Result`, whose constructor is `private protected`: only the library can create results, so the invariants can't be bypassed by subclassing. Key invariants enforced by the constructor: success ⇔ `Error` is `Error.None` **by reference** (violations throw `InvalidOperationException`; a `null` error throws `ArgumentNullException`). `Result.Success<T>(null)` throws — `Create` is the null-tolerant path. `Result.Success()` returns a shared cached instance. `Value` throws on failure (message includes the error code); `ValueOrDefault` never throws; `TryGetValue(out value, out error)` unwraps both; `TryGetError(out error)` is the one-line unwrap for a valueless `Result` (`true` on failure, `Error.None` on success). `Result<TValue>` hides the inherited `Failure(Error)` with a `new static` one returning `Result<TValue>` — inside `Result<TValue>`, an unqualified `Failure(error)` now binds to it. `Result.Combine(params Result[])` returns the first non-validation failure, or merges all validation failures into one `ValidationError`.
- `ResultExtensions` — combinators that must return the receiver's own type (`Ensure`, `Tap`, `TapError`, `MapError`). They're extensions, not instance methods on `Result`, because an instance method there would return a plain `Result` when called on a `Result<T>` and lose the value. It also holds `[Obsolete(error: true)]` + `[EditorBrowsable(Never)]` overloads of `Tap`/`TapError` taking `Func<…, Task>`: they exist only so an `async` delegate binds to them and fails to compile, instead of binding to the `Action` overload as `async void`. Don't remove them.
- `ResultAsyncExtensions` — `*Async` versions of the composition API, on a result (async delegate) and on `Task<Result>` / `Task<Result<T>>` (sync or async delegate; `MapErrorAsync` is sync-only). `TapAsync`/`TapErrorAsync` take `Func<…, Task>` and await it. Always `ConfigureAwait(false)`; `Task` only, no `ValueTask`. Sync-vs-async delegate overloads rely on C#'s "more specific" / better-conversion tie-break (a delegate with a return type beats a `void` one for an `async` lambda), covered by `ResultAsyncExtensionsTests`.

Design decisions to preserve:

- Implicit conversions: `Error → Result`, `Error → Result<T>`, `T → Result<T>`. The `T → Result<T>` parameter is annotated non-nullable on purpose, so converting a maybe-`null` value warns (CS8604) and steers callers to `Result.Create(value, error)`; a `null` that still arrives yields `Error.NullValue` (via `Result.Create`). C# disallows user-defined conversions from interfaces, so when the expression's static type is an interface callers must use `Result.Success(value)`.
- `Map` goes through `Create`, so a `null` mapper output becomes a `NullValue` failure.
- Composition API is `Match`, `Map`, `Bind`, `Ensure`, `Tap`, `TapError`, `MapError` plus their `*Async` counterparts; failures propagate by re-wrapping `Error` in the target result type, and combinators whose target type the receiver already is (including `Result<T>.Bind` to a plain `Result`) return the same instance (no allocation).
- `ToString()` never prints the value (`Success` / `Failure(Code)`) so logs can't leak it; `Result<T>` shows the value only through `[DebuggerDisplay]`. Likewise `Error.ToString()` never prints `Message`, and `ValidationError.ToString()` prints only field names.

## Conventions

- Tests: xUnit, `Assert.*` only (no assertion library), named `Member_ExpectedBehavior` (e.g. `Failure_WithErrorNone_Throws`), one test class per public type (`ResultTests`, `ResultOfTValueTests`, `ErrorTests`, `ResultExtensionsTests`, `ResultAsyncExtensionsTests`).
- When changing public behavior, keep `README.md` in sync — it's shipped as the NuGet package readme. That includes the **Pitfalls** and **Testing** sections, not just the API walkthrough. The README leads with the early-return style (`if (x.IsFailure) return x.Error;`, `TryGetValue`, `TryGetError`, `Result.Combine`); fluent combinators are presented as optional. Its snippets must compile on `net8.0` with nullable enabled.
