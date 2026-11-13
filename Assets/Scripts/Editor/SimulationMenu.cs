// 簡易シミュレーター(CPU 同士の対戦)。
//  エディタ:メニュー Tools/シロアリボージョ/シミュレーション(1000戦)
//  バッチ  :Unity -batchmode -quit -projectPath <P> -executeMethod ShiroariBojo.EditorTools.SimulationMenu.RunBatch
//            [-simGames 1000] [-simSeed 1] [-simOut <ファイルパス>]
// 結果はコンソールと Logs/Simulation/sim-<日時>.txt(-simOut で変更可)に出す。

using System;
using System.IO;
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
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-simGames") games = int.Parse(args[i + 1]);
                    else if (args[i] == "-simSeed") seed = uint.Parse(args[i + 1]);
                    else if (args[i] == "-simOut") outPath = args[i + 1];
                }
                Run(games, seed, outPath, null);
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

        /// <summary>対戦を回してレポートを書き出し、書き出したファイルのパスを返す</summary>
        public static string Run(int games, uint seed, string outPath, Action<int> progress)
        {
            var db = LoadCards();
            foreach (var w in db.Warnings) Debug.LogWarning(w);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = Simulator.Run(db, games, seed, null, progress);
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
            return outPath;
        }
    }
}
