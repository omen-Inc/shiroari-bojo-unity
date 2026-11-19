import { mkdtempSync, readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { cardsToMarkdown, pageToCard, pagesToCardsFile, parseNumber } from '../../scripts/spec/cards';
import { diffCards, diffSpec, splitSections } from '../../scripts/spec/diff';
import { blocksToMarkdown, richTextToMarkdown } from '../../scripts/spec/markdown';
import { assertReadOnly, NotionReader, NOTION_VERSION } from '../../scripts/spec/notion';
import { syncSpec } from '../../scripts/spec/sync';
import { block, cardPages, fakeNotionFetch, rt, specBlocks } from './fixtures';

const strip = (blocks: any[]): any[] => blocks.map(({ __children, ...b }) => ({ ...b, children: __children ? strip(__children) : undefined }));

describe('Markdown 変換', () => {
  it('装飾付きテキスト', () => {
    expect(richTextToMarkdown([rt('太字', { bold: true }), rt('と'), rt('コード', { code: true }), rt('リンク', {}, 'https://example.com')])).toBe(
      '**太字**と`コード`[リンク](https://example.com)',
    );
  });

  it('見出し・入れ子のリスト・表・子DB', () => {
    const md = blocksToMarkdown(strip(specBlocks()));
    expect(md).toContain('## 概要\n\n- CPU対戦カードゲーム');
    expect(md).toContain('### 補充\n\n- ターンの終わりに、手札が5枚になるまで自分の山札から引く\n  - 5枚を超えている場合は引かない');
    expect(md).toContain('| ポイント | 置ける種類 |\n| --- | --- |\n| キッチン | ヤマト・イエ |');
    expect(md).toContain('(データベース「カード一覧」:cards.md を参照)');
  });

  it('番号付きリストと to_do', () => {
    const md = blocksToMarkdown([
      block('numbered_list_item', 'a'),
      block('numbered_list_item', 'b'),
      block('to_do', '未決', { checked: false }),
      block('to_do', '決定', { checked: true }),
    ]);
    expect(md).toBe('1. a\n2. b\n- [ ] 未決\n- [x] 決定');
  });
});

describe('カード一覧の変換', () => {
  it('DB の行を CardRecord にする', () => {
    const c = pageToCard(cardPages()[0]);
    expect(c).toMatchObject({ id: 'spray', name: '薬剤散布', category: '駆除', status: 'たたき台', cost: 1, counts: { termite: 0, pest: 15, outside: 0 } });
    expect(c.notionPageId).toBe('aaaaaaaa000000000000000000000002');
  });

  it('コスト「なし」は null、女王アリは queen', () => {
    const q = pageToCard(cardPages()[2]);
    expect(q).toMatchObject({ id: 'queen', cost: null, counts: { outside: 1 } });
    expect(parseNumber('なし')).toBeNull();
    expect(parseNumber('２')).toBe(2);
  });

  it('並びは区分順で安定する', () => {
    const file = pagesToCardsFile(cardPages(), 'db');
    expect(file.cards.map((c) => c.id)).toEqual(['worker-ie', 'queen', 'spray']);
    expect(cardsToMarkdown(file)).toContain('| spray | 薬剤散布 | 駆除 | たたき台 | 1 | 0/15/- |');
  });
});

describe('差分', () => {
  it('カードの数値・追加・削除・改名', () => {
    const before = pagesToCardsFile(cardPages(1), 'db').cards;
    const after = pagesToCardsFile(cardPages(2), 'db').cards;
    expect(diffCards(before, after)).toEqual(['カード『薬剤散布』のコスト 1→2']);

    const renamed = after.map((c) => (c.id === 'spray' ? { ...c, name: '殺虫剤', id: 'card-x' } : c));
    expect(diffCards(after, renamed)).toEqual(['カード名『薬剤散布』→『殺虫剤』']);
    expect(diffCards(after, after.slice(1))).toEqual(['カード『職アリ-イエ』を削除']);
    expect(diffCards(after.slice(1), after)).toEqual(['カード『職アリ-イエ』を追加']);
  });

  it('効果文の変更', () => {
    const before = pagesToCardsFile(cardPages(), 'db').cards;
    const after = before.map((c) => (c.id === 'spray' ? { ...c, effect: '2枚除去する' } : c));
    expect(diffCards(before, after)).toEqual(['カード『薬剤散布』の効果が変更:「指定ポイントのシロアリカードを1枚除去する」→「2枚除去する」']);
  });

  it('仕様書の節ごとの変更', () => {
    const a = '# 仕様\n\n## 基本ルール\n### 補充\n- 5枚\n### 勝利\n- 5ポイント\n';
    const b = '# 仕様\n\n## 基本ルール\n### 補充\n- 6枚\n### 手札上限\n- 7枚\n';
    expect(splitSections(a).map((s) => s.path)).toEqual(['仕様', '仕様 > 基本ルール', '仕様 > 基本ルール > 補充', '仕様 > 基本ルール > 勝利']);
    expect(diffSpec(a, b)).toEqual(['仕様書:『補充』節が変更', '仕様書:『手札上限』節を追加', '仕様書:『勝利』節を削除']);
    expect(diffSpec(a, a)).toEqual([]);
  });
});

describe('Notion クライアント(読み取り専用)', () => {
  it('書き込み系の呼び出しは拒否する', () => {
    expect(() => assertReadOnly('POST', '/pages')).toThrow(/禁止/);
    expect(() => assertReadOnly('PATCH', '/pages/abc')).toThrow(/禁止/);
    expect(() => assertReadOnly('PATCH', '/blocks/abc/children')).toThrow(/禁止/);
    expect(() => assertReadOnly('DELETE', '/blocks/abc')).toThrow(/禁止/);
    expect(() => assertReadOnly('POST', '/databases/89587a8f/query')).not.toThrow();
    expect(() => assertReadOnly('GET', '/blocks/3f95/children?page_size=100')).not.toThrow();
  });

  it('ヘッダとページ送り', async () => {
    const calls: any[] = [];
    const reader = new NotionReader('secret', fakeNotionFetch({ title: 'T', blocks: specBlocks() }, cardPages(), calls) as any);
    const tree = await reader.getBlockTree('3f951e1703608149adfcf5adf3ebe51c');
    expect(tree).toHaveLength(specBlocks().length);
    const refill = tree.find((b) => b.type === 'bulleted_list_item' && b.children?.length);
    expect(refill?.children?.[0].bulleted_list_item.rich_text[0].plain_text).toBe('5枚を超えている場合は引かない');
    const rows = await reader.queryDatabase('89587a8f5c2d4fa583151ddc8df62d42');
    expect(rows).toHaveLength(3);
    expect(calls.every((c) => c.headers['Notion-Version'] === NOTION_VERSION && c.headers.Authorization === 'Bearer secret')).toBe(true);
    expect(calls.every((c) => c.method === 'GET' || c.url.endsWith('/query'))).toBe(true);
  });
});

describe('syncSpec', () => {
  it('初回はスナップショットを書き、2回目は差分を返す', async () => {
    const dir = mkdtempSync(join(tmpdir(), 'spec-'));
    const reader = (refill: string, sprayCost: number) =>
      new NotionReader('t', fakeNotionFetch({ title: 'シロアリボージョ(仮)ゲーム仕様書', blocks: specBlocks(refill) }, cardPages(sprayCost)) as any);

    const first = await syncSpec({ reader: reader('ターンの終わりに5枚まで引く', 1), outDir: dir });
    expect(first.changes).toEqual(['仕様書:初回の取り込み', 'カード一覧:初回の取り込み']);
    const spec = readFileSync(join(dir, 'spec.md'), 'utf8');
    expect(spec).toContain('# シロアリボージョ(仮)ゲーム仕様書');
    expect(spec).toContain('### 補充');
    expect(JSON.parse(readFileSync(join(dir, 'cards.json'), 'utf8')).cards).toHaveLength(3);
    expect(readFileSync(join(dir, 'cards.md'), 'utf8')).toContain('薬剤散布');

    const same = await syncSpec({ reader: reader('ターンの終わりに5枚まで引く', 1), outDir: dir });
    expect(same.changes).toEqual([]);

    const second = await syncSpec({ reader: reader('ターンの終わりに6枚まで引く', 2), outDir: dir });
    expect(second.changes).toEqual(['仕様書:『補充』節が変更', 'カード『薬剤散布』のコスト 1→2']);
  });
});
