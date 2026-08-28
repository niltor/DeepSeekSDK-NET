# DeepSeekSDK-NET Agent Guide

## 适用范围

本文件适用于整个仓库。开始修改前先确认工作区状态，并根据任务读取对应的仓库技能：

- 一般 .NET/C# 开发、API 模型、序列化、适配器、构建和测试：读取 `.agents/skills/dotnet-csharp/SKILL.md`。
- NuGet 打包、版本变更或发布新版本：读取 `.agents/skills/pack-release/SKILL.md`。

不要把构建产物、NuGet 密钥、DeepSeek API Key 或本地用户配置提交到仓库。除非用户明确要求，不要运行真实 API 集成测试，也不要向 NuGet.org 推送包。

## 项目定位

DeepSeekSDK-NET 是面向 .NET 应用的 DeepSeek API SDK，提供 HTTP 客户端、Chat Completions、Completions、OpenAI 兼容的 Responses API、流式 SSE、函数调用、视觉内容、Files API、余额/模型查询，以及 `Microsoft.Extensions.AI` 的 `IChatClient` 适配。

仓库发布两个 NuGet 库：

| 项目 | NuGet 包 | 作用 | 目标框架 | 主要包依赖 |
| --- | --- | --- | --- | --- |
| `src/DeepSeek.Core/DeepSeek.Core.csproj` | `Ater.DeepSeek.Core` | 核心 HTTP 客户端、请求/响应模型、序列化和 `IChatClient` 适配器 | `net8.0;net9.0;net10.0` | `Microsoft.Extensions.AI.Abstractions` `10.1.1` |
| `src/DeepSeek.AspNetCore/DeepSeek.AspNetCore.csproj` | `Ater.DeepSeek.AspNetCore` | ASP.NET Core 的 `HttpClientFactory`/DI 扩展，依赖 Core | `net8.0;net9.0;net10.0` | `Microsoft.Extensions.Http`、`Microsoft.Extensions.DependencyInjection` `10.0.1` |

README 当前仍以 .NET 8 为使用要求进行说明，而项目文件已经多目标到 .NET 8、9、10。修改支持版本时要同时核对项目文件、CI 和文档，不要只根据 README 做推断。

## 核心结构

- `src/DeepSeek.Core/DeepSeekClient.cs`：核心客户端和 endpoint 配置；默认使用 `https://api.deepseek.com/`，覆盖模型列表、聊天、Responses、Completions、Files 和余额接口。
- `src/DeepSeek.Core/Models/`：请求、响应及视觉/Files/Responses 数据模型。`Message` 通过自定义 JSON converter 同时保持旧的字符串 `content` 兼容性和多模态内容块数组格式。
- `src/DeepSeek.Core/Adapters/DeepSeekChatClient.cs`：把 `Microsoft.Extensions.AI` 映射到 Chat Completions。
- `src/DeepSeek.Core/Adapters/DeepSeekResponsesChatClient.cs`：把 `Microsoft.Extensions.AI` 映射到 Responses API。
- `src/DeepSeek.Core/Models/SourceGenerationContext.cs`：`System.Text.Json` source-generation 类型注册。新增可序列化的公开模型时检查是否需要补充注册。
- `src/DeepSeek.AspNetCore/Extension.cs`：`AddDeepSeek(Action<HttpClient>)` 注册方式；它通过 `IHttpClientFactory` 创建客户端。
- `test/DeepSeek.IntegrationTests/`：xUnit 测试。Responses、工具调用、视觉和 Files API 测试使用自定义 `HttpMessageHandler`，不应改成依赖网络的测试。
- `test/DeepSeek.IntegrationTests/DeepSeekLiveApiTests.cs`：需要真实 API Key 的在线测试，默认从 `DEEPSEEK_API_KEY` 或 user-secrets 读取。
- `sample/Sample/` 和 `sample/AspNetCoreSample/`：控制台与 ASP.NET Core 使用示例；它们是可构建的示例，不是发布包项目。
- `.github/workflows/build.yml`：主分支上的构建、测试、打包和 NuGet 发布流水线。
- `pack.ps1`：本地同时打包两个库的简易脚本；`pack/` 中的 `.nupkg` 是生成物，已被 `.gitignore` 忽略。

