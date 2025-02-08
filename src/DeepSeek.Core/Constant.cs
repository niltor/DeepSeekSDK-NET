namespace DeepSeek.Core;

[Obsolete("Use DeepSeekModels instead")]
public class Constant
{
    public class Model
    {
        public const string ChatModel = "deepseek-chat";
        public const string CoderModel = "deepseek-coder";
    }
}

public class DeepSeekModels
{
    public const string ChatModel = "deepseek-chat";
    [Obsolete]
    public const string CoderModel = "deepseek-coder";
    public const string ReasonerModel = "deepseek-reasoner";
}

public class ResponseFormatTypes
{
    public const string Text = "text";
    public const string JsonObject = "json_object";
}
