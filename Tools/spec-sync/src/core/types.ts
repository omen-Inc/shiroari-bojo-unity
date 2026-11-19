// 同期スクリプトが使う型(Web 版 shiroari-bojo の src/core/types.ts から必要な部分だけ)

export type Species = 'yamato' | 'ie' | 'kanzai';
export type Caste = 'worker' | 'soldier' | 'winged' | 'queen';

/** docs/spec/cards.json の1行 */
export interface CardRecord {
  id: string;
  name: string;
  category: string;
  status: string;
  cost: number | null;
  counts: { termite: number; pest: number; outside: number };
  effect: string;
  favored?: string;
  memo?: string;
  basis?: string;
  notionPageId?: string;
}

export interface CardsFile {
  source?: { databaseId?: string; note?: string };
  cards: CardRecord[];
}
