// カード名 → 安定ID の対応表。エンジンの効果の実装はこの ID で対応づける。
// Notion でカード名が変わったら、ここに旧名・新名の両方を書けば ID は変わらない。
// spec:sync(scripts/spec/)もこの表を使って cards.json の id を決める。

import type { Caste, Species } from './types';

export interface CardIdentity {
  id: string;
  species?: Species;
  caste?: Caste;
}

const SPECIES: Record<string, Species> = { ヤマト: 'yamato', イエ: 'ie', カンザイ: 'kanzai' };
const CASTES: Record<string, Caste> = { 職アリ: 'worker', 兵アリ: 'soldier', 羽アリ: 'winged', 女王アリ: 'queen' };

const FIXED: Record<string, string> = {
  '女王アリ-ヤマト/イエ/カンザイ': 'queen',
  輸入家具購入: 'import-furniture',
  大量発生: 'swarm',
  増援: 'reinforce',
  偵察: 'scout',
  妨害: 'disrupt',
  予知: 'foresight',
  バリア工法: 'barrier',
  薬剤散布: 'spray',
  ベイト工法: 'bait',
};

function normalize(name: string): string {
  return name.trim().replace(/[‐－―−–—]/g, '-').replace(/\s+/g, '');
}

function hashName(name: string): string {
  let h = 2166136261;
  for (let i = 0; i < name.length; i++) {
    h ^= name.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return (h >>> 0).toString(16).padStart(8, '0');
}

export function identifyCard(rawName: string): CardIdentity {
  const name = normalize(rawName);
  if (FIXED[name]) {
    return name === '女王アリ-ヤマト/イエ/カンザイ' ? { id: 'queen', caste: 'queen' } : { id: FIXED[name] };
  }
  const m = name.match(/^(職アリ|兵アリ|羽アリ|女王アリ)-(ヤマト|イエ|カンザイ)$/);
  if (m) {
    const caste = CASTES[m[1]];
    const species = SPECIES[m[2]];
    return { id: `${caste}-${species}`, caste, species };
  }
  // 対応表にないカード(Notion で新しく追加されたもの)。名前から決まるので同名なら同じ ID になる
  return { id: `card-${hashName(name)}` };
}

export function cardIdFor(name: string): string {
  return identifyCard(name).id;
}
