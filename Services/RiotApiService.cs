using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SummonerScout.Services
{
    public class RiotApiService
    {
        private readonly HttpClient _http = new();
        private string _apiKey = "";

        private static readonly Dictionary<string, string> PlatformMap = new()
        {
            ["NA"] = "na1", ["EUW"] = "euw1", ["EUNE"] = "eun1", ["KR"] = "kr",
            ["JP"] = "jp1", ["BR"] = "br1", ["LAN"] = "la1", ["LAS"] = "la2",
            ["OCE"] = "oc1", ["TR"] = "tr1", ["RU"] = "ru", ["PH"] = "ph2",
            ["SG"] = "sg2", ["TH"] = "th2", ["TW"] = "tw2", ["VN"] = "vn2"
        };

        private static readonly Dictionary<string, string> OpggRegionMap = new()
        {
            ["NA"] = "na", ["EUW"] = "euw", ["EUNE"] = "eune", ["KR"] = "kr",
            ["JP"] = "jp", ["BR"] = "br", ["LAN"] = "lan", ["LAS"] = "las",
            ["OCE"] = "oce", ["TR"] = "tr", ["RU"] = "ru"
        };

        public static readonly string[] Regions = { "NA", "EUW", "EUNE", "KR", "JP", "BR", "LAN", "LAS", "OCE", "TR", "RU" };

        public void SetApiKey(string key) => _apiKey = key;

        private string GetRegional(string platform)
        {
            string[] americas = { "na1", "br1", "la1", "la2", "oc1" };
            string[] asia = { "kr", "jp1", "ph2", "sg2", "th2", "tw2", "vn2" };
            if (americas.Contains(platform)) return "americas";
            if (asia.Contains(platform)) return "asia";
            return "europe";
        }

        private async Task<JToken> RiotFetch(string url)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-Riot-Token", _apiKey);
            var res = await _http.SendAsync(req);

            if ((int)res.StatusCode == 429)
            {
                await Task.Delay(2000);
                return await RiotFetch(url);
            }

            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                throw new Exception($"Riot API {(int)res.StatusCode}: {body}");

            return JToken.Parse(body);
        }

        public async Task<SummonerData> LookupSummoner(string gameName, string tagLine, string region)
        {
            var platform = PlatformMap[region];
            var regional = GetRegional(platform);

            // 1. Account
            var account = await RiotFetch(
                $"https://{regional}.api.riotgames.com/riot/account/v1/accounts/by-riot-id/{Uri.EscapeDataString(gameName)}/{Uri.EscapeDataString(tagLine)}");
            var puuid = account["puuid"]!.ToString();

            // 2. Summoner
            var summoner = await RiotFetch(
                $"https://{platform}.api.riotgames.com/lol/summoner/v4/summoners/by-puuid/{puuid}");

            // 3. Ranked
            var ranks = new List<RankInfo>();
            try
            {
                var rankData = await RiotFetch(
                    $"https://{platform}.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}");
                foreach (var r in (JArray)rankData)
                {
                    ranks.Add(new RankInfo
                    {
                        Queue = r["queueType"]!.ToString() == "RANKED_SOLO_5x5" ? "Solo/Duo" : "Flex",
                        Tier = r["tier"]!.ToString(),
                        Rank = r["rank"]!.ToString(),
                        LP = (int)r["leaguePoints"]!,
                        Wins = (int)r["wins"]!,
                        Losses = (int)r["losses"]!
                    });
                }
            }
            catch { }

            // 4. Match IDs
            var matchIds = await RiotFetch(
                $"https://{regional}.api.riotgames.com/lol/match/v5/matches/by-puuid/{puuid}/ids?start=0&count=20");

            // 5. Match details
            var matches = new List<MatchInfo>();
            foreach (var id in (JArray)matchIds)
            {
                try
                {
                    await Task.Delay(200);
                    var match = await RiotFetch(
                        $"https://{regional}.api.riotgames.com/lol/match/v5/matches/{id}");
                    var info = match["info"]!;
                    var participants = (JArray)info["participants"]!;
                    var p = participants.FirstOrDefault(x => x["puuid"]!.ToString() == puuid);
                    if (p == null) continue;

                    matches.Add(new MatchInfo
                    {
                        Champion = p["championName"]!.ToString(),
                        Win = (bool)p["win"]!,
                        Kills = (int)p["kills"]!,
                        Deaths = (int)p["deaths"]!,
                        Assists = (int)p["assists"]!,
                        CS = (int)p["totalMinionsKilled"]! + (int)p["neutralMinionsKilled"]!,
                        Duration = (int)info["gameDuration"]!,
                        QueueId = (int)info["queueId"]!,
                        TimeAgo = GetTimeAgo((long)(info["gameEndTimestamp"] ?? info["gameCreation"])!)
                    });
                }
                catch { }
            }

            return new SummonerData
            {
                Name = account["gameName"]!.ToString(),
                Tag = account["tagLine"]!.ToString(),
                Region = region,
                Level = (int)summoner["summonerLevel"]!,
                IconId = (int)summoner["profileIconId"]!,
                Ranks = ranks,
                Matches = matches
            };
        }

        public async Task<PastSeasons> GetPastSeasons(string gameName, string tagLine, string region)
        {
            var opggRegion = OpggRegionMap.GetValueOrDefault(region, "euw");
            var url = $"https://www.op.gg/summoners/{opggRegion}/{Uri.EscapeDataString(gameName)}-{Uri.EscapeDataString(tagLine)}";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("User-Agent", "Mozilla/5.0");
            var res = await _http.SendAsync(req);
            var html = await res.Content.ReadAsStringAsync();

            var result = new PastSeasons();
            var soloIdx = html.IndexOf("<caption>Ranked Solo/Duo</caption>");
            var flexIdx = html.IndexOf("<caption>Ranked Flex</caption>");

            if (soloIdx > -1)
            {
                var end = flexIdx > soloIdx ? flexIdx : soloIdx + 5000;
                result.Solo = ExtractSeasons(html.Substring(soloIdx, end - soloIdx));
            }
            if (flexIdx > -1)
            {
                var block = html.Substring(flexIdx, Math.Min(5000, html.Length - flexIdx));
                result.Flex = ExtractSeasons(block);
            }
            return result;
        }

        private List<SeasonRank> ExtractSeasons(string block)
        {
            var results = new List<SeasonRank>();
            var regex = new System.Text.RegularExpressions.Regex(
                @"S(20\d{2}(?:\s*S\d)?)\s*</strong>.*?<span[^>]*>([a-z]+)\s+(\d)</span>.*?text-gray-500"">\s*(\d+)",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            foreach (System.Text.RegularExpressions.Match m in regex.Matches(block))
            {
                results.Add(new SeasonRank
                {
                    Season = $"S{m.Groups[1].Value.Trim()}",
                    Tier = char.ToUpper(m.Groups[2].Value[0]) + m.Groups[2].Value.Substring(1),
                    Rank = m.Groups[3].Value,
                    LP = int.Parse(m.Groups[4].Value)
                });
            }
            return results;
        }

        private string GetTimeAgo(long timestamp)
        {
            var diff = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 2) return "Yesterday";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return $"{(int)(diff.TotalDays / 7)}w ago";
        }
    }

    // Data models
    public class SummonerData
    {
        public string Name { get; set; } = "";
        public string Tag { get; set; } = "";
        public string Region { get; set; } = "";
        public int Level { get; set; }
        public int IconId { get; set; }
        public List<RankInfo> Ranks { get; set; } = new();
        public List<MatchInfo> Matches { get; set; } = new();
    }

    public class RankInfo
    {
        public string Queue { get; set; } = "";
        public string Tier { get; set; } = "";
        public string Rank { get; set; } = "";
        public int LP { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int WinRate => Wins + Losses > 0 ? (int)Math.Round(Wins * 100.0 / (Wins + Losses)) : 0;
    }

    public class MatchInfo
    {
        public string Champion { get; set; } = "";
        public bool Win { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public int CS { get; set; }
        public int Duration { get; set; }
        public int QueueId { get; set; }
        public string TimeAgo { get; set; } = "";
        public string KDA => Deaths > 0 ? $"{((Kills + Assists) / (double)Deaths):F2}" : "Perfect";
        public string QueueName => QueueId switch { 420 => "Ranked Solo", 440 => "Ranked Flex", 450 => "ARAM", 490 => "Quickplay", _ => "Normal" };
    }

    public class PastSeasons
    {
        public List<SeasonRank> Solo { get; set; } = new();
        public List<SeasonRank> Flex { get; set; } = new();
    }

    public class SeasonRank
    {
        public string Season { get; set; } = "";
        public string Tier { get; set; } = "";
        public string Rank { get; set; } = "";
        public int LP { get; set; }
    }
}
