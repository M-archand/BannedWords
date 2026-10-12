using CounterStrikeSharp.API.Core;
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
        public bool EnableReason { get; set; } = true;

        [JsonPropertyName("PrintToPlayerChat")]
        public bool PrintToPlayerChat { get; set; } = true;

        [JsonPropertyName("PrintToAllChat")]
        public bool PrintToAllChat { get; set; } = false;

        [JsonPropertyName("BannedWords")]
        public string[] BannedWords { get; set; } = [];

        [JsonIgnore]
        public Regex[] CompiledBannedWords { get; set; } = [];

        [JsonIgnore]
        public string? ValidatedBanType { get; set; }

        [JsonIgnore]
        public string ValidatedDuration { get; set; } = string.Empty;
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
                BannedWords = ["word1", "word2", "word3"]
            },
            new BanSettingsGroup
            {
                PlayerBanType = "gag",
                DurationInMinutes = "10",
                EnableReason = true,
                PrintToPlayerChat = true,
                PrintToAllChat = true,
                BannedWords = ["word4", "word5", "word6"]
            }
        };

        [JsonPropertyName("ReloadPermission")]
        public string ReloadPermission { get; set; } = "@css/generic";

        [JsonPropertyName("WhitelistPermission")]
        public string WhitelistPermission { get; set; } = "@css/generic";

        [JsonPropertyName("ExcludeStartsWith")]
        public string ExcludeStartsWith { get; set; } = "!/.";

        // Applies RegexOptions.IgnoreCase to every pattern. Use inline (?-i) for a case-sensitive pattern.
        [JsonPropertyName("IgnoreCase")]
        public bool IgnoreCase { get; set; } = true;

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
