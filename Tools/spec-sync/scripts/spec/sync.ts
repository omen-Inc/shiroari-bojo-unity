// 仕様書ページとカード一覧DBを読み、docs/spec/ にスナップショットを書き、前回との差分を返す

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import type { CardsFile } from '../../src/core/types';
import { cardsToMarkdown, pagesToCardsFile } from './cards';
import { diffCards, diffSpec } from './diff';
import { blocksToMarkdown, plainText } from './markdown';
import type { NotionReader } from './notion';

export const SPEC_PAGE_ID = '3f951e1703608149adfcf5adf3ebe51c';
export const CARDS_DATABASE_ID = '89587a8f5c2d4fa583151ddc8df62d42';

export interface SyncOptions {
  reader: Pick<NotionReader, 'getPage' | 'getBlockTree' | 'queryDatabase'>;
  outDir: string;
  pageId?: string;
  databaseId?: string;
}

export interface SyncResult {
  changes: string[];
  specMd: string;
  cardsMd: string;
  cards: CardsFile;
}

export function pageTitle(page: { properties?: Record<string, any> }): string {
  const props = page.properties ?? {};
  const titleProp = Object.values(props).find((p: any) => p?.type === 'title') as any;
  return titleProp ? plainText(titleProp.title) : '';
}

export function renderSpec(title: string, body: string): string {
  return [
    '<!-- 自動生成:npm run spec:sync(Notion の仕様書ページ)。正は Notion。手で編集しない -->',
    `# ${title || 'シロアリボージョ(仮)ゲーム仕様書'}`,
    '',
    body,
    '',
  ].join('\n');
}

function readIf(path: string): string | null {
  return existsSync(path) ? readFileSync(path, 'utf8') : null;
}

export async function syncSpec(opts: SyncOptions): Promise<SyncResult> {
  const pageId = opts.pageId ?? SPEC_PAGE_ID;
  const databaseId = opts.databaseId ?? CARDS_DATABASE_ID;
  const [page, blocks, rows] = await Promise.all([
    opts.reader.getPage(pageId),
    opts.reader.getBlockTree(pageId),
    opts.reader.queryDatabase(databaseId),
  ]);
  const specMd = renderSpec(pageTitle(page), blocksToMarkdown(blocks));
  const cards = pagesToCardsFile(rows, databaseId);
  const cardsMd = cardsToMarkdown(cards);

  const specPath = join(opts.outDir, 'spec.md');
  const jsonPath = join(opts.outDir, 'cards.json');
  const prevSpec = readIf(specPath);
  const prevJson = readIf(jsonPath);

  const changes: string[] = [];
  if (prevSpec === null) changes.push('仕様書:初回の取り込み');
  else changes.push(...diffSpec(prevSpec, specMd));
  if (prevJson === null) changes.push('カード一覧:初回の取り込み');
  else changes.push(...diffCards((JSON.parse(prevJson) as CardsFile).cards, cards.cards));

  mkdirSync(opts.outDir, { recursive: true });
  writeFileSync(specPath, specMd);
  writeFileSync(join(opts.outDir, 'cards.md'), cardsMd);
  writeFileSync(jsonPath, JSON.stringify(cards, null, 2) + '\n');
  return { changes, specMd, cardsMd, cards };
}
