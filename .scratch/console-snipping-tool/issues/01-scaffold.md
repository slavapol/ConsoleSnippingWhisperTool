# 01 Scaffold the solution

Status: resolved
Blocked by: none

Spec: `.scratch/console-snipping-tool/spec.md` → Implementation Decisions / Process shape, Repository.

## Goal

A solution that builds and whose test project runs green, with nothing else in it.

## Scope

- `ConsoleSnippingTool.sln` at the repo root.
- `src/ConsoleSnippingTool/ConsoleSnippingTool.csproj`: `OutputType=WinExe`, `TargetFramework=net10.0-windows`, `UseWindowsForms=true`, `Nullable=enable`, `ImplicitUsings=enable`, `ApplicationHighDpiMode` left unset (the manifest decides), `RootNamespace=ConsoleSnippingTool`. An `app.manifest` declaring Per-Monitor-V2 DPI awareness. Publish properties for a single-file framework-dependent `win-x64` build (`RuntimeIdentifier=win-x64`, `PublishSingleFile=true`, `SelfContained=false`) set so that `dotnet publish -c Release` works without arguments.
- `Program.cs` with an `[STAThread] Main` that does nothing yet beyond `Application.Run` of an empty `ApplicationContext` that exits immediately (keep it trivial; ticket 08 replaces it).
- `tests/ConsoleSnippingTool.Tests/ConsoleSnippingTool.Tests.csproj`: xUnit v3 (`xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, latest stable of each from NuGet), referencing the app project. One placeholder test that passes.
- A .NET `.gitignore` at the root (`dotnet new gitignore`). It must not ignore `.scratch/`.
- No third-party runtime packages in the app project.

## Acceptance

- `dotnet build` and `dotnet test` succeed from the repo root.
- `dotnet publish -c Release src/ConsoleSnippingTool` produces a single `ConsoleSnippingTool.exe` under the publish folder.
- Opening the `.sln` in Visual Studio 2026 shows both projects.

## Comments
- Resolved in 1bd5134 (01: scaffold the solution), fast-forwarded onto integration/console-snipping-tool. Build, test and publish green. Added global.json selecting the Microsoft.Testing.Platform runner because xunit.v3 4.x cannot run under VSTest on the .NET 10 SDK; NoWarn WFO0003 in the app csproj since DPI mode is declared in the manifest by design.
