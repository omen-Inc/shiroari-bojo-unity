# シロアリボージョ カード試作(段階1)

3絵柄の枠(HTML/CSS)に生成イラストをはめ込み、カードPNG(744x1039px、63x88mm@300dpi相当)を出力する。

## 準備(初回のみ)
    python3 -m venv .venv
    .venv/bin/pip install playwright
    .venv/bin/playwright install chromium

## 実行
    .venv/bin/python render/render_cards.py

- 出力: `cards/{絵柄}/{AI}/{画像キー}.png`(3絵柄 x openai/gemini/comfy x 3カード = 27枚)
- イラストは `gen/{絵柄}_{画像キー}/images/{最新の日時}/{AI}.png` を読む。無い組み合わせは「画像なし」の仮枠になる。画像が揃ったら再実行するだけでよい
- カードデータ: `data/sample_cards.json` / 枠: `frames/{A_zukan,B_pop,C_zukai}.css`、共通 `frames/common.css`、`frames/card.html`
- フォントは Google Fonts(オンライン時)。オフラインでは Hiragino 系に代替される

# 段階2(B ポップ × ChatGPT、全21枚+共通裏面1枚)

## 実行
    .venv/bin/python render/render_all.py

画像が増えたら同じコマンドを再実行するだけで差し替わる。1回で次をすべて作る。
- `data/cards.json`: カードデータ(`display_name` が表示名、`side` は属性として残すが裏面の選択には使わない)。`backs` は共通1種
- `cards_final/front/{id}.png`(21枚), `cards_final/back/common.png`(744x1039px)
- `cards.html`: 種別ごとのプレビュー(390px幅で1列)。裏面は共通1種
- `print/cards_print.pdf`(表21枚、6ページ)、`print/backs_print.pdf`(共通裏面、6ページ)
- 中間物 `print/_bleed/`(塗り足し込み69x94mmのPNG)
- 画像: ie_soldier / barrier / outbreak は `gen/B_pop_*`、他は `gen2/{画像キー}/images/{最新日時}/openai.png`。裏面は全カード共通で `gen2/back_control/` の最新 openai.png(盾と家の柄、cover でトリミング)

## 印刷レイアウト(日本式トンボ)
- A4縦 2x2(1ページ4枚)。カードは仕上がり63x88mm、塗り足し3mm込み69x94mm
- 配置: 左上x=28mm・y=46.5mm、カード間隔は左右・上下とも16mm。左右余白28mm・上下余白46.5mmで紙の中心に対して左右上下対称
- 角トンボ: 各カードの4隅に、仕上がり線と塗り足し線の二重線(縦2本+横2本、長さ5mm)
- センタートンボ: 上下左右の中央に、外へ伸びる6mmの線とそれに交わる6mmの短線(十字、塗り足しの端から3mm外側で交差)
- トンボはすべて塗り足しの外側。線幅0.1mm、RGB黒(SVG描画)。隣のカードのトンボ同士は重ならない(間隔16mmに対し各側6mm以内)
- 裏PDF: 表と同じ6ページ・同じ配置で共通裏面を置く。長辺とじで表裏が合うよう、列を左右反転した位置に置く(2列で左右対称なので結果は同じ)。表で空いている枠(最終ページの3枠)は裏も空ける
- 表裏の仕上がり線が紙の中心に対して鏡像で一致することを build_pages.py の assert で確認している
