// 試作用のカードデータ。Assets/Data/cards.json(Notion の写し)から「没」以外を抜き出したもの。文言は変えていない。
// 手で編集しない。作り直すときは design/ui/README.md の「data.js の作り直し」を参照。
window.CARDS = [
 {
  "id": "worker-yamato",
  "name": "職アリ-ヤマト",
  "category": "シロアリ",
  "cost": 1,
  "counts": {
   "termite": 4,
   "pest": 0,
   "outside": 0
  },
  "effect": "1枚分として数える。基本の侵食カード",
  "art": "yamato_worker"
 },
 {
  "id": "worker-ie",
  "name": "職アリ-イエ",
  "category": "シロアリ",
  "cost": 1,
  "counts": {
   "termite": 12,
   "pest": 0,
   "outside": 0
  },
  "effect": "1枚分として数える。基本の侵食カード",
  "art": "ie_worker"
 },
 {
  "id": "worker-kanzai",
  "name": "職アリ-カンザイ",
  "category": "シロアリ",
  "cost": 1,
  "counts": {
   "termite": 4,
   "pest": 0,
   "outside": 0
  },
  "effect": "1枚分として数える。基本の侵食カード",
  "art": "kanzai_worker"
 },
 {
  "id": "soldier-yamato",
  "name": "兵アリ-ヤマト",
  "category": "シロアリ",
  "cost": 2,
  "counts": {
   "termite": 2,
   "pest": 0,
   "outside": 0
  },
  "effect": "1枚分として数える。いるポイントへの薬剤散布を1回無効化する（兵アリ1枚につき1回）。ベイト工法は防げない",
  "art": "yamato_soldier"
 },
 {
  "id": "soldier-ie",
  "name": "兵アリ-イエ",
  "category": "シロアリ",
  "cost": 2,
  "counts": {
   "termite": 5,
   "pest": 0,
   "outside": 0
  },
  "effect": "1枚分として数える。いるポイントへの薬剤散布を1回無効化する（兵アリ1枚につき1回）。ベイト工法は防げない",
  "art": "ie_soldier"
 },
 {
  "id": "soldier-kanzai",
  "name": "兵アリ-カンザイ",
  "category": "シロアリ",
  "cost": 2,
  "counts": {
   "termite": 2,
   "pest": 0,
   "outside": 0
  },
  "effect": "1枚分として数える。いるポイントへの薬剤散布を1回無効化する（兵アリ1枚につき1回）。ベイト工法は防げない",
  "art": "kanzai_soldier"
 },
 {
  "id": "winged-yamato",
  "name": "羽アリ-ヤマト",
  "category": "シロアリ",
  "cost": 2,
  "counts": {
   "termite": 1,
   "pest": 0,
   "outside": 0
  },
  "effect": "1枚分として数える。次の自分のターン開始時に女王アリと入れ替わり、同種の職アリを山札から2枚手札に加える",
  "art": "yamato_alate"
 },
 {
  "id": "winged-ie",
  "name": "羽アリ-イエ",
  "category": "シロアリ",
  "cost": 2,
  "counts": {
   "termite": 1,
   "pest": 0,
   "outside": 0
  },
  "effect": "1枚分として数える。次の自分のターン開始時に女王アリと入れ替わり、同種の職アリを山札から2枚手札に加える",
  "art": "ie_alate"
 },
 {
  "id": "winged-kanzai",
  "name": "羽アリ-カンザイ",
  "category": "シロアリ",
  "cost": 2,
  "counts": {
   "termite": 1,
   "pest": 0,
   "outside": 0
  },
  "effect": "1枚分として数える。次の自分のターン開始時に女王アリと入れ替わり、同種の職アリを山札から2枚手札に加える",
  "art": "kanzai_alate"
 },
 {
  "id": "queen-ie",
  "name": "女王アリ-イエ",
  "category": "シロアリ",
  "cost": null,
  "counts": {
   "termite": 0,
   "pest": 0,
   "outside": 1
  },
  "effect": "山札には加えないカード。1枚分として数える。成ったとき、同種の職アリを山札から2枚手札に加える（山札にない場合は空振り）",
  "art": "ie_queen"
 },
 {
  "id": "queen-kanzai",
  "name": "女王アリ-カンザイ",
  "category": "シロアリ",
  "cost": null,
  "counts": {
   "termite": 0,
   "pest": 0,
   "outside": 1
  },
  "effect": "山札には加えないカード。1枚分として数える。成ったとき、同種の職アリを山札から2枚手札に加える（山札にない場合は空振り）",
  "art": "kanzai_queen"
 },
 {
  "id": "queen-yamato",
  "name": "女王アリ-ヤマト",
  "category": "シロアリ",
  "cost": null,
  "counts": {
   "termite": 0,
   "pest": 0,
   "outside": 1
  },
  "effect": "山札には加えないカード。1枚分として数える。成ったとき、同種の職アリを山札から2枚手札に加える（山札にない場合は空振り）",
  "art": "yamato_queen"
 },
 {
  "id": "reinforce",
  "name": "増援",
  "category": "中立",
  "cost": 1,
  "counts": {
   "termite": 5,
   "pest": 7,
   "outside": 0
  },
  "effect": "自分の山札から2枚ドローする",
  "art": "reinforce"
 },
 {
  "id": "scout",
  "name": "偵察",
  "category": "中立",
  "cost": 1,
  "counts": {
   "termite": 1,
   "pest": 4,
   "outside": 0
  },
  "effect": "相手の手札を全て確認する（相手の手札がないときは使えない）",
  "art": "scout"
 },
 {
  "id": "disrupt",
  "name": "妨害",
  "category": "中立",
  "cost": 1,
  "counts": {
   "termite": 1,
   "pest": 4,
   "outside": 0
  },
  "effect": "相手の手札をランダムに1枚捨てさせる（相手の手札がないときは使えない）",
  "art": "disrupt"
 },
 {
  "id": "foresight",
  "name": "予知",
  "category": "中立",
  "cost": 1,
  "counts": {
   "termite": 2,
   "pest": 4,
   "outside": 0
  },
  "effect": "自分の山札の上3枚を見て1枚を手札に加え、残りを好きな順で山札の下に置く",
  "art": "foresight"
 },
 {
  "id": "barrier",
  "name": "バリア工法",
  "category": "防除",
  "cost": 2,
  "counts": {
   "termite": 0,
   "pest": 9,
   "outside": 0
  },
  "effect": "指定ポイントに3ターンの間、シロアリカードを置けなくする",
  "art": "barrier"
 },
 {
  "id": "spray",
  "name": "薬剤散布",
  "category": "駆除",
  "cost": 1,
  "counts": {
   "termite": 0,
   "pest": 15,
   "outside": 0
  },
  "effect": "指定ポイントのシロアリカードを1枚除去する（仮：最後に置かれたカード）",
  "art": "spray"
 },
 {
  "id": "bait",
  "name": "ベイト工法",
  "category": "駆除",
  "cost": 2,
  "counts": {
   "termite": 0,
   "pest": 7,
   "outside": 0
  },
  "effect": "置いてから2回目の自分のターン開始時に、指定ポイントのヤマト・イエシロアリをすべて除去する（カンザイは対象外）",
  "art": "bait"
 },
 {
  "id": "import-furniture",
  "name": "輸入家具購入",
  "category": "イベント（シロアリ側）",
  "cost": 1,
  "counts": {
   "termite": 3,
   "pest": 0,
   "outside": 0
  },
  "effect": "【即時】山札からカンザイシロアリを1枚手札に加える（山札にない場合は空振り。仮：山札で最初に見つかったもの）",
  "art": "import_furniture"
 },
 {
  "id": "swarm",
  "name": "大量発生",
  "category": "イベント（シロアリ側）",
  "cost": 1,
  "counts": {
   "termite": 6,
   "pest": 0,
   "outside": 0
  },
  "effect": "指定ポイントに次の自分のターンまで、防除・駆除カードを置けなくする。シロアリ側はそのポイントにカードを置ける",
  "art": "outbreak"
 }
];
