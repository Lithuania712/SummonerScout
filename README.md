# SummonerScout

A native Windows desktop application for looking up League of Legends player statistics.

## Features

- **Summoner Lookup** — Search any player by Riot ID across all regions (NA, EUW, EUNE, KR, BR, OCE, and more)
- **Ranked Overview** — View current Solo/Duo and Flex ranks with LP, win rate, and season record
- **Past Season Ranks** — See historical ranked tiers from previous seasons
- **Match History** — Browse recent matches with champion, KDA, CS, game duration, and result
- **Champion Stats** — Most played champions with per-champion win rates and KDA
- **Overall Performance** — Aggregate win rate, average KDA, and total games played

## Tech Stack

- C# / WPF (.NET 9)
- Riot Games API (Account-v1, Summoner-v4, League-v4, Match-v5)
- Data Dragon for champion and item assets

## Installation

1. Download `SummonerScout.exe` from the [Releases](../../releases) page
2. Run it — no installation required (self-contained single-file executable)

## Building from Source

```
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## API Usage

This application uses the following Riot Games API endpoints:
- `Account-v1` — Resolve Riot IDs to PUUIDs
- `Summoner-v4` — Retrieve summoner profile data
- `League-v4` — Fetch current ranked standings
- `Match-v5` — Retrieve match history and participant details

## Disclaimer

SummonerScout is not endorsed by Riot Games and does not reflect the views or opinions of Riot Games or anyone officially involved in producing or managing League of Legends. League of Legends and Riot Games are trademarks or registered trademarks of Riot Games, Inc.
