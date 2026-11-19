// 前回のスナップショットとの差分を、人が読める変更点の文にする

import type { CardRecord } from '../../src/core/types';

const FIELDS: { key: string; label: string; get: (c: CardRecord) => string }[] = [
  { key: 'category', label: '区分', get: (c) => c.category },
  { key: 'status', label: 'ステータス', get: (c) => c.status },
  { key: 'cost', label: 'コスト', get: (c) => (c.cost === null ? 'なし' : String(c.cost)) },
  { key: 'termite', label: '枚数(シロアリ山札)', get: (c) => String(c.counts.termite) },
  { key: 'pest', label: '枚数(防除山札)', get: (c) => String(c.counts.pest) },
  { key: 'outside', label: '枚数(山札外)', get: (c) => String(c.counts.outside) },
  { key: 'effect', label: '効果', get: (c) => c.effect },
  { key: 'memo', label: 'メモ', get: (c) => c.memo ?? '' },
  { key: 'favored', label: '有利な側', get: (c) => c.favored ?? '' },
  { key: 'basis', label: '根拠・元ネタ', get: (c) => c.basis ?? '' },
];

const LONG = new Set(['effect', 'memo', 'basis']);

export function diffCards(before: CardRecord[], after: CardRecord[]): string[] {
  const changes: string[] = [];
  const used = new Set<CardRecord>();
  const match = (c: CardRecord) =>
    (c.notionPageId && before.find((b) => b.notionPageId === c.notionPageId && !used.has(b))) ||
    before.find((b) => b.id === c.id && !used.has(b)) ||
    before.find((b) => b.name === c.name && !used.has(b));

  for (const c of after) {
    const b = match(c);
    if (!b) {
      changes.push(`カード『${c.name}』を追加`);
      continue;
    }
    used.add(b);
    if (b.name !== c.name) changes.push(`カード名『${b.name}』→『${c.name}』`);
    for (const f of FIELDS) {
      const x = f.get(b);
      const y = f.get(c);
      if (x === y) continue;
      if (LONG.has(f.key)) changes.push(`カード『${c.name}』の${f.label}が変更:「${x}」→「${y}」`);
      else changes.push(`カード『${c.name}』の${f.label} ${x || '(空)'}→${y || '(空)'}`);
    }
  }
  for (const b of before) if (!used.has(b)) changes.push(`カード『${b.name}』を削除`);
  return changes;
}

export interface Section {
  path: string;
  title: string;
  body: string;
}

/** Markdown を見出しで節に分ける(見出しより前の部分は「冒頭」) */
export function splitSections(md: string): Section[] {
  const sections: Section[] = [];
  const stack: string[] = [];
  let cur: Section = { path: '(冒頭)', title: '冒頭', body: '' };
  const flush = () => {
    cur.body = cur.body.replace(/^\s+|\s+$/g, '');
    sections.push(cur);
  };
  for (const line of md.split('\n')) {
    const m = line.match(/^(#{1,6})\s+(.*)$/);
    if (m) {
      flush();
      const level = m[1].length;
      stack.length = level - 1;
      stack[level - 1] = m[2].trim();
      const path = stack.filter(Boolean).join(' > ');
      cur = { path, title: m[2].trim(), body: '' };
    } else if (!line.startsWith('<!--')) {
      cur.body += line + '\n';
    }
  }
  flush();
  return sections.filter((s) => !(s.path === '(冒頭)' && s.body === ''));
}

export function diffSpec(before: string, after: string): string[] {
  const a = splitSections(before);
  const b = splitSections(after);
  const changes: string[] = [];
  const byPath = (list: Section[]) => new Map(list.map((s) => [s.path, s]));
  const am = byPath(a);
  const bm = byPath(b);
  for (const s of b) {
    const old = am.get(s.path);
    if (!old) changes.push(`仕様書:『${s.title}』節を追加`);
    else if (old.body !== s.body) changes.push(`仕様書:『${s.title}』節が変更`);
  }
  for (const s of a) if (!bm.has(s.path)) changes.push(`仕様書:『${s.title}』節を削除`);
  return changes;
}
