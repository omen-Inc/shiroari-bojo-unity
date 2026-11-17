// 簡易シミュレーター(CPU 同士の対戦)。
//  エディタ:メニュー Tools/シロアリボージョ/シミュレーション(1000戦)
//  バッチ  :Unity -batchmode -quit -projectPath <P> -executeMethod ShiroariBojo.EditorTools.SimulationMenu.RunBatch
//            [-simGames 1000] [-simSeed 1] [-simOut <ファイルパス>] [-simJson <ファイルパス>]
// 結果はコンソールと Logs/Simulation/sim-<日時>.txt(-simOut で変更可)に出す。
// -simJson を付けると、夜間の自動開発の報告用に JSON(games・winRate・avgTurns・cards[] など)も書き出す。

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ShiroariBojo.Core;
using ShiroariBojo.Core.Simulation;
using UnityEditor;
using UnityEngine;

namespace ShiroariBojo.EditorTools
{
    public static class SimulationMenu
    {
        public const string CardsAssetPath = "Assets/Data/cards.json";

        [MenuItem("Tools/シロアリボージョ/シミュレーション(1000戦)")]
        public static void RunFromMenu()
        {
            try
            {
                string path = Run(1000, 1, null, n =>
                {
                    if (n % 50 == 0) EditorUtility.DisplayProgressBar("シロアリボージョ", $"CPU 同士の対戦 {n}/1000", n / 1000f);
                });
                EditorUtility.RevealInFinder(path);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>-executeMethod 用</summary>
        public static void RunBatch()
        {
            try
            {
                int games = 1000;
                uint seed = 1;
                string outPath = null;
                string jsonPath = null;
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-simGames") games = int.Parse(args[i + 1]);
                    else if (args[i] == "-simSeed") seed = uint.Parse(args[i + 1]);
                    else if (args[i] == "-simOut") outPath = args[i + 1];
                    else if (args[i] == "-simJson") jsonPath = args[i + 1];
                }
                Run(games, seed, outPath, null, jsonPath);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        public static CardDatabase LoadCards()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CardsAssetPath);
            if (asset == null) throw new FileNotFoundException($"{CardsAssetPath} が見つかりません");
            return CardDatabase.FromJson(asset.text);
        }

        /// <summary>対戦を回してレポートを書き出し、書き出したファイルのパスを返す。jsonPath を渡すと JSON も書き出す</summary>
        public static string Run(int games, uint seed, string outPath, Action<int> progress, string jsonPath = null)
        {
            var db = LoadCards();
            foreach (var w in db.Warnings) Debug.LogWarning(w);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // Simulator.Run と同じ対戦(同じシード・同じルール)を回しつつ、勝利寄与のために1戦ごとの使用カードも記録する
            var rules = GameRules.Default;
            rules.KeepLog = false;
            var result = new SimulationResult();
            var contrib = new ContributionTracker();
            for (int g = 0; g < games; g++)
            {
                var s = Simulator.RunGame(db, unchecked(seed + (uint)g), rules);
                Simulator.Accumulate(result, s);
                contrib.Add(s);
                progress?.Invoke(g + 1);
            }
            sw.Stop();
            string report = result.ToReport(db, $"シロアリボージョ CPU 同士の対戦 {games}戦(seed {seed}、{DateTime.Now:yyyy-MM-dd HH:mm:ss}、{sw.ElapsedMilliseconds}ms)");
            if (string.IsNullOrEmpty(outPath))
            {
                string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "Simulation");
                Directory.CreateDirectory(dir);
                outPath = Path.Combine(dir, $"sim-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            }
            else
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            File.WriteAllText(outPath, report);
            Debug.Log(report + $"\n書き出し先: {outPath}");
            if (!string.IsNullOrEmpty(jsonPath))
            {
                string dir = Path.GetDirectoryName(Path.GetFullPath(jsonPath));
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(jsonPath, ToJson(result, contrib, db, seed), new UTF8Encoding(false));
                Debug.Log($"JSON の書き出し先: {jsonPath}");
            }
            return outPath;
        }

        /// <summary>
        /// カードを1回以上使った (試合, 陣営) ごとに「その陣営が勝ったか(1/0)」を記録する。
        /// 勝利寄与 = その平均 − その陣営の全体勝率(Web 版 shiroari-bojo の src/core/sim.ts と同じ定義)
        /// </summary>
        private sealed class ContributionTracker
        {
            public readonly Dictionary<string, long> Uses = new Dictionary<string, long>();
            public readonly Dictionary<string, long> Wins = new Dictionary<string, long>();
            /// <summary>[陣営] カードID → そのカードを使った (試合, 陣営) の数</summary>
            public readonly Dictionary<string, long>[] UsesBySide = { new Dictionary<string, long>(), new Dictionary<string, long>() };

            public void Add(GameState s)
            {
                for (int k = 0; k < 2; k++)
                {
                    bool won = s.Winner.HasValue && (int)s.Winner.Value == k;
                    foreach (var kv in s.Stats.Played[k])
                    {
                        if (kv.Value <= 0) continue;
                        Inc(Uses, kv.Key);
                        Inc(UsesBySide[k], kv.Key);
                        if (won) Inc(Wins, kv.Key);
                    }
                }
            }

            private static void Inc(Dictionary<string, long> d, string key) => d[key] = (d.TryGetValue(key, out var v) ? v : 0) + 1;
        }

        /// <summary>夜間の自動開発の報告用 JSON(README の「シミュレーター」の JSON の形)</summary>
        private static string ToJson(SimulationResult r, ContributionTracker c, CardDatabase db, uint seed)
        {
            var ci = CultureInfo.InvariantCulture;
            int games = Math.Max(1, r.Games);
            double termiteRate = (double)r.TermiteWins / games;
            double pestRate = (double)r.PestWins / games;
            string N(double v, int digits = 4) => Math.Round(v, digits, MidpointRounding.AwayFromZero).ToString("0.####", ci);

            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"games\":").Append(r.Games.ToString(ci));
            sb.Append(",\"seed\":").Append(Str(seed.ToString(ci)));
            sb.Append(",\"winRate\":{\"termite\":").Append(N(termiteRate)).Append(",\"pest\":").Append(N(pestRate)).Append('}');
            sb.Append(",\"draws\":").Append(r.Draws.ToString(ci));
            sb.Append(",\"avgTurns\":").Append(N((double)r.TotalTurns / games, 2));
            sb.Append(",\"cards\":[");
            bool first = true;
            foreach (var def in db.All)
            {
                if (def.CountTermiteDeck + def.CountPestDeck <= 0) continue;   // 山札に入らないカード(女王アリなど)は Web 版と同じく出さない
                long drawn = 0, played = 0;
                for (int k = 0; k < 2; k++)
                {
                    drawn += r.Drawn[k].TryGetValue(def.Id, out var d) ? d : 0;
                    played += r.Played[k].TryGetValue(def.Id, out var p) ? p : 0;
                }
                c.Uses.TryGetValue(def.Id, out long uses);
                c.Wins.TryGetValue(def.Id, out long wins);
                string contribution = "null";
                if (uses > 0)
                {
                    double baseline = 0;
                    for (int k = 0; k < 2; k++)
                    {
                        c.UsesBySide[k].TryGetValue(def.Id, out long u);
                        baseline += u * (k == (int)Side.Termite ? termiteRate : pestRate);
                    }
                    contribution = N((wins - baseline) / uses);
                }
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"id\":").Append(Str(def.Id));
                sb.Append(",\"name\":").Append(Str(def.Name));
                sb.Append(",\"side\":").Append(Str(SideKey(def.User)));
                sb.Append(",\"useRate\":").Append(drawn > 0 ? N((double)played / drawn) : "null");
                sb.Append(",\"winContribution\":").Append(contribution);
                sb.Append(",\"drawn\":").Append(drawn.ToString(ci));
                sb.Append(",\"played\":").Append(played.ToString(ci));
                sb.Append(",\"gamesUsed\":").Append(uses.ToString(ci));
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string SideKey(CardUser user)
        {
            switch (user)
            {
                case CardUser.Termite: return "termite";
                case CardUser.Pest: return "pest";
                default: return "neutral";
            }
        }

        private static string Str(string v)
        {
            var sb = new StringBuilder("\"");
            foreach (char ch in v ?? "")
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
