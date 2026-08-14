namespace DeepSeek.Core;

public class DeepSeekModels
{
    public const string Pro = "deepseek-v4-pro";
    public const string Flash = "deepseek-v4-flash";
}

public class ResponseFormatTypes
{
    public const string Text = "text";
    public const string JsonObject = "json_object";
}

public class ThinkingTypes
{
    public const string Enabled = "enabled";
    public const string Disabled = "disabled";
}

public class ReasoningEffortTypes
{
    public const string None = "none";
    public const string High = "high";
    public const string Max = "max";
}

public class ResponseTextFormatTypes
{
    public const string Text = "text";
    public const string JsonObject = "json_object";
    public const string JsonSchema = "json_schema";
}

public class ResponseToolTypes
{
    public const string Function = "function";
    public const string WebSearch = "web_search";
    public const string WebSearch20250826 = "web_search_2025_08_26";
}

public class ResponseInputItemTypes
{
    public const string Message = "message";
    public const string FunctionCall = "function_call";
    public const string FunctionCallOutput = "function_call_output";
    public const string Reasoning = "reasoning";
    public const string WebSearchCall = "web_search_call";
}

public class ResponseContentPartTypes
{
    public const string InputText = "input_text";
    public const string OutputText = "output_text";
    public const string ReasoningText = "reasoning_text";
}

public class ResponseEventTypes
{
    public const string Completed = "response.completed";
    public const string Incomplete = "response.incomplete";
    public const string Failed = "response.failed";
    public const string OutputTextDelta = "response.output_text.delta";
    public const string ReasoningTextDelta = "response.reasoning_text.delta";
    public const string FunctionCallArgumentsDelta = "response.function_call_arguments.delta";
    public const string FunctionCallArgumentsDone = "response.function_call_arguments.done";
    public const string OutputItemDone = "response.output_item.done";
}
