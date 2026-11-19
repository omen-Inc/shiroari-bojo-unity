// npm run spec:sync -- --changes <path> [--out-dir <出力先>]。出力先の既定は本リポジトリの docs/spec/
// NOTION_TOKEN(環境変数)で Notion を読み取り専用で読み、docs/spec/ を更新する。Notion へは書き込まない。
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { NotionReader } from './spec/notion';
import { syncSpec } from './spec/sync';

const { values } = parseArgs({
  options: {
    changes: { type: 'string' },
    'out-dir': { type: 'string', default: resolve(dirname(fileURLToPath(import.meta.url)), '../../../docs/spec') },
  },
});

function writeChanges(obj: object) {
  if (!values.changes) return;
  mkdirSync(dirname(values.changes), { recursive: true });
  writeFileSync(values.changes, JSON.stringify(obj, null, 2) + '\n');
}

const token = process.env.NOTION_TOKEN;
if (!token) {
  console.error('NOTION_TOKEN が設定されていません。docs/spec/ は更新しませんでした');
  writeChanges({ changes: [], error: 'NOTION_TOKEN が未設定' });
  process.exit(1);
}

try {
  const result = await syncSpec({ reader: new NotionReader(token), outDir: values['out-dir']! });
  writeChanges({ changes: result.changes });
  console.log(result.changes.length ? `仕様の変更点 ${result.changes.length}件:\n- ${result.changes.join('\n- ')}` : '仕様の変更点はありません');
} catch (e) {
  const msg = e instanceof Error ? e.message : String(e);
  console.error(`spec:sync に失敗しました: ${msg}`);
  writeChanges({ changes: [], error: msg });
  process.exit(1);
}
