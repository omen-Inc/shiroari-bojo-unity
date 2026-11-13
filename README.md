# シロアリボージョ(仮)Unity 版

受託スマホゲーム「シロアリボージョ(仮)」の Unity 版です。シロアリ側と防除業者側に分かれて戦う、CPU 対戦のカードゲームです。

- Unity 6000.2.8f1、2D(Built-in)テンプレート、uGUI を使います。スマホの縦画面専用で、参照解像度は 1080×1920 です
- `com.unity.pipeline` を入れてあるので、unity CLI からエディタを操作できます
- 仕様の正は Notion の仕様書です。写しは `shiroari-bojo` リポジトリの `docs/spec/`(spec.md・cards.md・cards.json)にあります
- ルールと CPU は、モック `omen-Inc/sb-proto`(index.html)を C# に移したものです

## 構成

```
Assets/
  Data/cards.json                  カード一覧(Notion の写し。docs/spec/cards.json をコピーしたもの)
  Scripts/Core/                    ルールエンジン(asmdef ShiroariBojo.Core。UnityEngine に依存しない)
    Cards.cs                       カード定義と cards.json の読み込み(CardDatabase.FromJson)
    Points.cs                      8ポイントと置ける種類の対応表、ルールの数値(GameRules)
    GameState.cs                   ゲームの状態
    Actions.cs                     行動(GameAction)
    GameEvent.cs                   ログ(出来事)
    Engine.cs                      状態 + 行動 → 新しい状態、合法手の列挙
    PlayerView.cs                  片方の陣営から見える情報だけを出すビュー(CPU が使う)
    Rng.cs                         シード付き乱数(mulberry32)
    MiniJson.cs                    小さな JSON パーサー
    AI/CpuPlayer.cs                CPU(sb-proto の評価関数を移したもの)
    Simulation/Simulator.cs        CPU 同士の対戦と集計
  Scripts/Editor/SimulationMenu.cs シミュレーターのメニューとバッチ用メソッド
  Tests/EditMode/                  EditMode テスト(asmdef ShiroariBojo.Tests.EditMode)
```

## テストの実行

エディタが開いていると、バッチモードではテストを実行できません。先にエディタを閉じてください。

```bash
P=/Users/a.nakashima/shiroari-bojo-unity
/Applications/Unity/Hub/Editor/6000.2.8f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -runTests -testPlatform EditMode -projectPath "$P" \
  -testResults /tmp/editmode-results.xml -logFile /tmp/editmode.log
```

結果は NUnit 形式の XML です。ルートの `total` `passed` `failed` を見ます。エディタからは Window > General > Test Runner で実行できます。

## シミュレーター(CPU 同士の対戦)

- エディタ:メニュー `Tools/シロアリボージョ/シミュレーション(1000戦)`
- バッチ:
  ```bash
  /Applications/Unity/Hub/Editor/6000.2.8f1/Unity.app/Contents/MacOS/Unity \
    -batchmode -quit -projectPath "$P" -logFile /tmp/sim.log \
    -executeMethod ShiroariBojo.EditorTools.SimulationMenu.RunBatch \
    -simGames 1000 -simSeed 1 [-simOut /path/to/result.txt]
  ```

結果はコンソールと `Logs/Simulation/sim-<日時>.txt` に出ます(`-simOut` で出力先を変更できます)。出る項目は、陣営別の勝率(先攻別も)、平均ターン数、平均完全侵食数、枯れ木が倒れた割合、カード別使用率(使った回数 / 手札に来た回数)です。シードが同じなら結果も同じになります。

## Core の使い方(画面から呼ぶ API)

```csharp
var db = CardDatabase.FromJson(cardsTextAsset.text);      // Assets/Data/cards.json の TextAsset
var state = Engine.NewGame(db, seed);                     // 先攻はコイントス。Engine.NewGame(db, seed, Side.Termite) で指定もできる
var options = Engine.HandOptions(state, Side.Pest);       // 手札ごとの Usable / Reason / Targets(画面のカードの明暗と、対象ポイントの光らせ方)
int logStart = state.Log.Count;
state = Engine.Apply(state, GameAction.Play(uid, point)); // 不正な行動は InvalidOperationException
foreach (var e in state.Log.Skip(logStart)) { /* e.Type・e.Point・e.CardId・e.TextFor(人の陣営) で演出 */ }

var cpu = new CpuPlayer(cpuSeed);
while (!state.IsOver && state.Current == cpuSide)
    state = Engine.Apply(state, cpu.ChooseAction(new PlayerView(state, cpuSide)));
```

Phase(状態の段階)ごとに使える行動は次のとおりです。

| Phase | 行動 |
| --- | --- |
| `Main` | `GameAction.Play(uid, point)`、`GameAction.EndTurn()`、`GameAction.EndTurn(mulliganUid)`(手詰まり防止。`Engine.CanMulligan` が true のとき) |
| `ForesightChoice` | `GameAction.ChooseForesight(takeUid, bottomOrder)`(めくったカードは `state.ForesightCards`。bottomOrder は先頭ほど上、末尾が山札の一番下) |
| `HandLimitDiscard` | `GameAction.DiscardForHandLimit(uids)`(`state.HandLimitDiscardCount` 枚) |
| `GameOver` | なし(`Winner`・`WinReason`) |
