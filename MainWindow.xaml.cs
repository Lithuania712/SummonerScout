using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SummonerScout.Services;

namespace SummonerScout
{
    public partial class MainWindow : Window
    {
        private readonly RiotApiService _api = new();
        private const string DDRAGON = "https://ddragon.leagueoflegends.com/cdn/16.8.1";

        // Obfuscated API key — split and reassembled at runtime so it's not a plain string in the binary
        private static string GetEmbeddedKey()
        {
            var parts = new[] { "RGAPI", "-", "8637b77c", "-", "cda2", "-", "4ce7", "-", "99a8", "-", "a920cb9bbaf0" };
            return string.Concat(parts);
        }

        public MainWindow()
        {
            InitializeComponent();
            foreach (var r in RiotApiService.Regions)
                RegionBox.Items.Add(r);
            RegionBox.SelectedIndex = 1; // EUW

            NameBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) SearchBtn_Click(s, e); };
            TagBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) SearchBtn_Click(s, e); };

            // Set the key internally — never shown to users
            _api.SetApiKey(GetEmbeddedKey());
        }

        private async void SearchBtn_Click(object sender, RoutedEventArgs e)
        {
            var name = NameBox.Text.Trim();
            var tag = TagBox.Text.Trim();
            var region = RegionBox.SelectedItem?.ToString() ?? "EUW";

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(tag)) return;
            ErrorText.Visibility = Visibility.Collapsed;
            LoadingText.Visibility = Visibility.Visible;
            SearchBtn.IsEnabled = false;

            try
            {
                var data = await _api.LookupSummoner(name, tag, region);
                RenderProfile(data);

                // Load past seasons async
                try
                {
                    var seasons = await _api.GetPastSeasons(name, tag, region);
                    RenderPastSeasons(seasons);
                }
                catch { }

                SearchView.Visibility = Visibility.Collapsed;
                ProfileView.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                ShowError(ex.Message.Length > 100 ? ex.Message.Substring(0, 100) : ex.Message);
            }
            finally
            {
                LoadingText.Visibility = Visibility.Collapsed;
                SearchBtn.IsEnabled = true;
            }
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            ProfileView.Visibility = Visibility.Collapsed;
            SearchView.Visibility = Visibility.Visible;
        }

        private void ShowError(string msg)
        {
            ErrorText.Text = msg;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void RenderProfile(SummonerData data)
        {
            // Header
            try { SummonerIcon.ImageSource = new BitmapImage(new Uri($"{DDRAGON}/img/profileicon/{data.IconId}.png")); } catch { }
            LevelText.Text = $"Lvl {data.Level}";
            SummonerNameText.Text = $"{data.Name}#{data.Tag}";
            RegionText.Text = data.Region.ToUpper();

            // Rank badges in header
            RankBadgesPanel.Children.Clear();
            foreach (var r in data.Ranks)
                RankBadgesPanel.Children.Add(CreateRankBadge(r));
            if (data.Ranks.Count == 0)
                RankBadgesPanel.Children.Add(MakeText("Unranked", 13, "#5A6A7A"));

            // Ranked cards
            var solo = data.Ranks.FirstOrDefault(r => r.Queue == "Solo/Duo");
            var flex = data.Ranks.FirstOrDefault(r => r.Queue == "Flex");
            RenderRankCard(SoloRankPanel, solo, data.Matches.Where(m => m.QueueId == 420).ToList());
            RenderRankCard(FlexRankPanel, flex, data.Matches.Where(m => m.QueueId == 440).ToList());

            // Win rate
            RenderWinRate(data.Matches);

            // Champions
            RenderChampions(data.Matches);

            // Match history
            RenderMatches(data.Matches);
        }

        private void RenderRankCard(StackPanel panel, RankInfo? rank, List<MatchInfo> queueMatches)
        {
            panel.Children.Clear();
            if (rank != null)
            {
                var tierText = $"{rank.Tier} {rank.Rank}";
                panel.Children.Add(MakeText(tierText, 20, "#F0E6D2", FontWeights.Bold));
                panel.Children.Add(MakeText($"{rank.LP} LP", 13, "#C89B3C", FontWeights.SemiBold));
                panel.Children.Add(MakeText($"{rank.Wins}W {rank.Losses}L · {rank.WinRate}% WR", 11, "#5A6A7A"));
            }
            else
            {
                panel.Children.Add(MakeText("Unranked", 16, "#5A6A7A"));
            }

            if (queueMatches.Count > 0)
            {
                var wins = queueMatches.Count(m => m.Win);
                var losses = queueMatches.Count - wins;
                var wr = (int)Math.Round(wins * 100.0 / queueMatches.Count);
                panel.Children.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(30, 58, 95)), Margin = new Thickness(0, 8, 0, 8) });
                panel.Children.Add(MakeText($"Recent: {wins}W {losses}L ({wr}%)", 11, "#8C9AA0"));
            }
        }

        private void RenderPastSeasons(PastSeasons seasons)
        {
            RenderSeasonList(SoloPastPanel, seasons.Solo);
            RenderSeasonList(FlexPastPanel, seasons.Flex);
        }

        private void RenderSeasonList(StackPanel panel, List<SeasonRank> seasons)
        {
            panel.Children.Clear();
            if (seasons.Count == 0) return;

            panel.Children.Add(new Separator { Background = new SolidColorBrush(Color.FromRgb(30, 58, 95)), Margin = new Thickness(0, 4, 0, 8) });
            panel.Children.Add(MakeText("PAST SEASONS", 9, "#5A6A7A", FontWeights.Bold));

            foreach (var s in seasons)
            {
                var row = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var label = MakeText(s.Season, 11, "#5A6A7A");
                Grid.SetColumn(label, 0);
                row.Children.Add(label);

                var tier = MakeText($"{s.Tier} {s.Rank}", 11, GetTierColor(s.Tier), FontWeights.SemiBold);
                Grid.SetColumn(tier, 1);
                row.Children.Add(tier);

                var lp = MakeText($"{s.LP} LP", 10, "#5A6A7A");
                Grid.SetColumn(lp, 2);
                row.Children.Add(lp);

                panel.Children.Add(row);
            }
        }

        private void RenderWinRate(List<MatchInfo> matches)
        {
            WinRatePanel.Children.Clear();
            var wins = matches.Count(m => m.Win);
            var losses = matches.Count - wins;
            var wr = matches.Count > 0 ? (int)Math.Round(wins * 100.0 / matches.Count) : 0;
            var avgK = matches.Count > 0 ? matches.Average(m => m.Kills) : 0;
            var avgD = matches.Count > 0 ? matches.Average(m => m.Deaths) : 0;
            var avgA = matches.Count > 0 ? matches.Average(m => m.Assists) : 0;
            var kda = avgD > 0 ? $"{((avgK + avgA) / avgD):F2}" : "Perfect";

            WinRatePanel.Children.Add(MakeText($"{wr}%", 36, wr >= 55 ? "#49B4AB" : wr >= 45 ? "#C89B3C" : "#E84057", FontWeights.Bold));
            WinRatePanel.Children.Add(MakeText("Win Rate", 10, "#5A6A7A"));

            var statsGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            statsGrid.ColumnDefinitions.Add(new ColumnDefinition());

            AddStatCell(statsGrid, 0, wins.ToString(), "Wins", "#49B4AB");
            AddStatCell(statsGrid, 1, losses.ToString(), "Losses", "#E84057");
            AddStatCell(statsGrid, 2, matches.Count.ToString(), "Played", "#F0E6D2");
            AddStatCell(statsGrid, 3, kda, "Avg KDA", "#F0E6D2");

            WinRatePanel.Children.Add(statsGrid);
        }

        private void AddStatCell(Grid grid, int col, string value, string label, string color)
        {
            var sp = new StackPanel();
            sp.Children.Add(MakeText(value, 20, color, FontWeights.Bold));
            sp.Children.Add(MakeText(label, 9, "#5A6A7A"));
            Grid.SetColumn(sp, col);
            grid.Children.Add(sp);
        }

        private void RenderChampions(List<MatchInfo> matches)
        {
            ChampionsPanel.Children.Clear();
            var champs = matches.GroupBy(m => m.Champion)
                .Select(g => new { Name = g.Key, Games = g.Count(), Wins = g.Count(m => m.Win), Losses = g.Count(m => !m.Win),
                    Kills = g.Sum(m => m.Kills), Deaths = g.Sum(m => m.Deaths), Assists = g.Sum(m => m.Assists) })
                .OrderByDescending(c => c.Games).Take(7);

            foreach (var c in champs)
            {
                var wr = (int)Math.Round(c.Wins * 100.0 / c.Games);
                var kda = c.Deaths > 0 ? $"{((c.Kills + c.Assists) / (double)c.Deaths):F2}" : "Perfect";
                var wrColor = wr >= 60 ? "#49B4AB" : wr >= 45 ? "#C89B3C" : "#E84057";

                var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

                var icon = new Ellipse { Width = 30, Height = 30 };
                try { icon.Fill = new ImageBrush(new BitmapImage(new Uri($"{DDRAGON}/img/champion/{c.Name.Replace(" ", "").Replace("'", "")}.png"))) { Stretch = Stretch.UniformToFill }; }
                catch { icon.Fill = new SolidColorBrush(Color.FromRgb(14, 34, 54)); }
                Grid.SetColumn(icon, 0);
                row.Children.Add(icon);

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                info.Children.Add(MakeText(c.Name, 12, "#F0E6D2", FontWeights.SemiBold));
                info.Children.Add(MakeText($"{c.Games}g · {c.Wins}W {c.Losses}L", 10, "#5A6A7A"));
                Grid.SetColumn(info, 1);
                row.Children.Add(info);

                var kdaText = MakeText($"{kda} KDA", 11, "#8C9AA0");
                kdaText.VerticalAlignment = VerticalAlignment.Center;
                kdaText.Margin = new Thickness(0, 0, 10, 0);
                Grid.SetColumn(kdaText, 2);
                row.Children.Add(kdaText);

                var wrText = MakeText($"{wr}%", 12, wrColor, FontWeights.Bold);
                wrText.VerticalAlignment = VerticalAlignment.Center;
                wrText.TextAlignment = TextAlignment.Right;
                Grid.SetColumn(wrText, 3);
                row.Children.Add(wrText);

                ChampionsPanel.Children.Add(row);
            }
        }

        private void RenderMatches(List<MatchInfo> matches)
        {
            MatchListPanel.Children.Clear();
            foreach (var m in matches)
            {
                var bg = m.Win ? Color.FromArgb(15, 73, 180, 171) : Color.FromArgb(15, 232, 64, 87);
                var border = new Border
                {
                    Background = new SolidColorBrush(bg),
                    BorderBrush = new SolidColorBrush(m.Win ? Color.FromArgb(60, 73, 180, 171) : Color.FromArgb(60, 232, 64, 87)),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 2, 0, 2)
                };

                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });

                var result = MakeText(m.Win ? "WIN" : "LOSS", 10, m.Win ? "#49B4AB" : "#E84057", FontWeights.Bold);
                result.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(result, 0);
                row.Children.Add(result);

                var champIcon = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(8) };
                try { champIcon.Background = new ImageBrush(new BitmapImage(new Uri($"{DDRAGON}/img/champion/{m.Champion.Replace(" ", "").Replace("'", "")}.png"))) { Stretch = Stretch.UniformToFill }; }
                catch { champIcon.Background = new SolidColorBrush(Color.FromRgb(14, 34, 54)); }
                Grid.SetColumn(champIcon, 1);
                row.Children.Add(champIcon);

                var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                info.Children.Add(MakeText(m.Champion, 12, "#F0E6D2", FontWeights.SemiBold));
                info.Children.Add(MakeText(m.QueueName, 10, "#5A6A7A"));
                Grid.SetColumn(info, 2);
                row.Children.Add(info);

                var kdaStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                kdaStack.Children.Add(MakeText($"{m.Kills}/{m.Deaths}/{m.Assists}", 12, "#F0E6D2", FontWeights.SemiBold));
                var kdaColor = m.KDA == "Perfect" ? "#C89B3C" : double.Parse(m.KDA) >= 4 ? "#49B4AB" : double.Parse(m.KDA) >= 2 ? "#8C9AA0" : "#E84057";
                kdaStack.Children.Add(MakeText($"{m.KDA} KDA", 10, kdaColor));
                Grid.SetColumn(kdaStack, 3);
                row.Children.Add(kdaStack);

                var cs = MakeText($"{m.CS} CS", 11, "#5A6A7A");
                cs.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(cs, 4);
                row.Children.Add(cs);

                var dur = MakeText($"{m.Duration / 60}:{(m.Duration % 60):D2}", 11, "#5A6A7A");
                dur.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(dur, 5);
                row.Children.Add(dur);

                var time = MakeText(m.TimeAgo, 10, "#3A4A5A");
                time.VerticalAlignment = VerticalAlignment.Center;
                time.TextAlignment = TextAlignment.Right;
                Grid.SetColumn(time, 6);
                row.Children.Add(time);

                border.Child = row;
                MatchListPanel.Children.Add(border);
            }
        }

        // Helper methods
        private Border CreateRankBadge(RankInfo r)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, 200, 155, 60)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 200, 155, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(4, 0, 4, 0)
            };
            var sp = new StackPanel();
            sp.Children.Add(MakeText(r.Queue.ToUpper(), 8, "#5A6A7A"));
            sp.Children.Add(MakeText($"{r.Tier} {r.Rank}", 13, "#F0E6D2", FontWeights.Bold));
            sp.Children.Add(MakeText($"{r.LP} LP · {r.WinRate}% WR", 10, "#C89B3C"));
            border.Child = sp;
            return border;
        }

        private static TextBlock MakeText(string text, double size, string color, FontWeight? weight = null)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                FontWeight = weight ?? FontWeights.Normal,
                FontFamily = new FontFamily("Segoe UI")
            };
        }

        private static string GetTierColor(string tier) => tier.ToLower() switch
        {
            "iron" => "#6B6B6B", "bronze" => "#A0715E", "silver" => "#8C9AA0",
            "gold" => "#C89B3C", "platinum" => "#4E9996", "emerald" => "#2D9171",
            "diamond" => "#576BCE", "master" => "#9D48E0", "grandmaster" => "#E34343",
            "challenger" => "#F4C874", _ => "#8C9AA0"
        };
    }
}
