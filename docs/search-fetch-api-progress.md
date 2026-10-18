# DeepSeek Search and Fetch API

版本：1.6.2。状态：已完成 SDK 实现、离线验证与 Search/Fetch 真实接口验证；本地发版验证进行中（2026-10-18）。

## 计划与范围

1. 以 DeepSeek 官方文档核对接口路径、鉴权、请求/响应字段、错误行为和模型更新。
2. 在 `DeepSeekClient` 中增加两个独立的异步方法及公开请求/响应模型。
3. 使用伪 HTTP handler 验证 JSON、路径、Bearer 鉴权和响应反序列化。
4. 更新中英文使用文档并记录验证边界。
5. 按用户要求增加并尝试运行真实 Search/Fetch 测试，只选择这两个新接口。

本次范围只覆盖 DeepSeek 独立的原始 Search/Fetch API，不把它们接入 OpenAI Responses、工具调用或
`Microsoft.Extensions.AI` 适配层。

## 已确认的官方契约

- Search：`POST /v1/tools/search`，请求包含 `query`，可选 `count`（1-50）、`ori_question`、
  `localization` 和 `need_content`；响应为 `results` 数组，每项包含标题、URL、摘要、内容、
  RFC 3339 发布时间、权威等级和重排分数。
- Fetch：`POST /v1/tools/fetch`，请求包含 HTTP/HTTPS `url`（最多 8192 个字符，不能带用户信息）；
  响应包含 `title` 和 Markdown `content`。
- 两个接口使用 `Authorization: Bearer <key>` 与 `application/json`，错误采用 OpenAI 风格的
  `error` 对象；客户端沿用本 SDK 的失败处理，返回 `null` 并设置 `ErrorMsg`。
- 2026-10-17 的模型更新将 API 服务升级到 DeepSeek-V4.1-Flash-1017，但调用 ID 仍为
  `deepseek-flash`，因此本次不修改模型常量。

官方资料：

- [Search and Fetch 指南](https://api-docs.deepseek.com/guides/search/)
- [Search API Reference](https://api-docs.deepseek.com/api/search/)
- [Fetch API Reference](https://api-docs.deepseek.com/api/fetch/)
- [DeepSeek Change Log](https://api-docs.deepseek.com/updates/)

## 完成情况与边界

- `SearchRequest`、`SearchResponse`、`SearchResult`、`FetchRequest` 和 `FetchResponse` 已加入 Core
  模型与 source-generation 注册。
- `DeepSeekClient.SearchAsync` 和 `DeepSeekClient.FetchAsync` 已使用独立 endpoint，并保留外部
  `HttpClient` 生命周期与取消令牌行为；endpoint 可按现有客户端方式覆盖，便于本地网关和测试。
- 修复了 Search 的 `Query` 和非 null `OriginalQuestion` 缺少 128 Unicode 字符上限校验的问题，
  使用 Unicode scalar 计数，避免把 emoji 的 UTF-16 代理对误算为两个字符。
- 离线测试覆盖请求路径、JSON、鉴权、反序列化、空搜索结果、可选字段省略、数量边界、Unicode
  长度边界、非法 URL/null 请求、错误保留、空响应、自定义 endpoint 及取消传播。
- 当前 Release 构建通过（net8.0/net9.0/net10.0，0 警告、0 错误）；非在线测试 **75/75 通过**。
- 在线测试增加在 `DeepSeekLiveApiTests`，使用 `Category=SearchFetchLive` 单独筛选，保留 CI 的
  在线排除规则。Search 验证非空结果、HTTP URL、分数和权威等级；Fetch 验证可用正文。
- 2026-10-18 已使用本机用户环境变量 `DEEPSEEK_API_KEY` 实际调用线上接口：**2/2 通过**。
  Search 返回符合断言的结果，Fetch 返回包含 DeepSeek 文本的 Markdown 正文。
- 使用命令：
  `dotnet test test/DeepSeek.IntegrationTests/DeepSeek.IntegrationTests.csproj --configuration Release --no-restore --filter "Category=SearchFetchLive"`
  凭据只保存于当前 Windows 用户环境变量及进程环境中，未写入仓库或本文件。

## 审查后修复

- Search/Fetch 每次通过请求参数校验后清空 `ErrorMsg`，避免先失败后成功时仍暴露旧错误。
  回归测试同时覆盖同一接口重试与两个接口之间的连续调用。
- 响应字段按官方必填约束使用 `JsonRequired`，并检查列表、列表元素和非 nullable 字符串的 null。
  JSON 损坏、类型错误、缺少字段、顶层 `null` 和异常 null 数据统一返回 `null` 并写入新的诊断信息。
  只处理响应反序列化的 `JsonException`，取消和网络异常继续传播。
- 保留合法空搜索结果、空字符串字段、允许为 null 的站点/日期元数据，以及未知扩展字段兼容性。
- 原有接口使用保留调用方 `BaseAddress` 的相对路径；仅 Completions 在默认根地址下补 `beta/`。
  Search/Fetch 地址逻辑本次保留：默认根地址正确，自定义 `/v1/` 基础地址需要通过
  `SetSearchEndpoint("tools/search")` 和 `SetFetchEndpoint("tools/fetch")` 避免重复拼接。
- 按用户要求，Fetch URL 的 UTF-16 长度计数暂不调整。

## 本机真实验证

在本机为测试项目配置 user-secrets 的 `apiKey`（占位符须替换为自己的 Key，请勿提交或发送到聊天）：

```powershell
dotnet user-secrets set "apiKey" "<your-key>" --project test/DeepSeek.IntegrationTests/DeepSeek.IntegrationTests.csproj
dotnet test test/DeepSeek.IntegrationTests/DeepSeek.IntegrationTests.csproj --configuration Release --filter "Category=SearchFetchLive"
```

也可使用当前进程可见的 `DEEPSEEK_API_KEY`。在线用例分别请求搜索和文档首页抓取，不运行其他
模型/Files 在线测试；真实请求按服务计费，站点可抓取性仍取决于目标站点。
