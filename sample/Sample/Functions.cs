using System.ComponentModel;

namespace Sample;

internal class Functions
{
    public static string GetWeather(WeatherDto dto)
    {
        return $"The weather in {dto.City} on {dto.Date:yyyy-MM-dd} is sunny with a high of 25°C and a low of 15°C.";
    }

    public static string GetCurrentTime()
    {
        return DateTime.UtcNow.ToString("o"); // ISO 8601 format
    }
}

internal class WeatherDto
{
    public required string City { get; set; }

    [Description("The date,default is today date")]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}
