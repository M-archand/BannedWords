# BannedWords
A simple CS2 plugin for CounterStrikeSharp that lets you set a configurable list of words that will result in an automatic silence or gag.

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
  "BanSettings": {
    "PlayerBanType": "silence",  // Usage: silence, gag
    "DurationInMinutes": 5
  },
  "BannedWords": [
    "word1",
    "word2",
    "word3",
  ],
  "ConfigVersion": 1
}
```
> [!NOTE]
> Ban chat messages located in  /addons/counterstrikesharp/plugins/BannedWords/lang/en.json
> 

```json
{
  "silencemsgplayer": " {Red}[Server] - {Default}You have automatically been silenced for {0} minutes due to {1}",
  "silencemsgserver": " {Red}[Server] - {LightRed}{0} {Default}has automatically been silenced due to {1}",
  "gagmsgplayer": " {Red}[Server] - {Default}You have automatically been gagged for {0} minutes due to {1}",
  "gagmsgserver": " {Red}[Server] - {LightRed}{0} {Default}has automatically been gagged due to {1}",
  "banreason": "using a banned word."
}