## 日常开发与验证

从仓库根目录执行：

```powershell
dotnet restore DeepSeek.slnx
dotnet build DeepSeek.slnx --configuration Release
dotnet test test\DeepSeek.IntegrationTests\DeepSeek.IntegrationTests.csproj --configuration Release --filter "FullyQualifiedName!~DeepSeekLiveApiTests"
```

本地测试项目目标为 `net8.0`，因此运行测试的机器必须有 .NET 8 runtime；只有 .NET 10 SDK/ runtime 时，编译可以成功，但 testhost 仍可能因缺少 .NET 8 runtime 而无法启动。优先安装所需 runtime，不要为了掩盖环境问题修改目标框架。

在线测试需要显式配置凭据，例如使用环境变量 `DEEPSEEK_API_KEY`，或按测试 fixture 中的说明设置 user-secrets。不要把 key 写入 `appsettings.json`、命令输出、提交内容或包中。

修改公共模型、JSON 命名、converter、SSE 解析、函数调用、Files/视觉映射或 `IChatClient` 行为时，应增加或更新对应的序列化/HTTP handler 测试，并至少执行 Release 构建和非在线测试。保持三个目标框架都能编译；不要直接使用 .NET 9/10 专属 API，除非代码有明确的条件化实现。

`DeepSeekClient(string apiKey)` 创建并拥有自己的 `HttpClient`；传入 `HttpClient` 的构造函数以及 ASP.NET Core DI 路径不拥有该实例。涉及生命周期时必须保留这一边界。

## 版本与发布概览

包版本来自两个可打包项目中的 `<Version>`，当前值均为 `1.6.0`：

- `src/DeepSeek.Core/DeepSeek.Core.csproj`：`PackageId=Ater.DeepSeek.Core`。
- `src/DeepSeek.AspNetCore/DeepSeek.AspNetCore.csproj`：`PackageId=Ater.DeepSeek.AspNetCore`。

Core 项目还显式保留 `AssemblyVersion=0.1.0.0` 和 `FileVersion=0.1.0.0`；它们不是当前 NuGet 包版本。除非用户明确要求改变程序集兼容策略，否则发包时只同步两个项目的 `<Version>`，不要顺手修改这两个字段。

本地打包：

```powershell
.\pack.ps1
```

该脚本把两个包写入仓库根目录的 `pack/`。CI 使用等价的 Release/no-build/no-restore `dotnet pack` 命令，将两个项目生成的 `.nupkg` 写入 `./pack`，随后逐个推送其中的包文件。

GitHub Actions 的实际发布条件和顺序如下：

1. push 到 `main`，或在 `main` 上手动触发 workflow。
2. 使用 .NET 10，restore/build，并运行排除 `DeepSeekLiveApiTests` 的测试。
3. 打包 `Ater.DeepSeek.AspNetCore` 和 `Ater.DeepSeek.Core`。
4. 使用 `NuGet/login@v1`、OIDC 和仓库 secret `NUGET_USER` 获取短期 NuGet API key。
5. 对 `./pack/*.nupkg` 执行 `dotnet nuget push`，源为 NuGet.org，并使用 `--skip-duplicate`。

该流水线不是 tag 流水线：现有 tag 同时存在 `v1.4.1` 与 `1.5.0` 等命名，tag 不会决定包版本，也不会单独触发发布。本项目当前发版约定是在合并到 `main` 后创建与包版本完全相同的 tag（无 `v` 前缀）、推送 tag，最后回到 `dev`；tag 仅作为版本标记。新版本发布前必须把两个项目的 `<Version>` 改成完全相同的值。

发版实际只需同步两个包项目的 `<Version>` 与 `PackageReleaseNotes`，再通过正常流程合并到 `main`；其余 restore、测试、打包和 NuGet 推送由 `.github/workflows/build.yml` 自动完成。需要执行该流程时使用 `.agents/skills/pack-release/SKILL.md`，不要只修改一个 `.csproj`。
