---
name: dotnet-csharp
description: Work on this repository's .NET/C# SDK code, including API models, HTTP behavior, Microsoft.Extensions.AI adapters, ASP.NET Core DI, builds, and tests.
metadata:
  short-description: Repository .NET/C# development
---

# Repository .NET/C# Development

Use this skill for implementation, review, debugging, or verification of the C# code in DeepSeekSDK-NET. Read the root `AGENTS.md` first; it is the source of the repository map, release facts, and safety constraints.

## Project boundaries

- Put the reusable API client, request/response models, endpoint behavior, JSON converters, source-generation registrations, and `Microsoft.Extensions.AI` adapters in `src/DeepSeek.Core`.
- Put only ASP.NET Core integration and `HttpClientFactory`/DI registration in `src/DeepSeek.AspNetCore`. Its public extension is `AddDeepSeek(Action<HttpClient>)` and it references Core.
- Keep samples under `sample/`; keep automated checks under `test/DeepSeek.IntegrationTests/`.
- The two library projects must continue to compile for `net8.0`, `net9.0`, and `net10.0`. The test and sample projects currently target `net8.0`.

## Implementation guidance

- Treat public request/response models and serialized JSON as compatibility-sensitive API. Preserve snake_case wire names, null/omission behavior, and the legacy string form of `Message.Content` when adding multimodal content.
- When adding a serializable model or changing a converter, inspect `Models/SourceGenerationContext.cs` and add focused serialization assertions where needed.
- For HTTP, streaming, Files, and adapter behavior, prefer deterministic tests with a custom `HttpMessageHandler`; do not make ordinary tests call DeepSeek over the network.
- Preserve `DeepSeekClient` ownership semantics: the API-key-only constructor owns its `HttpClient`; constructors accepting an external `HttpClient` do not. The ASP.NET Core registration uses `IHttpClientFactory`.
- Keep changes within the appropriate package boundary and avoid unrelated README, package metadata, or version edits during a feature change.

## Verification

From the repository root, use:

```powershell
dotnet build DeepSeek.slnx --configuration Release
dotnet test test\DeepSeek.IntegrationTests\DeepSeek.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName!~DeepSeekLiveApiTests"
```

The test project targets `net8.0`; a machine without the .NET 8 runtime can build successfully but cannot start its testhost. Report that environment limitation instead of treating it as a source failure. Run live tests only when the user explicitly provides authorization and a safe API-key configuration.
