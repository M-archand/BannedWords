using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Admin;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace BannedWords
{
    public partial class BannedWords(ILogger<BannedWords> logger) : BasePlugin, IPluginConfig<BannedWordsConfig>
    {
        internal const int ExpectedConfigVersion = 1;

        public override string ModuleName => "Banned Words";
        public override string ModuleAuthor => "Marchand";
        public override string ModuleVersion => "1.0.0";

        public override void Load(bool hotReload)
        {
            AddCommandListener(null!, OnAnyCommandTrace, HookMode.Pre);
            AddCommand("css_reloadbannedwords", "Reloads the BannedWords config.", ReloadConfigCommand);
        }

        public void OnConfigParsed(BannedWordsConfig config)
        {
            Config = config;
            PrepareRuntimeConfig(Config);

            if (Config.Version != ExpectedConfigVersion)
            {
                BannedWordsDebug($"Config version mismatch detected: expected {ExpectedConfigVersion}, but got {Config.Version}.");
            }
        }
        
        private readonly ILogger<BannedWords> _logger = logger;

        public void BannedWordsDebug(string msg)
        {
            _logger.LogInformation($"[DEBUG] {msg}\n");
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

            return HandlePlayerChat(player, info, isTeamChat: command == "say_team");
        }

        private string NormalizeRegexPattern(string pattern, string? playerBanType)
        {
            if (!pattern.Contains('\b'))
            {
                return pattern;
            }

            string normalizedPattern = pattern.Replace("\b", "\\b");

            _logger.LogInformation(
                "Normalized JSON backspace escape(s) in banned word regex for PlayerBanType '{PlayerBanType}'. Use '\\\\b' in JSON config for regex word boundaries.",
                playerBanType
            );

            return normalizedPattern;
        }

        private void PrepareRuntimeConfig(BannedWordsConfig config)
        {
            config.ExcludedStartCharacters = [.. (config.ExcludeStartsWith ?? string.Empty).Distinct()];

            foreach (var group in config.BanSettingsGroups)
            {
                var compiledPatterns = new List<Regex>();

                foreach (var pattern in group.BannedWords)
                {
                    if (string.IsNullOrWhiteSpace(pattern))
                    {
                        _logger.LogWarning("Skipping empty banned word regex for PlayerBanType '{PlayerBanType}'.", group.PlayerBanType);
                        continue;
                    }

                    try
                    {
                        string normalizedPattern = NormalizeRegexPattern(pattern, group.PlayerBanType);
                        compiledPatterns.Add(new Regex(normalizedPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant));
                    }
                    catch (ArgumentException ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Skipping invalid banned word regex '{Pattern}' for PlayerBanType '{PlayerBanType}'.",
                            pattern,
                            group.PlayerBanType
                        );
                    }
                }

                group.CompiledBannedWords = [.. compiledPatterns];
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

                _logger.LogInformation(
                    "Reloaded config from '{ConfigPath}'. Prepared {CompiledPatternCount} regex pattern(s) across {GroupCount} group(s).",
                    Config.GetConfigPath(),
                    compiledPatternCount,
                    Config.BanSettingsGroups.Count
                );

                command.ReplyToCommand(
                    $"[BannedWords] {ChatColors.Lime}Configuration reloaded successfully. Loaded {compiledPatternCount} regex pattern(s)."
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload BannedWords config.");
                command.ReplyToCommand($"[BannedWords] {ChatColors.LightRed}Failed to reload configuration: {ex.Message}");
            }
        }

        private HookResult HandlePlayerChat(CCSPlayerController? player, CommandInfo message, bool isTeamChat)
        {
            string normalizedMessage = ExtractChatMessage(message);

            if (player == null || !player.IsValid || player.IsBot)
            {
                return HookResult.Continue;
            }

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
                    string? configuredPlayerBanType = group.PlayerBanType?.Trim();

                    if (string.IsNullOrWhiteSpace(configuredPlayerBanType))
                    {
                        player.PrintToChat(Chat.FormatMessage(Localizer[LocalizerKeys.BanInfo]));
                        return HookResult.Stop;
                    }

                    string playerBanType = configuredPlayerBanType.ToLowerInvariant();
                    string command = $"css_{playerBanType}";

                    if (!IsValidBanType(playerBanType))
                    {
                        _logger.LogWarning($"Invalid PlayerBanType '{group.PlayerBanType}'. Command '{command}' will fail.");
                        return HookResult.Stop;
                    }

                    // Determine duration and reason
                    string duration = !string.IsNullOrEmpty(group.DurationInMinutes) ? group.DurationInMinutes : string.Empty;
                    string reason = group.EnableReason ? Localizer[LocalizerKeys.BanReason] : string.Empty;

                    // Build the executeCommand string dynamically
                    string executeCommand;
                    if (string.IsNullOrWhiteSpace(duration) && string.IsNullOrWhiteSpace(reason))
                    {
                        executeCommand = $"{command} #{player.UserId}";
                    }
                    else if (string.IsNullOrWhiteSpace(duration))
                    {
                        executeCommand = $"{command} #{player.UserId} {reason}";
                    }
                    else
                    {
                        executeCommand = $"{command} #{player.UserId} {duration} {reason}";
                    }

                    // Execute the appropriate command
                    Server.ExecuteCommand(executeCommand.Trim());

                    // Display messages to the player and server
                    string playerMessage = Chat.FormatMessage(Localizer[$"{playerBanType}msgplayer", duration, reason]);
                    string serverMessage = Chat.FormatMessage(Localizer[$"{playerBanType}msgserver", player.PlayerName, reason]);

                    if (group.PrintToPlayerChat)
                    {
                        player.PrintToChat(playerMessage);
                    }

                    if (group.PrintToAllChat)
                    {
                        Server.PrintToChatAll(serverMessage);
                    }

                    if (!group.PrintToPlayerChat && !group.PrintToAllChat)
                    {
                        _logger.LogWarning($"Both PrintToPlayerChat and PrintToAllChat are disabled for PlayerBanType: {group.PlayerBanType}. No messages will be sent.");
                    }

                    return HookResult.Stop;
                }
            }

            return HookResult.Continue;
        }

        private static bool IsValidBanType(string banType)
        {
            // List of known valid ban types
            var validBanTypes = new[] { "mute", "smute", "tmute", "gag", "sgag", "tgag", "silence", "ban", "kick", }; // Add more valid types as needed
            return validBanTypes.Contains(banType);
        }

        public static class Chat
        {
            private static readonly Dictionary<string, char> PredefinedColors = 
                typeof(ChatColors)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .ToDictionary(
                    field => $"{{{field.Name}}}", 
                    field => (char)(field.GetValue(null) ?? '\x01')
                );

            public static string FormatMessage(string message) =>
                PredefinedColors.Aggregate(message, (current, color) => current.Replace(color.Key, $"{color.Value}"));
        }

        public override void Unload(bool hotReload)
        {
            RemoveCommand("css_reloadbannedwords", ReloadConfigCommand);
            RemoveCommandListener(null!, OnAnyCommandTrace, HookMode.Pre);
            //Server.PrintToConsole("The BannedWords plugin was unloaded.");
        }
    }

    public static class LocalizerKeys
    {
        public const string SilenceMsgPlayer = "silencemsgplayer";
        public const string SilenceMsgServer = "silencemsgserver";
        public const string GagMsgPlayer = "gagmsgplayer";
        public const string GagMsgServer = "gagmsgserver";
        public const string TGagMsgPlayer = "tgagmsgplayer";
        public const string TGagMsgServer = "tgagmsgserver";
        public const string BanReason = "banreason";
        public const string BanInfo = "baninfo";
    }
}
