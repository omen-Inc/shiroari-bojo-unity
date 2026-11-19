// Notion のブロック → Markdown

import type { NotionBlock } from './notion';

export interface RichText {
  plain_text?: string;
  text?: { content: string; link?: { url: string } | null };
  href?: string | null;
  annotations?: { bold?: boolean; italic?: boolean; strikethrough?: boolean; code?: boolean };
  type?: string;
  mention?: any;
}

export function richTextToMarkdown(rts: RichText[] | undefined): string {
  if (!rts) return '';
  return rts
    .map((rt) => {
      let t = rt.plain_text ?? rt.text?.content ?? '';
      if (!t) return '';
      const a = rt.annotations ?? {};
      if (a.code) t = `\`${t}\``;
      if (a.bold) t = `**${t}**`;
      if (a.italic) t = `*${t}*`;
      if (a.strikethrough) t = `~~${t}~~`;
      const href = rt.href ?? rt.text?.link?.url;
      if (href && rt.type !== 'mention') t = `[${t}](${href})`;
      return t;
    })
    .join('');
}

export function plainText(rts: RichText[] | undefined): string {
  return (rts ?? []).map((rt) => rt.plain_text ?? rt.text?.content ?? '').join('');
}

function tableToMarkdown(block: NotionBlock, indent: string): string[] {
  const rows = (block.children ?? []).filter((r) => r.type === 'table_row');
  if (rows.length === 0) return [];
  const cells = rows.map((r) => (r.table_row.cells as RichText[][]).map((c) => richTextToMarkdown(c).replace(/\|/g, '\\|').replace(/\n/g, ' ')));
  const width = Math.max(...cells.map((c) => c.length));
  const line = (c: string[]) => `${indent}| ${Array.from({ length: width }, (_, i) => c[i] ?? '').join(' | ')} |`;
  const out = [line(cells[0]), `${indent}| ${Array.from({ length: width }, () => '---').join(' | ')} |`];
  for (const c of cells.slice(1)) out.push(line(c));
  return out;
}

/** ブロック列を Markdown の行にする。段落の間は空行を入れ、リストは詰める */
export function blocksToMarkdown(blocks: NotionBlock[], depth = 0): string {
  const lines: string[] = [];
  const indent = '  '.repeat(depth);
  let prevList = false;
  let numbered = 0;

  const pushBlank = () => {
    if (depth === 0 && lines.length > 0 && lines[lines.length - 1] !== '') lines.push('');
  };

  for (const b of blocks) {
    const data = b[b.type] ?? {};
    const text = richTextToMarkdown(data.rich_text);
    const isList = ['bulleted_list_item', 'numbered_list_item', 'to_do'].includes(b.type);
    if (!isList && prevList) pushBlank();
    if (b.type !== 'numbered_list_item') numbered = 0;
    const children = b.children && b.children.length && b.type !== 'table' ? blocksToMarkdown(b.children, depth + 1) : '';

    switch (b.type) {
      case 'heading_1':
      case 'heading_2':
      case 'heading_3': {
        pushBlank();
        // 仕様書のタイトルを # にするので、本文の見出しは1段下げる
        const level = Number(b.type.slice(-1)) + 1;
        lines.push(`${'#'.repeat(level)} ${text}`);
        lines.push('');
        if (children) lines.push(children, '');
        break;
      }
      case 'paragraph':
        if (text) {
          lines.push(`${indent}${text}`);
          if (depth === 0) lines.push('');
        }
        if (children) lines.push(children);
        break;
      case 'bulleted_list_item':
        lines.push(`${indent}- ${text}`);
        if (children) lines.push(children);
        break;
      case 'numbered_list_item':
        numbered += 1;
        lines.push(`${indent}${numbered}. ${text}`);
        if (children) lines.push(children);
        break;
      case 'to_do':
        lines.push(`${indent}- [${data.checked ? 'x' : ' '}] ${text}`);
        if (children) lines.push(children);
        break;
      case 'toggle':
        lines.push(`${indent}- ${text}`);
        if (children) lines.push(children);
        break;
      case 'quote':
        lines.push(`${indent}> ${text}`);
        if (children) lines.push(children);
        if (depth === 0) lines.push('');
        break;
      case 'callout': {
        const icon = data.icon?.emoji ? `${data.icon.emoji} ` : '';
        lines.push(`${indent}> ${icon}${text}`);
        if (children) lines.push(children);
        if (depth === 0) lines.push('');
        break;
      }
      case 'code':
        lines.push(`${indent}\`\`\`${data.language ?? ''}`, ...plainText(data.rich_text).split('\n').map((l) => indent + l), `${indent}\`\`\``);
        if (depth === 0) lines.push('');
        break;
      case 'divider':
        pushBlank();
        lines.push('---', '');
        break;
      case 'table':
        pushBlank();
        lines.push(...tableToMarkdown(b, indent));
        if (depth === 0) lines.push('');
        break;
      case 'child_database':
        lines.push(`${indent}(データベース「${data.title ?? ''}」:cards.md を参照)`);
        if (depth === 0) lines.push('');
        break;
      case 'child_page':
        lines.push(`${indent}(子ページ「${data.title ?? ''}」)`);
        if (depth === 0) lines.push('');
        break;
      case 'column_list':
      case 'column':
      case 'synced_block':
        if (b.children) lines.push(blocksToMarkdown(b.children, depth));
        break;
      default:
        if (text) lines.push(`${indent}${text}`);
        if (children) lines.push(children);
    }
    prevList = isList;
  }
  return lines.join('\n').replace(/\n{3,}/g, '\n\n').replace(/\s+$/, '');
}
