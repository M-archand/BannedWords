﻿using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BannedWords
{
    public class BanSettingsGroup
    {
        [JsonPropertyName("PlayerBanType")]
        public string? PlayerBanType { get; set; }

        [JsonPropertyName("DurationInMinutes")]
        public string? DurationInMinutes { get; set; }

        [JsonPropertyName("EnableReason")]
        public required bool EnableReason { get; set; }

        [JsonPropertyName("PrintToPlayerChat")]
        public required bool PrintToPlayerChat { get; set; }

        [JsonPropertyName("PrintToAllChat")]
        public required bool PrintToAllChat { get; set; }

        [JsonPropertyName("BannedWords")]
        public required string[] BannedWords { get; set; }

        [JsonIgnore]
        public Regex[] CompiledBannedWords { get; set; } = [];
    }

    public class BannedWordsConfig : BasePluginConfig
    {
        [JsonPropertyName("BanSettingsGroups")]
        public List<BanSettingsGroup> BanSettingsGroups { get; set; } = new List<BanSettingsGroup>
        {
            new BanSettingsGroup
            {
                PlayerBanType = "silence",
                DurationInMinutes = "5",
                EnableReason = true,
                PrintToPlayerChat = true,
                PrintToAllChat = true,
                BannedWords = ["(?i)word1", "(?i)word2", "(?i)word3"]
            },
            new BanSettingsGroup
            {
                PlayerBanType = "gag",
                DurationInMinutes = "10",
                EnableReason = true,
                PrintToPlayerChat = true,
                PrintToAllChat = true,
                BannedWords = ["(?i)word4", "(?i)word5", "(?i)word6"]
            }
        };

        [JsonPropertyName("ReloadPermission")]
        public string ReloadPermission { get; set; } = "@css/admin";

        [JsonPropertyName("WhitelistPermission")]
        public string WhitelistPermission { get; set; } = "@css/admin";

        [JsonPropertyName("ExcludeStartsWith")]
        public string ExcludeStartsWith { get; set; } = "!/.";

        [JsonIgnore]
        public HashSet<char> ExcludedStartCharacters { get; set; } = [];
        
        [JsonPropertyName("ConfigVersion")]
        public override int Version { get; set; } = BannedWords.ExpectedConfigVersion;
    }

    public partial class BannedWords : BasePlugin, IPluginConfig<BannedWordsConfig>
    {
        public required BannedWordsConfig Config { get; set; }
    }
}
