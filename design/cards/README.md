# カード絵柄の試作一式(資料)

omen が採用したカード絵柄(B ポップなイラスト風、ChatGPT で生成)の試作の資料です。ゲームでは使いません。

- ゲームのカードデータの正は `Assets/Data/cards.json`(Notion の仕様書の写し)です。ここにある `data/cards.json` は試作用のデータで、**効果文は要約版のため仕様と違います**。画像キーとの対応を見るためだけに使います
- ゲームで使うイラストは `Assets/Art/Cards/Illustrations/`(21枚)と `Assets/Art/Cards/back_common.png`(共通裏面)です。カード ID との対応は `Assets/Art/Cards/CardArtCatalog.asset` に持たせています
- 枠と文字は uGUI で組んでいます(`Assets/Prefabs/UI/CardDetail.prefab`・`HandCard.prefab`)。下の `frames/` が手本です

| パス | 中身 |
|---|---|
| `cards_final/front/*.png`・`cards_final/back/common.png` | 枠と文字を焼き込んだ完成カード(744×1039px、63×88mm の 300dpi 相当)。見た目の正解として比べる用 |
| `print/cards_print.pdf`・`print/backs_print.pdf` | ボードゲーム試作用の印刷データ(A4 に 2×2、塗り足し 3mm、トンボ付き) |
| `frames/B_pop.css`・`colors2.css`・`common.css`・`card.html` | 採用した枠(B ポップ)の HTML/CSS。設計座標は 630×880 |
| `prompts/` | 画像ごとの生成プロンプト(`prompts/README.md` に対応を記録) |
| `data/cards.json` | 試作のカードデータ(id = 画像キー、type_class = 種別の色) |
| `cards.html` | 全カードの一覧(試作時の確認用) |
| `HANDOFF_UNITY.md` | Unity 版への引き渡しメモ(omen の決定事項・生態の注意) |
| `README_prototype.md` | 試作の作り方の記録(元の README.md。Mac 側のスクリプトはここには入れていない) |
