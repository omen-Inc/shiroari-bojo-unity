# このリポジトリで作業する AI 向けの決まり

## 仕様
- 仕様の正は Notion の仕様書です。写しは `shiroari-bojo` リポジトリの `docs/spec/`(spec.md・cards.md・cards.json。`npm run spec:sync` で更新)にあります。このリポジトリの中で仕様を書き換えたり、作ったりしないでください
- カードデータは `Assets/Data/cards.json` です。Notion の写し(`docs/spec/cards.json`)をコピーしたもので、手で編集しません。更新するときは写しからコピーし直します
- 仕様が曖昧な点を勝手に決めないでください。決めた仮定は報告し、コードには「仮:」とコメントを残します
- ルールと CPU は sb-proto(`omen-Inc/sb-proto` の index.html)を引き継いでいます。sb-proto と仕様書が食い違うときは、仕様書に合わせます

## Core(Assets/Scripts/Core)
- asmdef `ShiroariBojo.Core` は `noEngineReferences: true` です。UnityEngine・UnityEditor を参照しません。ファイル I/O もしません(JSON は文字列で受け取ります)
- 状態の変更は `Engine.Apply(state, action)` だけで行います。Apply は状態を複製してから変更するので、引数の状態は変わりません
- 乱数はシード付きの `Rng` を使います。`System.Random` や `UnityEngine.Random` は使いません(同じシードで同じ対戦を再現するため)
- CPU は `PlayerView` だけを見ます。相手の手札・山札の中身や、自分の山札の並びを CPU に渡さないでください
- ルールを変えたら、`Assets/Tests/EditMode/` にテストを足して、バッチモードで全件通してください(README の「テストの実行」)。バランスに関わる変更では、シミュレーター 1000戦の結果も報告に添えます

## UI
- UI(uGUI)・シーン・プレハブは、エディタ上でシーン/プレハブとして組みます。実行時に C# で `new GameObject` して UI を作らないでください。C# は振る舞いだけにします
- エディタの操作は /unity-editor スキルの手順(unity CLI。`--project-path` を必ず付ける)で行い、Game ビューのスクリーンショットで見た目を確かめます
- 縦画面専用です。CanvasScaler の参照解像度は 1080×1920 にします
- 画面は Core の公開 API(README の「Core の使い方」)を呼びます。ルールの判定を画面側に書かないでください(使えるかどうかは `Engine.HandOptions` の `Usable`・`Reason`・`Targets` を使います)
- 文言は日本語です。相手に見せるログは `GameEvent.TextFor(見る側の陣営)` を使い、非公開の情報を出さないようにします

## git
- commit・push は指示があるときだけ行います
