namespace DeepSeek.Core.Models;
public class Message
{
    public string Content { get; set; } = string.Empty;
    public string Role { get; set; } = "user";


    public static Message NewUserMessage(string content)
    {
        return new Message
        {
            Content = content,
            Role = "user"
        };
    }

    public static Message NewSystemMessage(string content)
    {
        return new Message
        {
            Content = content,
            Role = "system"
        };
    }

    public static Message NewAssistantMessage(string content)
    {
        return new Message
        {
            Content = content,
            Role = "assistant"
        };
    }
}
