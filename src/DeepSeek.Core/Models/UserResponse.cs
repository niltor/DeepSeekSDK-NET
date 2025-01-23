using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace DeepSeek.Core.Models;
public class UserResponse
{
    /// <summary>
    /// has balance
    /// </summary>
    [JsonPropertyName("is_available")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("balance_infos")]
    public List<UserBalance> BalanceInfos { get; set; } = [];
}

public class UserBalance
{
    /// <summary>
    /// CNY or USD
    /// </summary>
    public string Currency { get; set; } = "CNY";

    [JsonPropertyName("total_balance")]
    public string TotalBalance { get; set; } = string.Empty;
    [JsonPropertyName("granted_balance")]
    public string GrantedBalance { get; set; } = string.Empty;

    [JsonPropertyName("topped_up_balance")]
    public string ToppedUpBalance { get; set; } = string.Empty;
}
