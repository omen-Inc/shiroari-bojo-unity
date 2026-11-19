// Notion 公式 REST API の読み取り専用クライアント(fetch で直接呼ぶ)。
// 書き込み系(POST /pages、PATCH など)は呼べないよう、許可リストで縛る。
// POST /databases/{id}/query は読み取り(検索)なので許可する。

export const NOTION_API = 'https://api.notion.com/v1';
export const NOTION_VERSION = '2022-06-28';

export type FetchLike = (url: string, init: { method: string; headers: Record<string, string>; body?: string }) => Promise<{
  ok: boolean;
  status: number;
  json(): Promise<any>;
  text(): Promise<string>;
}>;

export interface NotionBlock {
  id: string;
  type: string;
  has_children?: boolean;
  children?: NotionBlock[];
  [key: string]: any;
}

export interface NotionPage {
  id: string;
  properties: Record<string, any>;
  [key: string]: any;
}

const READ_ONLY_RULES: { method: string; pattern: RegExp }[] = [
  { method: 'GET', pattern: /^\/blocks\/[0-9a-f-]+\/children(\?.*)?$/ },
  { method: 'GET', pattern: /^\/pages\/[0-9a-f-]+$/ },
  { method: 'GET', pattern: /^\/databases\/[0-9a-f-]+$/ },
  { method: 'POST', pattern: /^\/databases\/[0-9a-f-]+\/query$/ },
];

export function assertReadOnly(method: string, path: string): void {
  if (!READ_ONLY_RULES.some((r) => r.method === method && r.pattern.test(path))) {
    throw new Error(`Notion への書き込み・未許可の呼び出しは禁止です: ${method} ${path}`);
  }
}

export class NotionReader {
  constructor(
    private token: string,
    private fetchImpl: FetchLike = fetch as unknown as FetchLike,
  ) {}

  private async request(method: 'GET' | 'POST', path: string, body?: unknown): Promise<any> {
    assertReadOnly(method, path);
    const res = await this.fetchImpl(`${NOTION_API}${path}`, {
      method,
      headers: {
        Authorization: `Bearer ${this.token}`,
        'Notion-Version': NOTION_VERSION,
        'Content-Type': 'application/json',
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!res.ok) throw new Error(`Notion API ${method} ${path} が失敗しました(HTTP ${res.status}): ${await res.text()}`);
    return res.json();
  }

  async getPage(id: string): Promise<NotionPage> {
    return this.request('GET', `/pages/${id}`);
  }

  async getChildren(id: string): Promise<NotionBlock[]> {
    const out: NotionBlock[] = [];
    let cursor: string | undefined;
    do {
      const q = new URLSearchParams({ page_size: '100' });
      if (cursor) q.set('start_cursor', cursor);
      const r = await this.request('GET', `/blocks/${id}/children?${q.toString()}`);
      out.push(...r.results);
      cursor = r.has_more ? r.next_cursor : undefined;
    } while (cursor);
    return out;
  }

  /** 子ブロックを再帰的に取得する(子ページ・子DBの中身には入らない) */
  async getBlockTree(id: string): Promise<NotionBlock[]> {
    const blocks = await this.getChildren(id);
    for (const b of blocks) {
      if (b.has_children && b.type !== 'child_page' && b.type !== 'child_database') {
        b.children = await this.getBlockTree(b.id);
      }
    }
    return blocks;
  }

  async queryDatabase(id: string): Promise<NotionPage[]> {
    const out: NotionPage[] = [];
    let cursor: string | undefined;
    do {
      const body: Record<string, unknown> = { page_size: 100 };
      if (cursor) body.start_cursor = cursor;
      const r = await this.request('POST', `/databases/${id}/query`, body);
      out.push(...r.results);
      cursor = r.has_more ? r.next_cursor : undefined;
    } while (cursor);
    return out;
  }
}
