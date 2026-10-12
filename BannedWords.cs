using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using System.Collections.Frozen;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace BannedWords
{
    [MinimumApiVersion(80)]
    public partial class BannedWords : BasePlugin, IPluginConfig<BannedWordsConfig>
    {
        internal const int ExpectedConfigVersion = 1;

        public override string ModuleName => "Banned Words";
        public override string ModuleAuthor => "Marchand";
        public override string ModuleVersion => "1.0.0";

        public override void Load(bool hotReload)
        {
            // Global listener on purpose: named "say"/"say_team" listeners stopped all filtering during in-game test
            AddCommandListener(null!, OnAnyCommandTrace, HookMode.Pre);
            AddCommand("css_reloadbannedwords", "Reloads the BannedWords config.", ReloadConfigCommand);
        }

        public void OnConfigParsed(BannedWordsConfig config)
        {
            Config = config;
            PrepareRuntimeConfig(Config);

            if (Config.Version != ExpectedConfigVersion)
            {
                Logger.LogWarning(
                    "Config version mismatch detected: expected {ExpectedConfigVersion}, but got {ConfigVersion}.",
                    ExpectedConfigVersion,
                    Config.Version
                );
            }
        }

        private static string ExtractChatMessage(CommandInfo message)
        {
            string commandString = message.GetCommandString;
            if (!string.IsNullOrWhiteSpace(commandString))
            {
                int firstSpace = commandString.IndexOf(' ');
                if (firstSpace >= 0 && firstSpace < commandString.Length - 1)
                {
                    return commandString[(firstSpace + 1)..].Trim().Trim('"');
                }
            }

            string argString = message.ArgString?.Trim().Trim('"').Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(argString))
            {
                return argString;
            }

            return message.GetArg(1).Trim();
        }

        private HookResult OnAnyCommandTrace(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.IsBot)
            {
                return HookResult.Continue;
            }

            string command = info.GetArg(0).Trim().ToLowerInvariant();
            if (command is not ("say" or "say_team"))
            {
                return HookResult.Continue;
            }

            return HandlePlayerChat(player, info);
        }

        private string NormalizeRegexPattern(string pattern, string? playerBanType)
        {
            if (!pattern.Contains('\b'))
            {
                return pattern;
            }

            string normalizedPattern = pattern.Replace("\b", "\\b");

            Logger.LogInformation(
                "Normalized JSON backspace escape(s) in banned word regex for PlayerBanType '{PlayerBanType}'. Use '\\\\b' in JSON config for regex word boundaries.",
                playerBanType
            );

            return normalizedPattern;
        }

        // banreason from lang/ folder, cleaned for use inside a console command
        private string _banReason = string.Empty;

        private void PrepareRuntimeConfig(BannedWordsConfig config)
        {
            config.ExcludedStartCharacters = [.. (config.ExcludeStartsWith ?? string.Empty).Distinct()];
            _banReason = SanitizeConsoleArgument(Localizer[LocalizerKeys.BanReason], LocalizerKeys.BanReason);

            foreach (var group in config.BanSettingsGroups)
            {
                var compiledPatterns = new List<Regex>();

                foreach (var pattern in group.BannedWords)
                {
                    if (string.IsNullOrWhiteSpace(pattern))
                    {
                        Logger.LogWarning("Skipping empty banned word regex for PlayerBanType '{PlayerBanType}'.", group.PlayerBanType);
                        continue;
                    }

                    try
                    {
                        string normalizedPattern = NormalizeRegexPattern(pattern, group.PlayerBanType);
                        compiledPatterns.Add(new Regex(normalizedPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant));
                    }
                    catch (ArgumentException ex)
                    {
                        Logger.LogWarning(
                            ex,
                            "Skipping invalid banned word regex '{Pattern}' for PlayerBanType '{PlayerBanType}'.",
                            pattern,
                            group.PlayerBanType
                        );
                    }
                }

                group.CompiledBannedWords = [.. compiledPatterns];

                if (group.CompiledBannedWords.Length == 0)
                {
                    Logger.LogWarning("No usable banned word regex for PlayerBanType '{PlayerBanType}'. This group will never match.", group.PlayerBanType);
                }

                ValidateBanSettings(group);

                if (group.ValidatedBanType == null)
                {
                    WarnIfMissingLangKey(LocalizerKeys.BanInfo);
                }
            }

            Logger.LogInformation(
                "Prepared {CompiledPatternCount} regex pattern(s) across {GroupCount} group(s).",
                config.BanSettingsGroups.Sum(group => group.CompiledBannedWords.Length),
                config.BanSettingsGroups.Count
            );
        }

        // Plain minutes ("10") or SimpleAdmin time units ("1h", "1h30m")
        private static readonly Regex DurationPattern = new(@"^(\d+|(\d+(mo|m|h|d|w|y))+)$", RegexOptions.CultureInvariant);

        private void ValidateBanSettings(BanSettingsGroup group)
        {
            group.ValidatedBanType = null;
            group.ValidatedDuration = string.Empty;

            string playerBanType = group.PlayerBanType?.Trim().ToLowerInvariant() ?? string.Empty;
            string duration = group.DurationInMinutes?.Trim() ?? string.Empty;

            if (playerBanType.Length == 0)
            {
                return;
            }

            if (!ValidBanTypes.Contains(playerBanType))
            {
                Logger.LogWarning("Invalid PlayerBanType '{PlayerBanType}'. Matching messages will be blocked without punishment.", group.PlayerBanType);
                return;
            }

            if (playerBanType == "kick")
            {
                duration = string.Empty; // kick takes no time arg
            }
            else if (duration.Length == 0)
            {
                Logger.LogWarning("DurationInMinutes is missing for PlayerBanType '{PlayerBanType}'. Falling back to 1 minute, add DurationInMinutes to the config.", group.PlayerBanType);
                duration = "1";
            }
            else if (!DurationPattern.IsMatch(duration))
            {
                Logger.LogWarning(
                    "Invalid DurationInMinutes '{DurationInMinutes}' for PlayerBanType '{PlayerBanType}'. Use a number of minutes such as \"10\". Matching messages will be blocked without punishment.",
                    group.DurationInMinutes,
                    group.PlayerBanType
                );
                return;
            }

            if (!group.PrintToPlayerChat && !group.PrintToAllChat)
            {
                Logger.LogWarning("Both PrintToPlayerChat and PrintToAllChat are disabled for PlayerBanType '{PlayerBanType}'. No messages will be sent.", group.PlayerBanType);
            }

            if (group.PrintToPlayerChat)
            {
                WarnIfMissingLangKey($"{playerBanType}msgplayer");
            }

            if (group.PrintToAllChat)
            {
                WarnIfMissingLangKey($"{playerBanType}msgserver");
            }

            if (group.EnableReason)
            {
                WarnIfMissingLangKey(LocalizerKeys.BanReason);
            }

            group.ValidatedBanType = playerBanType;
            group.ValidatedDuration = duration;
        }

        // In console ';'  and '"' will fuck shit up
        private string SanitizeConsoleArgument(string value, string langKey)
        {
            string cleaned = new(value.Where(c => c is not (';' or '"') && !char.IsControl(c)).ToArray());

            if (cleaned.Length != value.Length)
            {
                Logger.LogWarning(
                    "Lang key '{LangKey}' contains ';', '\"' or control characters. They were removed; using '{Value}'.",
                    langKey,
                    cleaned
                );
            }

            return cleaned;
        }

        private void WarnIfMissingLangKey(string key)
        {
            if (Localizer[key].ResourceNotFound)
            {
                Logger.LogWarning("Missing lang key '{LangKey}'. Chat will show the key name instead of a message.", key);
            }
        }

        public void ReloadConfigCommand(CCSPlayerController? player, CommandInfo command)
        {
            if (player != null && !AdminManager.PlayerHasPermissions(player, Config.ReloadPermission))
            {
                command.ReplyToCommand($"[BannedWords] {ChatColors.Red}You do not have the correct permission to execute this command.");
                return;
            }

            try
            {
                Config.Reload();
                PrepareRuntimeConfig(Config);

                int compiledPatternCount = Config.BanSettingsGroups.Sum(group => group.CompiledBannedWords.Length);

                Logger.LogInformation("Reloaded config from '{ConfigPath}'.", Config.GetConfigPath());

                command.ReplyToCommand(
                    $"[BannedWords] {ChatColors.Lime}Configuration reloaded successfully. Loaded {compiledPatternCount} regex pattern(s)."
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to reload BannedWords config.");
                command.ReplyToCommand($"[BannedWords] {ChatColors.LightRed}Failed to reload configuration: {ex.Message}");
            }
        }

        private HookResult HandlePlayerChat(CCSPlayerController player, CommandInfo message)
        {
            string normalizedMessage = ExtractChatMessage(message);

            if (string.IsNullOrWhiteSpace(normalizedMessage))
            {
                return HookResult.Continue;
            }

            // Check for whitelist permission
            if (AdminManager.PlayerHasPermissions(player, Config.WhitelistPermission))
            {
                return HookResult.Continue;
            }

            if (normalizedMessage.Length > 0 && Config.ExcludedStartCharacters.Contains(normalizedMessage[0]))
            {
                return HookResult.Continue;
            }

            foreach (var group in Config.BanSettingsGroups)
            {
                var matchedPattern = group.CompiledBannedWords.FirstOrDefault(regex => regex.IsMatch(normalizedMessage));

                if (matchedPattern != null)
                {
                    string? playerBanType = group.ValidatedBanType;

                    if (playerBanType == null)
                    {
                        player.PrintToChat(Localizer[LocalizerKeys.BanInfo]);
                        return HookResult.Stop;
                    }

                    if (player.UserId is not int userId)
                    {
                        Logger.LogWarning("Could not resolve UserId for '{PlayerName}'. Message blocked without punishment.", player.PlayerName);
                        return HookResult.Stop;
                    }

                    string command = $"css_{playerBanType}";

                    // Determine duration and reason
                    string duration = group.ValidatedDuration;
                    string reason = group.EnableReason ? _banReason : string.Empty;

                    // Build the executeCommand string dynamically
                    string executeCommand;
                    if (string.IsNullOrWhiteSpace(duration) && string.IsNullOrWhiteSpace(reason))
                    {
                        executeCommand = $"{command} #{userId}";
                    }
                    else if (string.IsNullOrWhiteSpace(duration))
                    {
                        executeCommand = $"{command} #{userId} {reason}";
                    }
                    else
                    {
                        executeCommand = $"{command} #{userId} {duration} {reason}";
                    }

                    // Execute the appropriate command
                    Server.ExecuteCommand(executeCommand.Trim());

                    // Display messages to the player and server
                    string playerMessage = Localizer[$"{playerBanType}msgplayer", duration, reason];
                    string serverMessage = Localizer[$"{playerBanType}msgserver", player.PlayerName, reason];

                    if (group.PrintToPlayerChat)
                    {
                        player.PrintToChat(playerMessage);
                    }

                    if (group.PrintToAllChat)
                    {
                        Server.PrintToChatAll(serverMessage);
                    }

                    return HookResult.Stop;
                }
            }

            return HookResult.Continue;
        }

        // SimpleAdmin css_<type> commands this plugin can issue. Each needs <type>msgplayer and <type>msgserver in lang/ file
        private static readonly FrozenSet<string> ValidBanTypes =
            new[] { "mute", "smute", "tmute", "gag", "sgag", "tgag", "silence", "ban", "kick" }.ToFrozenSet();

        public override void Unload(bool hotReload)
        {
            RemoveCommand("css_reloadbannedwords", ReloadConfigCommand);
            RemoveCommandListener(null!, OnAnyCommandTrace, HookMode.Pre);
            //Server.PrintToConsole("The BannedWords plugin was unloaded.");
        }
    }

    public static class LocalizerKeys
    {
        public const string BanReason = "banreason";
        public const string BanInfo = "baninfo";
    }
}
