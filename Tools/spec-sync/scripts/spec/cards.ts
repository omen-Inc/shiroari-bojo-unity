// カード一覧DBの行 → CardRecord(cards.json)/ cards.md

import { cardIdFor } from '../../src/core/cardIds';
import type { CardRecord, CardsFile } from '../../src/core/types';
import { plainText } from './markdown';
import type { NotionPage } from './notion';

/** Notion のカード一覧DBの列名 */
export const COLUMNS = {
  name: 'カード名',
  category: '区分',
  status: 'ステータス',
  cost: 'コスト',
  termite: '枚数(シロアリ山札)',
  pest: '枚数(防除山札)',
  outside: '枚数(山札外)',
  effect: '効果',
  favored: '有利な側',
  memo: 'メモ',
  basis: '根拠・元ネタ',
} as const;

/** プロパティを文字列にする(型の違いを吸収する) */
export function propText(prop: any): string {
  if (!prop) return '';
  switch (prop.type) {
    case 'title':
      return plainText(prop.title);
    case 'rich_text':
      return plainText(prop.rich_text);
    case 'select':
      return prop.select?.name ?? '';
    case 'status':
      return prop.status?.name ?? '';
    case 'multi_select':
      return (prop.multi_select ?? []).map((o: any) => o.name).join('・');
    case 'number':
      return prop.number === null || prop.number === undefined ? '' : String(prop.number);
    case 'checkbox':
      return prop.checkbox ? 'はい' : 'いいえ';
    case 'formula': {
      const f = prop.formula ?? {};
      return f[f.type] === null || f[f.type] === undefined ? '' : String(f[f.type]);
    }
    default:
      return '';
  }
}

/** 数値に直す。「なし」や空は null */
export function parseNumber(text: string): number | null {
  const m = text.replace(/[０-９]/g, (c) => String.fromCharCode(c.charCodeAt(0) - 0xfee0)).match(/-?\d+(\.\d+)?/);
  return m ? Number(m[0]) : null;
}

function findProp(props: Record<string, any>, name: string): any {
  if (props[name]) return props[name];
  // 括弧の全角・半角の違いを吸収する
  const norm = (s: string) => s.replace(/[（(]/g, '(').replace(/[）)]/g, ')').replace(/\s/g, '');
  const key = Object.keys(props).find((k) => norm(k) === norm(name));
  return key ? props[key] : undefined;
}

export function pageToCard(page: NotionPage): CardRecord {
  const p = page.properties;
  const t = (col: string) => propText(findProp(p, col)).trim();
  const name = t(COLUMNS.name);
  const rec: CardRecord = {
    id: cardIdFor(name),
    name,
    category: t(COLUMNS.category),
    status: t(COLUMNS.status),
    cost: parseNumber(t(COLUMNS.cost)),
    counts: {
      termite: parseNumber(t(COLUMNS.termite)) ?? 0,
      pest: parseNumber(t(COLUMNS.pest)) ?? 0,
      outside: parseNumber(t(COLUMNS.outside)) ?? 0,
    },
    effect: t(COLUMNS.effect),
    favored: t(COLUMNS.favored),
    memo: t(COLUMNS.memo),
    basis: t(COLUMNS.basis),
    notionPageId: page.id.replace(/-/g, ''),
  };
  return rec;
}

const CATEGORY_ORDER = ['シロアリ', 'イベント(シロアリ側)', '中立', '防除', '駆除'];
const ID_ORDER = [
  'worker-yamato', 'worker-ie', 'worker-kanzai',
  'soldier-yamato', 'soldier-ie', 'soldier-kanzai',
  'winged-yamato', 'winged-ie', 'winged-kanzai', 'queen',
  'import-furniture', 'swarm', 'reinforce', 'scout', 'disrupt', 'foresight',
  'barrier', 'spray', 'bait',
];

/** Notion の返す順は安定しないので、区分 → 既知の並び → 名前の順に並べる */
export function sortCards(cards: CardRecord[]): CardRecord[] {
  const cat = (c: CardRecord) => {
    const i = CATEGORY_ORDER.indexOf(c.category);
    return i < 0 ? CATEGORY_ORDER.length : i;
  };
  const idx = (c: CardRecord) => {
    const i = ID_ORDER.indexOf(c.id);
    return i < 0 ? ID_ORDER.length : i;
  };
  return cards.slice().sort((a, b) => cat(a) - cat(b) || idx(a) - idx(b) || a.name.localeCompare(b.name, 'ja'));
}

export function pagesToCardsFile(pages: NotionPage[], databaseId: string): CardsFile {
  return {
    source: { databaseId, note: 'Notion のカード一覧DBの写し。npm run spec:sync が生成する。手で編集しない' },
    cards: sortCards(pages.map(pageToCard).filter((c) => c.name)),
  };
}

const cell = (s: string | number | null | undefined) => String(s ?? '').replace(/\|/g, '\\|').replace(/\n/g, ' ');

export function cardsToMarkdown(file: CardsFile): string {
  const lines = [
    '<!-- 自動生成:npm run spec:sync(Notion のカード一覧DB)。手で編集しない -->',
    '# カード一覧',
    '',
    '枚数 = シロアリ山札 / 防除山札 / 山札外。ステータス「没」も含む(エンジンは「没」を使わない)。',
    '',
    '| ID | カード名 | 区分 | ステータス | コスト | 枚数 | 効果 | メモ |',
    '| --- | --- | --- | --- | --- | --- | --- | --- |',
  ];
  for (const c of file.cards) {
    const counts = `${c.counts.termite}/${c.counts.pest}/${c.counts.outside || '-'}`;
    lines.push(`| ${cell(c.id)} | ${cell(c.name)} | ${cell(c.category)} | ${cell(c.status)} | ${c.cost ?? 'なし'} | ${counts} | ${cell(c.effect)} | ${cell(c.memo)} |`);
  }
  const live = file.cards.filter((c) => c.status !== '没');
  const sum = (k: 'termite' | 'pest') => live.reduce((a, c) => a + c.counts[k], 0);
  lines.push('', `山札の合計(「没」を除く):シロアリ ${sum('termite')}枚、防除 ${sum('pest')}枚。`);
  return lines.join('\n') + '\n';
}
