# BannedWords
A simple CS2 plugin for CounterStrikeSharp that lets you set a configurable list of regex patterns that will result in an automatic silence or gag.

Requires [CS2-SimpleAdmin](https://github.com/daffyyyy/CS2-SimpleAdmin)

## [ Credits ]
> [!IMPORTANT]
> The idea and some of the code structure comes from [ExtraUtilities](https://github.com/johandrevwyk/ExtraUtilities) by [johandrevwyk](https://github.com/johandrevwyk)
## [ Configuration ]

> [!NOTE]
> Config located in  /addons/counterstrikesharp/configs/plugins/BannedWords/BannedWords.json                                
>

```json
{
  "BanSettingsGroups": [
    {
      "PlayerBanType": "silence",
      "DurationInMinutes": "5",
      "EnableReason": true,
      "PrintToPlayerChat": true,
      "PrintToAllChat": true,
      "BannedWords": [
        "(?i)word1",
        "(?i)word2",
        "(?i)\\bWord3\\b"
      ]
    }
  ],
  "WhitelistPermission": "@css/admin",
  "ExcludeStartsWith": "!/.",
  "ConfigVersion": 2
}
```

`BannedWords` entries are regular expressions. Use inline regex flags such as `(?i)` for case-insensitive matching. For example, `(?i)ban` matches `BAN`, `ban`, and `BANNING`, while `(?i)\\bBAN\\b` matches only the exact word in the JSON config.
If `PlayerBanType` is empty or omitted, the message is blocked but no punishment command is executed; instead, the player receives the localized `baninfo` chat message.
`ExcludeStartsWith` is parsed character-by-character. For example, `"ExcludeStartsWith": "!/"` excludes any message starting with `!` or `/` from banned word checks.
If you want regex word boundaries in the JSON config, use double backslashes such as `"(?i)\\bBADWORD\\b"`, not `"(?i)\bBADWORD\b"`.
> [!NOTE]
> Ban chat messages located in  /addons/counterstrikesharp/plugins/BannedWords/lang/en.json
> 

```json
{
  "silencemsgplayer": " {Red}[Server] - {Default}You have automatically been silenced for {0} minutes due to {1}",
  "silencemsgserver": " {Red}[Server] - {LightRed}{0} {Default}has automatically been silenced due to {1}",
  "gagmsgplayer": " {Red}[Server] - {Default}You have automatically been gagged for {0} minutes due to {1}",
  "gagmsgserver": " {Red}[Server] - {LightRed}{0} {Default}has automatically been gagged due to {1}",
  "banreason": "using a banned word.",
  "baninfo": "This phrase is not allowed."
}
