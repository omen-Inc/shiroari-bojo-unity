// Notion API の応答を模したテスト用データ(2022-06-28 形式)
import type { NotionBlock, NotionPage } from '../../scripts/spec/notion';

export const rt = (text: string, annotations: Record<string, boolean> = {}, href: string | null = null) => ({
  type: 'text',
  plain_text: text,
  text: { content: text, link: href ? { url: href } : null },
  href,
  annotations: { bold: false, italic: false, strikethrough: false, underline: false, code: false, color: 'default', ...annotations },
});

let n = 0;
const id = () => `00000000-0000-0000-0000-${String(++n).padStart(12, '0')}`;

export const block = (type: string, text: string, extra: Record<string, unknown> = {}, children?: NotionBlock[]): NotionBlock => ({
  object: 'block',
  id: id(),
  type,
  has_children: !!children?.length,
  [type]: { rich_text: [rt(text)], ...extra },
  ...(children ? { __children: children } : {}),
});

export function specBlocks(refillText = 'ターンの終わりに、手札が5枚になるまで自分の山札から引く'): NotionBlock[] {
  return [
    block('heading_1', '概要'),
    block('bulleted_list_item', 'CPU対戦カードゲーム'),
    block('heading_1', '基本ルール'),
    block('heading_2', '補充'),
    block('bulleted_list_item', refillText, {}, [block('bulleted_list_item', '5枚を超えている場合は引かない')]),
    block('heading_2', '侵食と勝利条件'),
    block('paragraph', '3枚で完全侵食'),
    {
      object: 'block',
      id: id(),
      type: 'table',
      has_children: true,
      table: { table_width: 2, has_column_header: true },
      __children: [
        { object: 'block', id: id(), type: 'table_row', has_children: false, table_row: { cells: [[rt('ポイント')], [rt('置ける種類')]] } },
        { object: 'block', id: id(), type: 'table_row', has_children: false, table_row: { cells: [[rt('キッチン')], [rt('ヤマト・イエ')]] } },
      ],
    },
    { object: 'block', id: id(), type: 'child_database', has_children: false, child_database: { title: 'カード一覧' } },
  ];
}

export function cardPage(opts: {
  pageId: string;
  name: string;
  category: string;
  status?: string;
  cost: number | null;
  termite: number;
  pest: number;
  outside?: number | null;
  effect: string;
}): NotionPage {
  return {
    object: 'page',
    id: opts.pageId,
    properties: {
      カード名: { id: 'title', type: 'title', title: [rt(opts.name)] },
      区分: { id: 'a', type: 'select', select: { name: opts.category } },
      ステータス: { id: 'b', type: 'status', status: { name: opts.status ?? '確定' } },
      コスト: { id: 'c', type: 'number', number: opts.cost },
      '枚数(シロアリ山札)': { id: 'd', type: 'number', number: opts.termite },
      '枚数(防除山札)': { id: 'e', type: 'number', number: opts.pest },
      '枚数(山札外)': { id: 'f', type: 'number', number: opts.outside ?? null },
      効果: { id: 'g', type: 'rich_text', rich_text: [rt(opts.effect)] },
      有利な側: { id: 'h', type: 'select', select: null },
      メモ: { id: 'i', type: 'rich_text', rich_text: [] },
      '根拠・元ネタ': { id: 'j', type: 'rich_text', rich_text: [] },
    },
  };
}

export function cardPages(sprayCost = 1): NotionPage[] {
  return [
    cardPage({ pageId: 'aaaaaaaa-0000-0000-0000-000000000002', name: '薬剤散布', category: '駆除', status: 'たたき台', cost: sprayCost, termite: 0, pest: 15, effect: '指定ポイントのシロアリカードを1枚除去する' }),
    cardPage({ pageId: 'aaaaaaaa-0000-0000-0000-000000000001', name: '職アリ-イエ', category: 'シロアリ', cost: 1, termite: 12, pest: 0, effect: '1枚分として数える' }),
    cardPage({ pageId: 'aaaaaaaa-0000-0000-0000-000000000003', name: '女王アリ-ヤマト/イエ/カンザイ', category: 'シロアリ', cost: null, termite: 0, pest: 0, outside: 1, effect: '山札に加えない' }),
  ];
}

/** ブロックツリーを、children API の応答を返す偽 fetch にする */
export function fakeNotionFetch(page: { title: string; blocks: NotionBlock[] }, rows: NotionPage[], calls: { method: string; url: string; headers: Record<string, string>; body?: string }[] = []) {
  const byParent = new Map<string, NotionBlock[]>();
  const index = (parent: string, blocks: NotionBlock[]) => {
    byParent.set(parent, blocks.map(({ __children, ...b }) => b as NotionBlock));
    for (const b of blocks) if (b.__children) index(b.id, b.__children);
  };
  const PAGE = '3f951e1703608149adfcf5adf3ebe51c';
  index(PAGE, page.blocks);

  return async (url: string, init: { method: string; headers: Record<string, string>; body?: string }) => {
    calls.push({ method: init.method, url, headers: init.headers, body: init.body });
    const u = new URL(url);
    const path = u.pathname.replace('/v1', '');
    let json: unknown;
    let m: RegExpMatchArray | null;
    if (init.method === 'GET' && (m = path.match(/^\/blocks\/([^/]+)\/children$/))) {
      const all = byParent.get(m[1]) ?? [];
      // 2件ずつのページングで返す(ページ送りの確認用)
      const start = Number(u.searchParams.get('start_cursor') ?? 0);
      const results = all.slice(start, start + 2);
      const more = start + 2 < all.length;
      json = { object: 'list', results, has_more: more, next_cursor: more ? String(start + 2) : null };
    } else if (init.method === 'GET' && path === `/pages/${PAGE}`) {
      json = { object: 'page', id: PAGE, properties: { title: { type: 'title', title: [rt(page.title)] } } };
    } else if (init.method === 'POST' && path.endsWith('/query')) {
      const body = JSON.parse(init.body ?? '{}');
      const start = Number(body.start_cursor ?? 0);
      const results = rows.slice(start, start + 2);
      const more = start + 2 < rows.length;
      json = { object: 'list', results, has_more: more, next_cursor: more ? String(start + 2) : null };
    } else {
      return { ok: false, status: 404, json: async () => ({}), text: async () => 'not found' };
    }
    return { ok: true, status: 200, json: async () => json, text: async () => JSON.stringify(json) };
  };
}
