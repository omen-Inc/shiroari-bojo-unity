// シロアリボージョ UI 試作。配置・操作の流れ・演出を確かめるためのもので、ルールは正確ではない。
// 仮:使えるかどうか・効果の処理は、試作用の簡易版。Unity では Engine.HandOptions / Engine.Apply を使い、画面側に判定を書かない。
// 演出の時間(ms)とイージングは T と EASE にまとめた。design/ui/README.md の「演出の一覧」と同じ値。
'use strict';

// ───────── 素材のパス(worktree の中の相対パス。コピーしない) ─────────
const ART = '../../../Assets/Art/Cards/Illustrations/';
const BACK = '../../../Assets/Art/Cards/back_common.png';

// ───────── 演出の時間(ms)。DOTween に移すときは秒にする ─────────
const T = {
  press: 60, release: 180,
  screenFade: 300,
  deal: 350, dealStagger: 80,
  draw: 350, drawStagger: 100, flip: 150,
  relayout: 250,
  lift: 150, detailOpen: 250, detailClose: 200, shade: 200,
  snapBack: 300, shake: 300,
  playFly: 300, playHold: 200, playPop: 200,
  slotPop: 250, sprayMist: 500, guardFlash: 400, badgeDrop: 350, destroyShake: 400, stamp: 250, treeFall: 500,
  apSpend: 200, apRefill: 250, apStagger: 80,
  punch: 250,
  bannerIn: 250, bannerHold: 600, bannerOut: 200,
  cpuThink: 500, cpuLift: 200, cpuFly: 300, cpuShow: 700, cpuGap: 400,
  sheetIn: 300, sheetOut: 200,
  logIn: 250, ticker: 1800,
  coin: 900,
  verdictIn: 500, verdictHold: 1600, confetti: 1500,
};
// DOTween の Ease に対応する cubic-bezier(近似)
const EASE = {
  Linear: 'linear',
  OutQuad: 'cubic-bezier(.25,.46,.45,.94)', InQuad: 'cubic-bezier(.55,.085,.68,.53)',
  OutCubic: 'cubic-bezier(.215,.61,.355,1)', InCubic: 'cubic-bezier(.55,.055,.675,.19)',
  InOutQuad: 'cubic-bezier(.455,.03,.515,.955)', InOutSine: 'cubic-bezier(.445,.05,.55,.95)',
  OutBack: 'cubic-bezier(.175,.885,.32,1.275)', InBack: 'cubic-bezier(.6,-.28,.735,.045)',
};
let SPEED = 1; // 1 = 等速。デバッグの「×0.5」で遅くする
const D = ms => ms / SPEED;
const sleep = ms => new Promise(r => setTimeout(r, D(ms)));
function anim(el, frames, ms, ease = 'OutCubic', opt = {}) {
  if (!el) return Promise.resolve();
  const a = el.animate(frames, Object.assign({ duration: D(ms), easing: EASE[ease] || ease, fill: 'forwards' }, opt));
  return a.finished.catch(() => {});
}
const $ = (q, r = document) => r.querySelector(q);
const $$ = (q, r = document) => [...r.querySelectorAll(q)];
const h = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const rnd = n => Math.floor(Math.random() * n);
const pickOne = a => a[rnd(a.length)];

// ───────── 枠の拡大縮小(1080×1920 を画面に収める) ─────────
const stage = $('#stage');
let SCALE = 1;
function fit() {
  const vp = $('#viewport');
  SCALE = Math.min(vp.clientWidth / 1080, vp.clientHeight / 1920);
  stage.style.transform = `scale(${SCALE})`;
  stage.style.left = ((vp.clientWidth - 1080 * SCALE) / 2) + 'px';
  stage.style.top = ((vp.clientHeight - 1920 * SCALE) / 2) + 'px';
}
window.addEventListener('resize', fit);
function toStage(e) { const r = stage.getBoundingClientRect(); return { x: (e.clientX - r.left) / SCALE, y: (e.clientY - r.top) / SCALE }; }
function rectOf(el) {
  const s = stage.getBoundingClientRect(), r = el.getBoundingClientRect();
  return { x: (r.left - s.left) / SCALE, y: (r.top - s.top) / SCALE, w: r.width / SCALE, h: r.height / SCALE };
}

// ───────── カードデータ ─────────
const DB = {}; for (const c of window.CARDS) DB[c.id] = c;
const SPECIES = { yamato: 'ヤマト', ie: 'イエ', kanzai: 'カンザイ' };
const CASTE = { worker: '職', soldier: '兵', winged: '羽', queen: '女王' };
const isTermite = c => c.category === 'シロアリ';
const speciesOf = c => isTermite(c) ? c.id.split('-')[1] : null;
const casteOf = c => isTermite(c) ? c.id.split('-')[0] : null;
function themeOf(c) {
  if (isTermite(c)) return 't-' + speciesOf(c);
  if (c.category === '防除') return 't-boujo';
  if (c.category === '駆除') return 't-kujo';
  if (c.category.startsWith('イベント')) return 't-event';
  return 't-neutral';
}
const displayName = c => isTermite(c) ? c.name.split('-')[0] : c.name;                 // CardLabels.DisplayName
const typeLabel = c => isTermite(c) ? SPECIES[speciesOf(c)] + 'シロアリ' : (c.category.startsWith('イベント') ? 'イベント' : c.category); // CardLabels.TypeLabel
const costLabel = c => c.cost == null ? '−' : String(c.cost);                           // CardLabels.Cost
const kindLabel = c => isTermite(c) ? 'シロアリ' : typeLabel(c);
const sideOfCard = c => (isTermite(c) || c.category.startsWith('イベント')) ? 'termite' : c.category === '中立' ? 'both' : 'pest';
const SIDE_LABEL = { termite: 'シロアリ側', pest: '防除業者側' };
const other = s => s === 'termite' ? 'pest' : 'termite';

function smallCard(id) {
  const c = DB[id];
  const e = h('div', 'bpop-s ' + themeOf(c));
  e.innerHTML = `<div class="cost">${costLabel(c)}</div><div class="name">${displayName(c)}</div>` +
    `<div class="art">${c.art ? `<img src="${ART}${c.art}.png" alt="">` : ''}</div><div class="type"><span>${typeLabel(c)}</span></div>`;
  return e;
}
function fullCard(id, scale) {
  const c = DB[id];
  const e = h('div', 'bpop ' + themeOf(c));
  e.innerHTML = `<div class="cost"><span>${costLabel(c)}</span></div><div class="name"><span>${displayName(c)}</span></div>` +
    `<div class="art">${c.art ? `<img src="${ART}${c.art}.png" alt="">` : ''}</div><div class="type"><span>${typeLabel(c)}</span></div>` +
    `<div class="effect"><p>${c.effect}</p></div>`;
  e.style.transform = `scale(${scale})`;
  return e;
}
function backCard(w, hgt) { const e = h('div', 'cardback'); e.style.width = w + 'px'; e.style.height = hgt + 'px'; e.innerHTML = `<img src="${BACK}" alt="">`; return e; }

// ───────── ポイント(Points.cs と同じ並び。表示位置は家の断面図) ─────────
const POINTS = [
  { name: '浴室・洗面所', zone: 'wet', allowed: ['yamato', 'ie'] },
  { name: 'キッチン', zone: 'wet', allowed: ['yamato', 'ie'] },
  { name: '床下・土台', zone: 'dry', allowed: ['ie'] },
  { name: '玄関・框', zone: 'dry', allowed: ['ie'] },
  { name: '庭の枯れ木', zone: 'dry', tree: true, allowed: ['ie'] },
  { name: 'ウッドデッキ', zone: 'dry', allowed: ['ie'] },
  { name: '梁・柱', zone: 'upper', allowed: ['ie', 'kanzai'] },
  { name: '屋根裏・天井', zone: 'upper', allowed: ['ie', 'kanzai'] },
];
// 盤面の中の位置(#field の中の px)。上から 上部 / 水回り / 乾燥箇所 / 屋外
const TILE_POS = { 7: [10, 108], 6: [546, 108], 0: [10, 286], 1: [546, 286], 3: [10, 464], 2: [546, 464], 5: [10, 638], 4: [546, 638] };
const ZONE_LABEL = { wet: '水回り', dry: '乾燥箇所', upper: '上部' };
const ZONE_COLOR = { wet: 'var(--zone-wet)', dry: 'var(--zone-dry)', upper: 'var(--zone-upper)' };
const SP_COLOR = { yamato: 'var(--sp-yamato)', ie: 'var(--sp-ie)', kanzai: 'var(--sp-kanzai)' };

// ───────── 状態(試作用) ─────────
let S = null;
let uidSeq = 1;
function newState(human) {
  return {
    human, cpu: other(human), turn: 1, current: null, ap: 0, maxAp: 3,
    hand: [], deck: { [human]: 50, [other(human)]: 50 }, discard: { termite: 0, pest: 0 }, oppHand: 0,
    points: POINTS.map(() => ({ cards: [], barrier: 0, bait: 0, swarm: false, destroyed: false })),
    destroyed: 0, treeFallen: false, used: 0, placed: new Set(), log: [], over: null, busy: false, sel: null, mode: null,
  };
}
const req = i => S.treeFallen ? 2 : POINTS[i].tree ? 4 : 3;
const canAct = () => S && !S.over && !S.busy && S.current === 'me';
function drawId(side) {
  const pool = [];
  for (const c of window.CARDS) { const n = c.counts[side] || 0; for (let k = 0; k < n; k++) pool.push(c.id); }
  return pickOne(pool);
}
function log(side, text) { S.log.push({ side, text, turn: S.turn }); renderLog(); }

// 仮:試作用の簡易判定(Unity では Engine.HandOptions の Usable・Reason・Targets を使う)
function options(card) {
  const c = DB[card.id];
  const o = { usable: false, reason: null, targets: [], needsTarget: false };
  const s = S.human;
  if (!canAct()) { o.reason = '相手のターンです'; return o; }
  if (c.cost == null) { o.reason = '手札から使えないカードです'; return o; }
  const user = sideOfCard(c);
  if (user !== 'both' && user !== s) { o.reason = '相手の陣営のカードです'; return o; }
  if (c.cost > S.ap) { o.reason = '行動力が足りません'; return o; }
  if ((c.id === 'scout' || c.id === 'disrupt') && S.oppHand === 0) { o.reason = '相手の手札がありません'; return o; }
  if (['foresight', 'reinforce', 'import-furniture'].includes(c.id) && S.deck[s] === 0) { o.reason = '山札がありません'; return o; }
  const P = S.points;
  let t = null;
  if (isTermite(c)) t = P.map((p, i) => i).filter(i => !P[i].destroyed && P[i].barrier === 0 && !S.placed.has(i) && POINTS[i].allowed.includes(speciesOf(c)));
  else if (c.id === 'spray') t = P.map((p, i) => i).filter(i => !P[i].destroyed && !P[i].swarm && P[i].cards.length > 0);
  else if (c.id === 'barrier') t = P.map((p, i) => i).filter(i => !P[i].destroyed && !P[i].swarm && P[i].barrier === 0);
  else if (c.id === 'bait') t = P.map((p, i) => i).filter(i => !P[i].destroyed && !P[i].swarm && P[i].bait === 0);
  else if (c.id === 'swarm') t = P.map((p, i) => i).filter(i => !P[i].destroyed && !P[i].swarm);
  if (t) { o.needsTarget = true; o.targets = t; if (!t.length) { o.reason = '使える場所がありません'; return o; } }
  o.usable = true; return o;
}

// ───────── 画面の切り替え ─────────
function show(id) {
  $$('.screen').forEach(s => s.classList.toggle('on', s.id === id));
  anim($('#' + id), [{ opacity: 0 }, { opacity: 1 }], T.screenFade, 'InOutQuad');
}
function showSelect() { S = null; closeAllOverlays(); show('screen-select'); }

// ───────── 描画 ─────────
function renderAll() { renderOpp(); renderGoals(); renderField(); renderInfo(); renderMe(); renderHand(); }

function renderOpp() {
  const cpu = S.cpu;
  $('#opp .avatar').style.background = `var(--${cpu})`;
  const chip = $('#opp .who .chip'); chip.className = 'chip ' + cpu; chip.textContent = SIDE_LABEL[cpu];
  $('#opp .stats').innerHTML = `山札 <b>${S.deck[cpu]}</b> ・ 手札 <b>${S.oppHand}</b> ・ 捨て札 <b>${S.discard[cpu]}</b>`;
  $('#opp .thinking').classList.toggle('on', !S.over && S.current === 'cpu');
  const box = $('#opp-hand'); box.innerHTML = '';
  const n = S.oppHand, mid = (n - 1) / 2, sp = n > 1 ? Math.min(46, 250 / (n - 1)) : 0;
  for (let i = 0; i < n; i++) {
    const b = h('div', 'back'); b.innerHTML = `<img src="${BACK}" alt="">`;
    b.style.left = (125 + (i - mid) * sp) + 'px'; b.style.top = (20 + Math.abs(i - mid) * 4) + 'px';
    b.style.transform = `rotate(${(i - mid) * -4}deg)`; box.appendChild(b);
  }
}
let lastGoal = {};
function renderGoals() {
  const pips = $('#goals .pips'); pips.innerHTML = '';
  for (let k = 0; k < 5; k++) pips.appendChild(h('span', 'pip' + (k < S.destroyed ? ' on' : '')));
  const d = $('#g-destroyed'), dk = $('#g-deck');
  d.textContent = `${S.destroyed}/5`;
  const td = S.deck.termite; dk.textContent = td;
  $('#g-turn').textContent = `ターン ${S.turn}`;
  if (lastGoal.d != null && lastGoal.d !== S.destroyed) punch(d);
  if (lastGoal.t != null && lastGoal.t !== td) punch(dk);
  lastGoal = { d: S.destroyed, t: td };
}
function punch(el) { return anim(el, [{ transform: 'scale(1)' }, { transform: 'scale(1.35)', color: 'var(--danger)' }, { transform: 'scale(1)' }], T.punch, 'OutQuad', { fill: 'none' }); }

function tileEl(i) { return $(`#field .tile[data-i="${i}"]`); }
function renderField(targets = []) {
  const field = $('#field');
  for (let i = 0; i < 8; i++) {
    let t = tileEl(i);
    const def = POINTS[i], p = S.points[i];
    if (!t) {
      t = h('div', 'tile'); t.dataset.i = i;
      t.style.left = TILE_POS[i][0] + 'px'; t.style.top = TILE_POS[i][1] + 'px';
      t.addEventListener('click', () => onTileTap(i));
      field.appendChild(t);
    }
    const color = def.tree ? 'var(--zone-tree)' : ZONE_COLOR[def.zone];
    t.className = 'tile' + (def.tree ? ' tree' : '') + (p.destroyed ? ' destroyed' : '') + (targets.includes(i) ? ' target' : '');
    if (!p.destroyed && !targets.includes(i)) t.style.borderColor = color; else t.style.borderColor = '';
    const r = req(i), cells = Math.max(r, p.cards.length);
    let slots = '';
    for (let k = 0; k < cells; k++) {
      const cd = p.cards[k];
      if (cd) {
        const c = DB[cd.id];
        const mk = cd.guard ? '<span class="mk">盾</span>' : cd.promote ? '<span class="mk">↑</span>' : '';
        slots += `<div class="slot on" style="background:${SP_COLOR[speciesOf(c)]}">${CASTE[casteOf(c)]}${mk}</div>`;
      } else slots += '<div class="slot"></div>';
    }
    const zoneNote = def.tree ? (S.treeFallen ? '特殊:倒れた' : '特殊:4枚で倒れる') : ZONE_LABEL[def.zone];
    let badges = '';
    if (p.barrier > 0) badges += `<span class="badge barrier">バリア 残${p.barrier}</span>`;
    if (p.bait > 0) badges += `<span class="badge bait">${p.bait <= 1 ? 'ベイト 次で発動' : 'ベイト あと' + p.bait}</span>`;
    if (p.swarm) badges += `<span class="badge swarm">大量発生</span>`;
    t.innerHTML = `<div class="head" style="background:${color}"><span class="nm">${def.name}</span><span class="zn">${zoneNote}</span><span class="sp">${def.allowed.map(s => `<i style="color:${SP_COLOR[s]}">${SPECIES[s]}</i>`).join('')}</span></div>` +
      // 完全侵食したポイントは数字を出さず、札だけにする(README「盤面」)
      `<div class="slots">${slots}${p.destroyed ? `<span class="stamp">${def.tree ? '倒れた' : '完全侵食'}</span>` : `<span class="prog">${p.cards.length}/${r}</span>`}</div><div class="badges">${badges}</div>`;
  }
  field.classList.toggle('field-dim', targets.length > 0);
}

function renderInfo() {
  const info = $('#info'), l1 = $('#info .l1'), l2 = $('#info .l2');
  info.classList.remove('mode-target');
  $('#btn-cancel').style.display = 'none';
  if (S.over) { l1.textContent = '対戦終了'; l2.textContent = ''; return; }
  if (S.current !== 'me') { l1.innerHTML = '<span style="color:var(--muted)">相手のターンです。CPU が考えています…</span>'; l2.textContent = ''; return; }
  if (S.mode === 'target' && S.sel != null) {
    const card = S.hand.find(x => x.uid === S.sel), c = DB[card.id];
    info.classList.add('mode-target');
    $('#btn-cancel').style.display = '';
    l1.innerHTML = `<b>${c.name}</b> <small>コスト${costLabel(c)} ・ ${kindLabel(c)}</small>  <span class="guide">光っているポイントを選んでください</span>`;
    l2.textContent = c.effect;
    return;
  }
  l1.textContent = `カードを選んでください(行動力 残り${S.ap})`;
  l2.textContent = 'タップで拡大して確認 ・ 上に引き上げて使う ・ 場所のあるカードはポイントまで引っぱる';
}

function renderMe() {
  const me = S.human;
  const chip = $('#me .who .chip'); chip.className = 'chip ' + me; chip.textContent = SIDE_LABEL[me];
  $('#my-stats').innerHTML = `山札 <b>${S.deck[me]}</b> ・ 捨て札 <b>${S.discard[me]}</b>`;
  const orbs = $('#ap .orbs'); orbs.innerHTML = '';
  const myTurn = !S.over && S.current === 'me';
  for (let k = 0; k < S.maxAp; k++) orbs.appendChild(h('span', 'orb' + (myTurn && k < S.ap ? '' : ' off')));
  $('#ap .lbl').textContent = myTurn ? `行動力 ${S.ap}/${S.maxAp}` : '行動力 -';
  const end = $('#btn-end');
  end.className = 'btn side-' + me;
  end.disabled = !canAct();
  const anyUsable = myTurn && S.hand.some(c => options(c).usable);
  end.classList.toggle('nudge', canAct() && !anyUsable);
}

// 手札:扇形に並べる(HandFanLayout にあたる)
const CARD_W = 240, CARD_H = 336; // 手札のカード(B ポップ 300×420 を 0.8 倍)
function fanSlot(i, n, selected) {
  const mid = (n - 1) / 2;
  const sp = n > 1 ? Math.min(210, (1040 - CARD_W) / (n - 1)) : 0;
  const x = 540 - CARD_W / 2 + (i - mid) * sp;
  const rot = (i - mid) * Math.min(3, 18 / Math.max(n, 1));
  const y = 120 + Math.pow(Math.abs(i - mid), 2) * 4;
  if (selected) return { x, y: y - 80, rot: 0, s: 1.08 };
  return { x, y, rot, s: 1 };
}
const tf = p => `translate(${p.x}px,${p.y}px) rotate(${p.rot}deg) scale(${p.s})`;
function renderHand(opts = {}) {
  const box = $('#hand');
  const keep = new Set(S.hand.map(c => c.uid));
  $$('.hcard', box).forEach(e => { if (!keep.has(+e.dataset.uid)) e.remove(); });
  const n = S.hand.length;
  S.hand.forEach((card, i) => {
    let e = $(`.hcard[data-uid="${card.uid}"]`, box);
    const fresh = !e;
    if (fresh) {
      e = h('div', 'hcard'); e.dataset.uid = card.uid;
      e.appendChild(smallCard(card.id)); e.appendChild(h('div', 'dim'));
      bindHandCard(e, card.uid);
      box.appendChild(e);
    }
    const o = options(card);
    const myTurn = !S.over && S.current === 'me';
    e.classList.toggle('usable', myTurn && o.usable && canAct());
    e.classList.toggle('pulse', myTurn && o.usable && canAct() && S.sel !== card.uid);
    e.classList.toggle('unusable', myTurn && !o.usable);
    e.classList.toggle('selected', S.sel === card.uid);
    e.style.zIndex = S.sel === card.uid ? 50 : i + 1;
    const p = fanSlot(i, n, S.sel === card.uid);
    e._slot = p;
    if (fresh && opts.hideNew) e.style.opacity = '0';
    if (fresh) { e.style.transform = tf(p); }
    else { e.style.transition = `transform ${D(T.relayout)}ms ${EASE.OutCubic}`; e.style.transform = tf(p); }
  });
}

// ───────── 手札の操作(タップで拡大、ドラッグで使う) ─────────
function bindHandCard(e, uid) {
  let start = null, dragging = false, hover = -1;
  e.addEventListener('pointerdown', ev => {
    if (!canAct()) return;
    ev.preventDefault();
    e.setPointerCapture(ev.pointerId);
    start = toStage(ev); dragging = false; hover = -1;
  });
  e.addEventListener('pointermove', ev => {
    if (!start) return;
    const p = toStage(ev);
    if (!dragging && Math.hypot(p.x - start.x, p.y - start.y) > 14) {
      dragging = true;
      if (S.mode === 'target' && S.sel !== uid) cancelTarget();
      e.style.transition = 'none'; e.style.zIndex = 80;
      const o = options(S.hand.find(c => c.uid === uid));
      if (o.usable && o.needsTarget) { renderField(o.targets); showDragInfo(uid); }
    }
    if (dragging) {
      // 引っぱっている間は少し小さくして、盤面を隠しにくくする
      e.style.transform = `translate(${p.x - CARD_W / 2}px,${p.y - 1400 - CARD_H * 0.75}px) rotate(0deg) scale(.8)`;
      const o = options(S.hand.find(c => c.uid === uid));
      hover = -1;
      if (o.usable && o.needsTarget) {
        for (const i of o.targets) { const r = rectOf(tileEl(i)); if (p.x >= r.x && p.x <= r.x + r.w && p.y >= r.y && p.y <= r.y + r.h) hover = i; }
        $$('#field .tile').forEach(t => t.classList.toggle('hover', +t.dataset.i === hover));
      }
    }
  });
  const up = ev => {
    if (!start) return;
    const p = toStage(ev); start = null;
    $$('#field .tile').forEach(t => t.classList.remove('hover'));
    if (!dragging) { onHandTap(uid); return; }
    const card = S.hand.find(c => c.uid === uid), o = options(card);
    if (o.usable && o.needsTarget && hover >= 0) { playCard(uid, hover, e); return; }
    if (o.usable && !o.needsTarget && p.y < 1250) { playCard(uid, -1, e); return; }
    // 戻す(使えないカードを引き上げたときは震わせる)
    renderField(S.mode === 'target' ? currentTargets() : []); renderInfo();
    e.style.transition = `transform ${D(T.snapBack)}ms ${EASE.OutBack}`;
    e.style.transform = tf(e._slot); e.style.zIndex = '';
    if (!o.usable && p.y < 1250) { shake(e); flashReason(o.reason); }
    renderHand();
  };
  e.addEventListener('pointerup', up);
  e.addEventListener('pointercancel', up);
}
function showDragInfo(uid) {
  const c = DB[S.hand.find(x => x.uid === uid).id];
  $('#info').classList.add('mode-target');
  $('#info .l1').innerHTML = `<b>${c.name}</b> <small>コスト${costLabel(c)} ・ ${kindLabel(c)}</small>  <span class="guide">光っているポイントで指を離してください</span>`;
  $('#info .l2').textContent = c.effect;
}
function shake(el) {
  return anim(el, [0, -12, 12, -9, 9, -4, 0].map(x => ({ translate: `${x}px 0` })), T.shake, 'Linear', { fill: 'none' });
}
function flashReason(reason) { if (reason) ticker(reason, true); }
function currentTargets() { const card = S.hand.find(c => c.uid === S.sel); return card ? options(card).targets : []; }

function onHandTap(uid) {
  if (!canAct()) return;
  if (S.mode === 'target') { const same = S.sel === uid; cancelTarget(); if (same) return; }
  openDetail(uid);
}
function onTileTap(i) {
  if (!canAct() || S.mode !== 'target') return;
  if (!currentTargets().includes(i)) return;
  const uid = S.sel; const e = $(`.hcard[data-uid="${uid}"]`);
  playCard(uid, i, e);
}
function enterTarget(uid) {
  S.sel = uid; S.mode = 'target';
  renderField(currentTargets()); renderInfo(); renderHand();
}
function cancelTarget() { S.sel = null; S.mode = null; renderField(); renderInfo(); renderHand(); }

// ───────── 拡大表示 ─────────
const DETAIL_SCALE = 628 / 630; // 拡大の大きさ(628×877)。README「拡大表示の位置と大きさ」
function openDetail(uid) {
  const card = S.hand.find(c => c.uid === uid); const o = options(card); const c = DB[card.id];
  S.sel = uid; renderHand();
  const d = $('#detail'), holder = $('.holder', d);
  holder.innerHTML = ''; holder.appendChild(fullCard(card.id, DETAIL_SCALE));
  holder.style.transform = '';
  const hint = $('.hint', d);
  if (!o.usable) { hint.textContent = o.reason; hint.className = 'hint warn'; hint.style.display = ''; }
  else if (o.needsTarget) { hint.textContent = '置く場所を選んで使うカードです'; hint.className = 'hint guide'; hint.style.display = ''; }
  else hint.style.display = 'none';
  const use = $('.use', d);
  use.disabled = !o.usable;
  use.textContent = !o.usable ? '使えません' : o.needsTarget ? '場所を選ぶ' : '使う';
  $('.swipe', d).style.visibility = o.usable ? 'visible' : 'hidden';
  d.classList.add('on');
  d._uid = uid;
  const from = rectOf($(`.hcard[data-uid="${uid}"]`));
  const s0 = from.w / 628;
  anim(holder, [{ transform: `translate(${from.x - 226}px,${from.y - 250}px) scale(${s0})` }, { transform: 'none' }], T.detailOpen, 'OutBack');
  anim($('.shade', d), [{ opacity: 0 }, { opacity: 1 }], T.shade, 'Linear');
  anim($('.btns', d), [{ opacity: 0, transform: 'translateY(40px)' }, { opacity: 1, transform: 'none' }], T.detailOpen, 'OutCubic');
}
async function closeDetail(toHand = true) {
  const d = $('#detail'); if (!d.classList.contains('on')) return;
  const holder = $('.holder', d);
  const he = $(`.hcard[data-uid="${d._uid}"]`);
  if (toHand && he) {
    const to = rectOf(he);
    anim($('.shade', d), [{ opacity: 1 }, { opacity: 0 }], T.detailClose, 'Linear');
    await anim(holder, [{ transform: getComputedStyle(holder).transform }, { transform: `translate(${to.x - 226}px,${to.y - 250}px) scale(${to.w / 628})` }], T.detailClose, 'InCubic');
  }
  d.classList.remove('on');
  holder.getAnimations().forEach(a => a.cancel());
}
function bindDetail() {
  const d = $('#detail'), holder = $('.holder', d);
  $('.shade', d).addEventListener('click', async () => { await closeDetail(); S.sel = null; renderHand(); });
  $('.back', d).addEventListener('click', async () => { await closeDetail(); S.sel = null; renderHand(); });
  $('.use', d).addEventListener('click', () => useFromDetail());
  let start = null;
  holder.addEventListener('pointerdown', ev => { ev.preventDefault(); holder.setPointerCapture(ev.pointerId); start = toStage(ev); holder.getAnimations().forEach(a => a.cancel()); });
  holder.addEventListener('pointermove', ev => { if (!start) return; const p = toStage(ev); const dy = Math.min(0, p.y - start.y); holder.style.transform = `translateY(${dy}px) scale(${1 + Math.min(0.06, -dy / 3000)})`; });
  holder.addEventListener('pointerup', ev => {
    if (!start) return; const p = toStage(ev); const dy = p.y - start.y; start = null;
    const card = S.hand.find(c => c.uid === d._uid); const o = options(card);
    if (dy < -160 && o.usable) { useFromDetail(); return; }
    if (dy < -160 && !o.usable) shake(holder.parentElement.querySelector('.hint'));
    if (Math.abs(dy) < 10) { closeDetail().then(() => { S.sel = null; renderHand(); }); return; }
    anim(holder, [{ transform: holder.style.transform }, { transform: 'none' }], T.snapBack, 'OutBack').then(() => holder.style.transform = '');
  });
}
async function useFromDetail() {
  const d = $('#detail'); const uid = d._uid;
  const card = S.hand.find(c => c.uid === uid); const o = options(card);
  if (!o.usable) return;
  if (o.needsTarget) { await closeDetail(); enterTarget(uid); return; }
  // 拡大のまま上へ飛ばして使う
  const holder = $('.holder', d);
  const r = rectOf(holder);
  d.classList.remove('on');
  playCard(uid, -1, null, r);
}

// ───────── カードを出す ─────────
async function playCard(uid, point, fromEl, fromRect) {
  const idx = S.hand.findIndex(c => c.uid === uid); if (idx < 0) return;
  const card = S.hand[idx]; const c = DB[card.id];
  S.busy = true;
  const r = fromRect || rectOf(fromEl);
  S.hand.splice(idx, 1); S.sel = null; S.mode = null;
  const fly = h('div', 'fly'); fly.style.width = '630px'; fly.style.height = '880px';
  fly.appendChild(fullCard(card.id, 1));
  $('#fx').appendChild(fly);
  const s0 = r.w / 630;
  renderField(); renderHand(); renderInfo();
  await apSpend(c.cost);
  if (point >= 0) {
    const tr = rectOf(tileEl(point));
    const s1 = 0.3;
    await anim(fly, [{ transform: `translate(${r.x}px,${r.y}px) scale(${s0})` }, { transform: `translate(${tr.x + tr.w / 2 - 315 * s1}px,${tr.y + tr.h / 2 - 440 * s1}px) scale(${s1})`, opacity: .9 }], T.playFly, 'InCubic');
    fly.remove();
  } else {
    const s1 = 0.75;
    await anim(fly, [{ transform: `translate(${r.x}px,${r.y}px) scale(${s0})` }, { transform: `translate(${540 - 315 * s1}px,${700 - 440 * s1}px) scale(${s1})` }], T.playFly, 'OutCubic');
    popText('発動!', 540, 1050);
    await sleep(T.playHold);
    await anim(fly, [{ opacity: 1, transform: `translate(${540 - 315 * s1}px,${700 - 440 * s1}px) scale(${s1})` }, { opacity: 0, transform: `translate(${540 - 315 * .9}px,${700 - 440 * .9}px) scale(.9)` }], T.playPop, 'OutQuad');
    fly.remove();
  }
  S.used++;
  S.discard[S.human] += isTermite(c) ? 0 : 1;
  await resolve(S.human, card.id, point);
  S.busy = false;
  renderAll();
}
async function apSpend(cost) {
  if (!cost) return;
  const orbs = $$('#ap .orb');
  const ps = [];
  for (let k = S.ap - 1; k >= S.ap - cost; k--) if (orbs[k]) ps.push(anim(orbs[k], [{ transform: 'scale(1)' }, { transform: 'scale(1.25)' }, { transform: 'scale(0)' }], T.apSpend, 'InBack', { fill: 'none' }));
  S.ap -= cost;
  await Promise.all(ps);
  renderMe();
}

// 効果の処理と演出(仮:簡易版)
async function resolve(side, id, point) {
  const c = DB[id]; const P = S.points;
  const who = side === S.human ? 'あなた' : 'CPU';
  if (isTermite(c)) {
    const p = P[point];
    p.cards.push({ id, guard: casteOf(c) === 'soldier', promote: casteOf(c) === 'winged' });
    if (side === S.human) S.placed.add(point);
    log(side, `${c.name}を${POINTS[point].name}に置いた(${p.cards.length}/${req(point)})`);
    renderField();
    const slot = $$('.slot.on', tileEl(point)).pop();
    await anim(slot, [{ transform: 'scale(0)' }, { transform: 'scale(1)' }], T.slotPop, 'OutBack', { fill: 'none' });
    await checkDestroy(point);
  } else if (id === 'spray') {
    const p = P[point];
    await mist(point);
    const g = p.cards.findIndex(x => x.guard);
    if (g >= 0) {
      p.cards[g].guard = false;
      log(side, `${POINTS[point].name}に薬剤散布。${DB[p.cards[g].id].name}が耐えた`);
      const tr = rectOf(tileEl(point)); popText('耐えた!', tr.x + tr.w / 2, tr.y + 40);
      renderField();
      await anim(tileEl(point), [{ boxShadow: '0 0 0 0 #fff' }, { boxShadow: '0 0 0 14px #fff' }, { boxShadow: '0 0 0 0 #fff' }], T.guardFlash, 'OutQuad', { fill: 'none' });
    } else {
      const slot = $$('.slot.on', tileEl(point)).pop();
      const removed = p.cards.pop();
      await anim(slot, [{ transform: 'none', opacity: 1 }, { transform: 'translateY(-50px) scale(.6)', opacity: 0 }], 300, 'InCubic');
      log(side, `${POINTS[point].name}に薬剤散布。「${DB[removed.id].name}」を駆除`);
      S.discard.termite++;
      renderField();
    }
  } else if (id === 'barrier') {
    P[point].barrier = 3; log(side, `バリア工法を${POINTS[point].name}に施工(3ターン)`); renderField();
    await anim($('.badge.barrier', tileEl(point)), [{ transform: 'translateY(-80px)', opacity: 0 }, { transform: 'none', opacity: 1 }], T.badgeDrop, 'OutBack');
  } else if (id === 'bait') {
    P[point].bait = 2; log(side, `ベイト工法を${POINTS[point].name}に設置(2回後の自分のターン開始時に発動)`); renderField();
    await anim($('.badge.bait', tileEl(point)), [{ transform: 'translateY(-80px)', opacity: 0 }, { transform: 'none', opacity: 1 }], T.badgeDrop, 'OutBack');
  } else if (id === 'swarm') {
    P[point].swarm = true; log(side, `大量発生。${POINTS[point].name}に次のシロアリ側のターンまで防除・駆除カードを置けない`); renderField();
    await anim(tileEl(point), [{ filter: 'none' }, { filter: 'drop-shadow(0 0 30px #ff7a1a) brightness(1.15)' }, { filter: 'none' }], 500, 'InOutSine', { fill: 'none' });
  } else if (id === 'reinforce') {
    log(side, `${c.name}を使った`);
    if (side === S.human) await drawCards(2, '増援'); else { S.oppHand += 2; S.deck[side] -= 2; renderOpp(); }
  } else if (id === 'import-furniture') {
    log(side, `${c.name}を使った`);
    if (side === S.human) await drawCards(1, '輸入家具購入', 'worker-kanzai'); else { S.oppHand++; renderOpp(); }
  } else if (id === 'scout') {
    log(side, `${c.name}を使った`);
    if (side === S.human) await openScout();
  } else if (id === 'disrupt') {
    log(side, `${c.name}を使った`);
    if (side === S.human) await disruptAnim(); else if (S.hand.length) { const lost = S.hand.splice(rnd(S.hand.length), 1)[0]; log(side, `妨害で相手の「${DB[lost.id].name}」を捨てさせた`); renderHand(); }
  } else if (id === 'foresight') {
    log(side, `${c.name}を使った`);
    if (side === S.human) await openForesight();
  }
  if (side !== S.human) ticker(`CPU:${S.log[S.log.length - 1].text}`);
  renderAll();
  checkWin();
}
async function checkDestroy(i) {
  const p = S.points[i];
  if (p.destroyed || p.cards.length < req(i)) return;
  p.destroyed = true;
  const t = tileEl(i);
  await anim(t, [0, -14, 14, -10, 10, -5, 0].map(x => ({ transform: `translateX(${x}px)` })), T.destroyShake, 'Linear', { fill: 'none' });
  if (POINTS[i].tree) {
    S.treeFallen = true;
    log('termite', `庭の枯れ木が倒れた! 全ての場所が2枚で完全侵食`);
    renderField();
    await anim(tileEl(i), [{ transform: 'rotate(0)' }, { transform: 'rotate(-6deg) translateY(10px)' }], T.treeFall, 'OutBounce' in EASE ? 'OutBounce' : 'OutCubic');
    await banner('庭の枯れ木が倒れた!', 'termite', '全ての場所が2枚で完全侵食');
    // 必要枚数が減った分、他のポイントも完全侵食になることがある
    for (let k = 0; k < 8; k++) if (k !== i) await checkDestroy(k);
  } else {
    S.destroyed++;
    log('termite', `${POINTS[i].name}が完全侵食された(${S.destroyed}/5)`);
    renderField();
  }
  await anim($('.stamp', tileEl(i)), [{ transform: 'rotate(-6deg) scale(2.2)', opacity: 0 }, { transform: 'rotate(-6deg) scale(1)', opacity: 1 }], T.stamp, 'OutBack', { fill: 'none' });
  renderGoals();
}
async function mist(i) {
  const r = rectOf(tileEl(i));
  const ps = [];
  for (let k = 0; k < 6; k++) {
    const m = h('div', 'mist'); const sz = 120 + rnd(80);
    m.style.width = m.style.height = sz + 'px';
    const x = r.x + 40 + rnd(r.w - 80) - sz / 2, y = r.y + 30 + rnd(r.h - 60) - sz / 2;
    $('#fx').appendChild(m);
    m.style.opacity = '0';
    ps.push(anim(m, [{ transform: `translate(${x}px,${y - 60}px) scale(.3)`, opacity: 0 }, { transform: `translate(${x}px,${y}px) scale(1)`, opacity: 1, offset: .4 }, { transform: `translate(${x}px,${y + 20}px) scale(1.3)`, opacity: 0 }], T.sprayMist, 'OutQuad', { delay: k * 40 }).then(() => m.remove()));
  }
  await Promise.all(ps);
}
function popText(text, x, y, color) {
  const e = h('div', 'pop-text', text); if (color) e.style.color = color;
  $('#fx').appendChild(e);
  e.style.left = x + 'px'; e.style.top = y + 'px';
  anim(e, [{ transform: 'translate(-50%,-50%) scale(.3)', opacity: 0 }, { transform: 'translate(-50%,-50%) scale(1.15)', opacity: 1, offset: .3 }, { transform: 'translate(-50%,-90%) scale(1)', opacity: 1, offset: .75 }, { transform: 'translate(-50%,-120%) scale(1)', opacity: 0 }], 900, 'OutQuad').then(() => e.remove());
}

// ───────── ドロー ─────────
async function drawCards(n, reason, fixedId) {
  const me = S.human; const got = [];
  for (let k = 0; k < n && S.deck[me] > 0; k++) { const id = fixedId || drawId(me); S.deck[me]--; const card = { uid: uidSeq++, id }; S.hand.push(card); got.push(card); }
  if (!got.length) return;
  renderHand({ hideNew: true }); renderMe(); renderGoals();
  const from = rectOf($('#my-pile'));
  await Promise.all(got.map((card, k) => (async () => {
    await sleep(k * T.drawStagger);
    const he = $(`.hcard[data-uid="${card.uid}"]`); if (!he) return;
    const p = he._slot;
    const b = backCard(CARD_W, CARD_H); b.classList.add('fly'); $('#fx').appendChild(b);
    await anim(b, [{ transform: `translate(${from.x}px,${from.y}px) scale(.15)` }, { transform: `translate(${p.x}px,${1400 + p.y}px) rotate(${p.rot}deg) scale(1)` }], T.draw, 'OutCubic');
    await anim(b, [{ transform: `translate(${p.x}px,${1400 + p.y}px) rotate(${p.rot}deg) scaleX(1)` }, { transform: `translate(${p.x + CARD_W / 2}px,${1400 + p.y}px) rotate(${p.rot}deg) scaleX(0)` }], T.flip / 2, 'InQuad');
    b.remove(); he.style.opacity = '1';
    await anim(he, [{ scale: '0 1' }, { scale: '1 1' }], T.flip / 2, 'OutQuad', { fill: 'none' });
  })()));
  log(me, `${reason}:${got.length}枚引いた(${got.map(c => DB[c.id].name).join('・')})`);
  renderAll();
}

// ───────── 選択シート ─────────
function openSheet(cfg) {
  const sh = $('#sheet');
  $('.title', sh).textContent = cfg.title; $('.desc', sh).textContent = cfg.desc || '';
  const box = $('.cards', sh); box.innerHTML = '';
  const row = h('div', 'scrollrow'); box.appendChild(row);
  const n = cfg.cards.length; const sp = n <= 3 ? 320 : 310;
  const total = 300 + sp * (n - 1); const x0 = Math.max(60, (1080 - total) / 2);
  cfg.cards.forEach((card, i) => {
    const e = h('div', 'pick'); e.style.left = (x0 + i * sp) + 'px'; e.style.top = '40px';
    e.appendChild(smallCard(card.id)); e.appendChild(h('div', 'x', '捨てる'));
    const tag = cfg.tag ? cfg.tag(card, i) : null;
    if (tag) e.appendChild(h('div', 'tag ' + (tag.cls || ''), tag.text));
    if (cfg.isSel && cfg.isSel(card, i)) e.classList.add('sel');
    if (cfg.isDiscard && cfg.isDiscard(card, i)) e.classList.add('discard');
    e.addEventListener('click', () => cfg.onPick && cfg.onPick(card, i));
    row.appendChild(e);
  });
  row.appendChild(Object.assign(h('div'), { style: `position:absolute;left:${x0 + total + 60}px;width:1px;height:1px` }));
  const selCard = cfg.selEffect ? cfg.selEffect() : null;
  if (selCard) box.appendChild(h('div', 'sel-effect', `<b>${DB[selCard.id].name}</b>:${DB[selCard.id].effect}`));
  const acts = $('.acts', sh); acts.innerHTML = '';
  if (cfg.sub) { const b = h('button', 'btn secondary sub', cfg.sub.text); b.onclick = cfg.sub.fn; acts.appendChild(b); }
  if (cfg.ok) { const b = h('button', 'btn primary ok' + (cfg.sub ? '' : ' wide'), cfg.ok.text); b.disabled = cfg.ok.enabled === false; b.onclick = cfg.ok.fn; acts.appendChild(b); }
  if (!sh.classList.contains('on')) {
    sh.classList.add('on');
    anim($('.panel', sh), [{ transform: 'translateY(1100px)' }, { transform: 'none' }], T.sheetIn, 'OutCubic');
    anim($('.shade', sh), [{ opacity: 0 }, { opacity: 1 }], T.shade, 'Linear');
  }
}
async function closeSheet() {
  const sh = $('#sheet'); if (!sh.classList.contains('on')) return;
  anim($('.shade', sh), [{ opacity: 1 }, { opacity: 0 }], T.sheetOut, 'Linear');
  await anim($('.panel', sh), [{ transform: 'none' }, { transform: 'translateY(1100px)' }], T.sheetOut, 'InCubic');
  sh.classList.remove('on');
}

function openForesight() {
  return new Promise(done => {
    const cards = [0, 1, 2].map(() => ({ uid: uidSeq++, id: drawId(S.human) }));
    let take = -1; let order = [];
    const step1 = () => openSheet({
      title: `予知:山札の上${cards.length}枚`, desc: '手札に加える1枚を選んでください。残りは山札の下に置きます',
      cards, isSel: c => c.uid === take, tag: c => c.uid === take ? { text: '手札に加える', cls: 'gold' } : null,
      selEffect: () => cards.find(c => c.uid === take),
      onPick: c => { take = c.uid; step1(); },
      ok: { text: 'これを手札に加える', enabled: take >= 0, fn: () => { order = cards.filter(c => c.uid !== take); step2(); } },
    });
    const step2 = () => openSheet({
      title: '山札の下に置く順番', desc: `手札に加える:${DB[cards.find(c => c.uid === take).id].name}\n右にあるカードほど、山札の底になります(タップで入れ替え)`,
      cards: order, tag: (c, i) => ({ text: i === order.length - 1 ? `${i + 1}(いちばん底)` : `${i + 1}` }),
      onPick: (c, i) => { const j = i === 0 ? 1 : i - 1; [order[i], order[j]] = [order[j], order[i]]; step2(); },
      sub: { text: '選び直す', fn: () => step1() },
      ok: { text: 'この順で山札の下へ', fn: async () => {
        await closeSheet();
        const t = cards.find(c => c.uid === take);
        S.deck[S.human]++; // drawCards で1枚減らすため
        await drawCards(1, '予知', t.id);
        log(S.human, `予知で「${DB[t.id].name}」を手札に加え、${order.length}枚を山札の下へ置いた`);
        done();
      } },
    });
    step1();
  });
}
function openDiscard(need) {
  return new Promise(done => {
    const sel = new Set();
    const draw = () => openSheet({
      title: '手札が7枚を超えています', desc: `捨てるカードを${need}枚選んでください(${sel.size}/${need})`,
      cards: S.hand, isDiscard: c => sel.has(c.uid),
      onPick: c => { if (!sel.delete(c.uid) && sel.size < need) sel.add(c.uid); draw(); },
      ok: { text: '捨ててターン終了', enabled: sel.size === need, fn: async () => {
        await closeSheet();
        const names = S.hand.filter(c => sel.has(c.uid)).map(c => DB[c.id].name);
        const els = [...sel].map(u => $(`.hcard[data-uid="${u}"]`));
        await Promise.all(els.map(e => anim(e, [{ opacity: 1 }, { opacity: 0, transform: e.style.transform + ' translateY(200px)' }], 300, 'InCubic')));
        S.hand = S.hand.filter(c => !sel.has(c.uid)); S.discard[S.human] += need;
        log(S.human, `手札上限で${need}枚捨てた(${names.join('・')})`);
        renderAll(); done();
      } },
    });
    draw();
  });
}
function openMulligan() {
  return new Promise(done => {
    let pick = -1;
    const draw = () => openSheet({
      title: '1枚も使いませんでした', desc: '手札を1枚捨てて1枚引けます(任意)',
      cards: S.hand, isDiscard: c => c.uid === pick,
      onPick: c => { pick = pick === c.uid ? -1 : c.uid; draw(); },
      sub: { text: '捨てずにターン終了', fn: async () => { await closeSheet(); done(); } },
      ok: { text: '捨てて1枚引く', enabled: pick >= 0, fn: async () => {
        await closeSheet();
        const c = S.hand.find(x => x.uid === pick);
        S.hand = S.hand.filter(x => x.uid !== pick); S.discard[S.human]++;
        log(S.human, `手詰まり防止:「${DB[c.id].name}」を捨てた`);
        renderHand(); await sleep(200); await drawCards(1, '手詰まり防止'); done();
      } },
    });
    draw();
  });
}
async function openScout() {
  const n = Math.max(1, S.oppHand);
  // 相手の手札が裏から表に返る演出
  const backs = $$('#opp-hand .back');
  await Promise.all(backs.map((b, k) => anim(b, [{ transform: b.style.transform }, { transform: b.style.transform + ' translateY(30px) scaleX(0)' }], 200, 'InQuad', { delay: k * 60 })));
  const cards = Array.from({ length: n }, () => ({ uid: uidSeq++, id: drawId(S.cpu) }));
  log(S.human, `偵察で相手の手札を見た(${cards.map(c => DB[c.id].name).join('・')})`);
  return new Promise(done => openSheet({
    title: '偵察:相手の手札', desc: `相手の手札は${n}枚です`, cards,
    ok: { text: 'とじる', fn: async () => { await closeSheet(); renderOpp(); done(); } },
  }));
}
async function disruptAnim() {
  const backs = $$('#opp-hand .back'); if (!backs.length) return;
  const b = pickOne(backs);
  await anim(b, [0, -10, 10, -8, 8, 0].map(x => ({ transform: b.style.transform + ` translateX(${x}px)` })), 300, 'Linear', { fill: 'none' });
  await anim(b, [{ opacity: 1 }, { opacity: 0, transform: b.style.transform + ' translateY(-120px) rotate(20deg)' }], 400, 'InCubic');
  const lostId = drawId(S.cpu);
  S.oppHand--; S.discard[S.cpu]++;
  log(S.human, `妨害で相手の「${DB[lostId].name}」を捨てさせた`);
  renderOpp();
}

// ───────── 手番 ─────────
async function banner(text, cls, sub) {
  const b = h('div', 'banner ' + cls, `<div>${text}${sub ? `<small>${sub}</small>` : ''}</div>`);
  $('#fx').appendChild(b);
  await anim(b, [{ transform: 'translateX(-1100px)' }, { transform: 'none' }], T.bannerIn, 'OutCubic');
  await sleep(T.bannerHold);
  await anim(b, [{ transform: 'none' }, { transform: 'translateX(1100px)' }], T.bannerOut, 'InCubic');
  b.remove();
}
async function apRefill() {
  renderMe();
  await Promise.all($$('#ap .orb').map((o, k) => anim(o, [{ transform: 'scale(0)' }, { transform: 'scale(1)' }], T.apRefill, 'OutBack', { delay: D(k * T.apStagger), fill: 'backwards' })));
}
async function turnStart(side) {
  const P = S.points;
  if (side === 'pest') {
    for (let i = 0; i < 8; i++) {
      if (P[i].barrier > 0 && --P[i].barrier === 0) log(side, `${POINTS[i].name}のバリア工法が切れた`);
      if (P[i].bait > 0 && --P[i].bait === 0) {
        const removed = P[i].cards.filter(c => speciesOf(DB[c.id]) !== 'kanzai');
        renderField();
        const slots = $$('.slot.on', tileEl(i)).filter((s, k) => speciesOf(DB[P[i].cards[k].id]) !== 'kanzai');
        await anim(tileEl(i), [{ filter: 'none' }, { filter: 'drop-shadow(0 0 30px #3a7bc0) brightness(1.2)' }, { filter: 'none' }], 400, 'InOutSine', { fill: 'none' });
        await Promise.all(slots.map((s, k) => anim(s, [{ opacity: 1 }, { opacity: 0, transform: 'scale(.4)' }], 250, 'InBack', { delay: D(k * 80) })));
        P[i].cards = P[i].cards.filter(c => speciesOf(DB[c.id]) === 'kanzai');
        S.discard.termite += removed.length;
        log(side, `ベイト工法が発動。${POINTS[i].name}のシロアリを${removed.length}枚駆除`);
        renderField();
      }
    }
  } else {
    for (let i = 0; i < 8; i++) {
      if (P[i].swarm) { P[i].swarm = false; log(side, `${POINTS[i].name}の大量発生が収まった`); }
      const w = P[i].cards.find(c => c.promote);
      if (w) await promote(side, i, w);
    }
  }
  renderField();
}
async function promote(side, i, w) {
  const sp = speciesOf(DB[w.id]);
  w.promote = false; const wName = DB[w.id].name; w.id = 'queen-' + sp;
  renderField();
  const slot = $$('.slot.on', tileEl(i))[S.points[i].cards.indexOf(w)];
  await anim(slot, [{ transform: 'scale(1)', filter: 'none' }, { transform: 'scale(1.5)', filter: 'brightness(1.8)' }, { transform: 'scale(1)', filter: 'none' }], 500, 'OutBack', { fill: 'none' });
  log(side, `${POINTS[i].name}の${wName}が${DB[w.id].name}になった`);
  const r = rectOf(tileEl(i)); popText('女王アリに!', r.x + r.w / 2, r.y + 30, 'var(--cost)');
  if (side === S.human) await drawCards(2, `${DB[w.id].name}の効果`, 'worker-' + sp);
  else { S.oppHand += 2; renderOpp(); }
}

async function startMyTurn() {
  S.current = 'me'; S.ap = 0; S.used = 0; S.placed.clear(); S.busy = true;
  log(S.human, `― ${SIDE_LABEL[S.human]}のターン(シロアリ山札${S.deck.termite}枚・防除山札${S.deck.pest}枚)`);
  renderAll();
  await banner('あなたのターン', S.human);
  await turnStart(S.human);
  S.ap = S.maxAp; S.busy = false;
  await apRefill();
  renderAll();
}
async function onEndTurn() {
  if (!canAct()) return;
  S.busy = true; renderMe();
  if (S.used === 0 && S.hand.length > 0) await openMulligan();
  const need = Math.max(0, 5 - S.hand.length);
  if (need) await drawCards(need, '補充');
  if (S.hand.length > 7) await openDiscard(S.hand.length - 7);
  log(S.human, `${SIDE_LABEL[S.human]}のターン終了`);
  if (checkWin()) return;
  S.busy = false;
  cpuTurn();
}

// CPU の手番(仮:試作用に1〜2枚を適当な場所へ出す)
function cpuChoices() {
  const out = [];
  const P = S.points;
  if (S.cpu === 'termite') {
    for (const id of ['worker-ie', 'worker-yamato', 'worker-kanzai', 'soldier-ie', 'winged-ie']) {
      const c = DB[id];
      const t = P.map((p, i) => i).filter(i => !P[i].destroyed && P[i].barrier === 0 && POINTS[i].allowed.includes(speciesOf(c)));
      if (t.length) out.push({ id, point: pickOne(t) });
    }
  } else {
    const hit = P.map((p, i) => i).filter(i => !P[i].destroyed && !P[i].swarm && P[i].cards.length);
    if (hit.length) { hit.sort((a, b) => P[b].cards.length - P[a].cards.length); out.push({ id: 'spray', point: hit[0] }, { id: 'spray', point: hit[0] }); }
    const bar = P.map((p, i) => i).filter(i => !P[i].destroyed && !P[i].swarm && P[i].barrier === 0);
    if (bar.length) out.push({ id: 'barrier', point: pickOne(bar) });
  }
  return out;
}
async function cpuTurn() {
  S.current = 'cpu'; S.busy = true; S.sel = null; S.mode = null;
  log(S.cpu, `― ${SIDE_LABEL[S.cpu]}のターン(シロアリ山札${S.deck.termite}枚・防除山札${S.deck.pest}枚)`);
  renderAll();
  await banner('相手のターン', S.cpu);
  await turnStart(S.cpu);
  let ap = 3; const n = 1 + (Math.random() < 0.6 ? 1 : 0);
  for (let k = 0; k < n && !S.over; k++) {
    await sleep(T.cpuThink);
    const ch = cpuChoices().filter(x => (DB[x.id].cost || 0) <= ap);
    if (!ch.length || S.oppHand === 0) break;
    const a = S.cpu === 'pest' && k === 0 && ch[0].id === 'spray' ? ch[0] : pickOne(ch);
    ap -= DB[a.id].cost || 0;
    await cpuPlay(a.id, a.point);
    await sleep(T.cpuGap);
  }
  if (S.over) return;
  const refill = Math.max(0, 5 - S.oppHand);
  S.oppHand += refill; S.deck[S.cpu] = Math.max(0, S.deck[S.cpu] - refill);
  log(S.cpu, `${SIDE_LABEL[S.cpu]}のターン終了`);
  ticker('CPU:ターン終了');
  renderAll();
  if (checkWin()) return;
  S.turn++;
  await sleep(300);
  startMyTurn();
}
async function cpuPlay(id, point) {
  const backs = $$('#opp-hand .back'); const src = backs[Math.floor(backs.length / 2)] || $('#opp-hand');
  const r = rectOf(src);
  S.oppHand--; renderOpp();
  // 裏のまま持ち上げる → 中央で表に返す → 見せる → 場所へ
  const sc = 0.62, cw = 630 * sc, ch = 880 * sc, cx = 540 - cw / 2, cy = 760 - ch / 2;
  const s0 = 110 / cw;
  const b = backCard(cw, ch); b.classList.add('fly'); $('#fx').appendChild(b);
  await anim(b, [{ transform: `translate(${r.x}px,${r.y}px) scale(${s0})` }, { transform: `translate(${r.x}px,${r.y + 40}px) scale(${s0 * 1.1})` }], T.cpuLift, 'OutQuad');
  await anim(b, [{ transform: `translate(${r.x}px,${r.y + 40}px) scale(${s0 * 1.1})` }, { transform: `translate(${cx}px,${cy}px) scale(1)` }], T.cpuFly, 'OutCubic');
  await anim(b, [{ transform: `translate(${cx}px,${cy}px) scale(1,1)` }, { transform: `translate(${cx + cw / 2}px,${cy}px) scale(0,1)` }], T.flip / 2, 'InQuad');
  b.remove();
  const f = h('div', 'fly'); f.appendChild(fullCard(id, 1)); $('#fx').appendChild(f);
  await anim(f, [{ transform: `translate(${cx + cw / 2}px,${cy}px) scale(0,${sc})` }, { transform: `translate(${cx}px,${cy}px) scale(${sc})` }], T.flip / 2, 'OutQuad');
  ticker(`CPU:${DB[id].name}を使った${point >= 0 ? ' → ' + POINTS[point].name : ''}`);
  await sleep(T.cpuShow);
  if (point >= 0) {
    const tr = rectOf(tileEl(point)); const s1 = .3;
    await anim(f, [{ transform: `translate(${cx}px,${cy}px) scale(${sc})` }, { transform: `translate(${tr.x + tr.w / 2 - 315 * s1}px,${tr.y + tr.h / 2 - 440 * s1}px) scale(${s1})`, opacity: .9 }], T.cpuFly, 'InCubic');
  } else await anim(f, [{ opacity: 1 }, { opacity: 0 }], 200, 'OutQuad');
  f.remove();
  S.discard[S.cpu] += isTermite(DB[id]) ? 0 : 1;
  await resolve(S.cpu, id, point);
}

// ───────── 勝敗 ─────────
function checkWin() {
  if (S.over) return true;
  if (S.destroyed >= 5) { finish('termite', 'シロアリ側の勝利(5ポイントを完全侵食)'); return true; }
  if (S.deck.termite <= 0) { finish('pest', '防除業者側の勝利(シロアリの山札が尽きるまで守りきった)'); return true; }
  return false;
}
async function finish(winner, text) {
  S.over = { winner, text }; S.busy = true;
  log(winner || S.current, text);
  closeAllOverlays(); renderAll();
  await sleep(400);
  const win = winner === S.human, draw = winner == null;
  const v = h('div', 'verdict' + (win ? '' : ' lose'));
  v.innerHTML = (win ? '<div class="rays"></div>' : '') + `<div class="word">${draw ? '引き分け' : win ? '勝利!' : '敗北…'}</div><div class="why">${draw ? text : SIDE_LABEL[winner] + 'の勝ち'}</div>`;
  $('#fx').appendChild(v);
  if (!win) $('#screen-battle').classList.add('grayout');
  anim(v, [{ opacity: 0 }, { opacity: 1 }], 200, 'Linear');
  if (win) {
    anim($('.rays', v), [{ transform: 'rotate(0)' }, { transform: 'rotate(40deg)' }], 3000, 'Linear');
    anim($('.word', v), [{ transform: 'scale(0)' }, { transform: 'scale(1.15)', offset: .7 }, { transform: 'scale(1)' }], T.verdictIn, 'OutBack');
    confetti();
  } else {
    anim($('.word', v), [{ transform: 'translateY(-700px)' }, { transform: 'translateY(0)', offset: .6 }, { transform: 'translateY(-40px)', offset: .8 }, { transform: 'translateY(0)' }], T.verdictIn + 200, 'OutQuad');
  }
  anim($('.why', v), [{ opacity: 0, transform: 'translateY(30px)' }, { opacity: 1, transform: 'none' }], 300, 'OutCubic', { delay: D(T.verdictIn) });
  await sleep(T.verdictIn + T.verdictHold);
  v.remove(); $('#screen-battle').classList.remove('grayout');
  showResult();
}
function confetti() {
  const colors = ['#ffd23f', '#3f9a5a', '#3a7bc0', '#ff7a1a', '#b5472f', '#8a5bb5', '#fffaf0'];
  for (let k = 0; k < 60; k++) {
    const c = h('div', 'confetti'); c.style.background = pickOne(colors);
    $('#fx').appendChild(c);
    const x = rnd(1080), dx = rnd(300) - 150, r = rnd(720) - 360;
    anim(c, [{ transform: `translate(${x}px,-60px) rotate(0)` }, { transform: `translate(${x + dx}px,${1500 + rnd(400)}px) rotate(${r}deg)` }], T.confetti + rnd(600), 'OutQuad', { delay: D(rnd(400)) }).then(() => c.remove());
  }
}
function showResult() {
  closeAllOverlays();
  const w = S.over ? S.over.winner : S.human;
  const win = w === S.human, draw = w == null;
  const t = $('#res-title'); t.textContent = draw ? '引き分け' : win ? '勝利!' : '敗北…'; t.className = draw ? 'draw' : win ? '' : 'lose';
  const mark = $('#res-emblem .mark');
  mark.textContent = w === 'pest' ? '守' : w === 'termite' ? '蝕' : '−';
  mark.style.background = w ? `var(--${w})` : 'var(--muted)';
  $('#res-reason').innerHTML = (w ? `<span style="color:var(--${w === 'pest' ? 'pest-soft' : 'termite-soft'})">${SIDE_LABEL[w]}の勝ち</span><br>` : '') + (S.over ? S.over.text : '');
  $('#res-stats').innerHTML =
    `<div class="row"><span>あなた</span><b>${SIDE_LABEL[S.human]}</b></div>` +
    `<div class="row"><span>完全侵食</span><b><span class="cnt" data-to="${S.destroyed}">0</span>/5</b></div>` +
    `<div class="row"><span>ターン</span><b class="cnt" data-to="${S.turn}">0</b></div>` +
    `<div class="row"><span>庭の枯れ木</span><b>${S.treeFallen ? '倒れた' : '倒れていない'}</b></div>` +
    `<div class="row"><span>シロアリ山札 残り</span><b class="cnt" data-to="${S.deck.termite}">0</b></div>`;
  show('screen-result');
  anim($('#res-emblem'), [{ transform: 'scale(.2) rotate(-20deg)', opacity: 0 }, { transform: 'none', opacity: 1 }], 500, 'OutBack');
  anim(t, [{ transform: 'scale(2)', opacity: 0 }, { transform: 'none', opacity: 1 }], T.stamp + 100, 'OutBack', { delay: D(250) });
  $$('#res-stats .row').forEach((r, k) => anim(r, [{ opacity: 0, transform: 'translateX(-40px)' }, { opacity: 1, transform: 'none' }], 300, 'OutCubic', { delay: D(500 + k * 100), fill: 'backwards' }));
  $$('#res-stats .cnt').forEach(e => countUp(e, +e.dataset.to, 600, 600));
  ['#res-again', '#res-swap'].forEach((q, k) => anim($(q), [{ opacity: 0, transform: 'translateY(60px)' }, { opacity: 1, transform: 'none' }], 300, 'OutCubic', { delay: D(1100 + k * 80), fill: 'backwards' }));
}
function countUp(el, to, ms, delay) {
  const t0 = performance.now() + D(delay);
  const step = now => { const k = Math.max(0, Math.min(1, (now - t0) / D(ms))); el.textContent = Math.round(to * (1 - Math.pow(1 - k, 2))); if (k < 1) requestAnimationFrame(step); };
  requestAnimationFrame(step);
}

// ───────── ログと速報 ─────────
function renderLog() {
  const box = $('#log .lines'); if (!S) { box.innerHTML = ''; return; }
  box.innerHTML = S.log.map(e => {
    const color = e.side === 'pest' ? '#74cdbf' : '#f29a85';
    if (e.text.startsWith('―')) return `<div class="t" style="color:${color}">${e.text}</div>`;
    return `<div><span style="color:${color}">${e.side === S.human ? 'あなた' : 'CPU'}</span> ${e.text}</div>`;
  }).join('');
  box.scrollTop = box.scrollHeight;
}
function openLog() {
  renderLog();
  $('#log-shade').style.display = 'block';
  anim($('#log'), [{ transform: 'translateX(780px)' }, { transform: 'none' }], T.logIn, 'OutCubic');
}
function closeLog() {
  $('#log-shade').style.display = 'none';
  anim($('#log'), [{ transform: 'none' }, { transform: 'translateX(780px)' }], T.logIn, 'InCubic');
}
let tickerAnim = null;
function ticker(text, warn) {
  const s = $('#ticker span'); s.textContent = text; s.style.background = warn ? 'var(--danger)' : '';
  if (tickerAnim) tickerAnim.cancel();
  tickerAnim = s.animate([{ opacity: 0, transform: 'translateY(-10px)' }, { opacity: 1, transform: 'none', offset: .08 }, { opacity: 1, offset: .85 }, { opacity: 0 }], { duration: D(T.ticker + 450), easing: 'linear', fill: 'forwards' });
}
function closeAllOverlays() {
  $('#detail').classList.remove('on'); $('#sheet').classList.remove('on');
  $('#log-shade').style.display = 'none'; $('#log').getAnimations().forEach(a => a.cancel());
  $('#fx').innerHTML = '';
}

// ───────── 対戦の開始 ─────────
async function startBattle(human, opts = {}) {
  closeAllOverlays();
  S = newState(human); lastGoal = {};
  $('#field').querySelectorAll('.tile').forEach(t => t.remove());
  show('screen-battle');
  renderAll();
  if (opts.quick) return;
  S.busy = true;
  // コイントス(できれば)
  const first = Math.random() < 0.5 ? 'pest' : 'termite';
  const coin = h('div', 'coin', '?'); coin.style.background = 'var(--muted)'; $('#fx').appendChild(coin);
  await anim(coin, [{ transform: 'translateY(300px) rotateY(0)' }, { transform: 'translateY(-120px) rotateY(900deg)', offset: .5 }, { transform: 'translateY(0) rotateY(1800deg)' }], T.coin, 'OutCubic');
  coin.textContent = first === 'pest' ? '防' : 'シ'; coin.style.background = `var(--${first})`;
  await anim(coin, [{ transform: 'scale(1)' }, { transform: 'scale(1.2)' }, { transform: 'scale(1)' }], 250, 'OutBack');
  log(first, `コイントスの結果、${SIDE_LABEL[first]}が先攻`);
  await sleep(300); coin.remove();
  await banner(`${SIDE_LABEL[first]}が先攻`, first, 'コイントスの結果');
  // 初期手札を配る
  S.oppHand = 0;
  const dealOpp = (async () => { for (let k = 0; k < 5; k++) { S.oppHand++; S.deck[S.cpu]--; renderOpp(); const bs = $$('#opp-hand .back'); anim(bs[bs.length - 1], [{ transform: 'translateY(-200px)', opacity: 0 }, { opacity: 1 }], T.deal, 'OutCubic', { fill: 'none' }); await sleep(T.dealStagger); } })();
  S.deck[S.human] += 0;
  await Promise.all([dealOpp, drawCards(5, '初期手札')]);
  S.busy = false;
  if (first === S.human) startMyTurn(); else cpuTurn();
}

// 途中の盤面(デバッグ用)
function setupMidgame(human) {
  S = newState(human); lastGoal = {};
  $('#field').querySelectorAll('.tile').forEach(t => t.remove());
  const P = S.points, put = (i, id, ex = {}) => P[i].cards.push(Object.assign({ id, guard: false, promote: false }, ex));
  put(0, 'worker-ie');
  put(1, 'worker-yamato'); put(1, 'soldier-yamato', { guard: true });
  P[2].bait = 1;
  put(3, 'worker-ie'); put(3, 'worker-ie');
  put(4, 'worker-ie'); put(4, 'worker-ie');
  P[5].barrier = 2;
  put(6, 'worker-kanzai'); put(6, 'worker-kanzai'); put(6, 'soldier-kanzai'); P[6].destroyed = true;
  put(7, 'winged-kanzai', { promote: true });
  P[1].swarm = human === 'pest' ? false : false;
  S.destroyed = 1; S.turn = 7;
  S.deck = { termite: 24, pest: 31 }; S.discard = { termite: 9, pest: 11 }; S.oppHand = 5;
  const ids = human === 'pest' ? ['spray', 'barrier', 'bait', 'scout', 'foresight'] : ['worker-ie', 'soldier-ie', 'winged-yamato', 'swarm', 'reinforce'];
  S.hand = ids.map(id => ({ uid: uidSeq++, id }));
  S.current = 'me'; S.ap = 3;
  S.log = [];
  log('termite', '― シロアリ側のターン(シロアリ山札25枚・防除山札31枚)');
  log('termite', '職アリ-イエを玄関・框に置いた(2/3)');
  log('termite', '羽アリ-カンザイを屋根裏・天井に置いた(1/3)');
  log('termite', 'シロアリ側のターン終了');
  log('pest', `― 防除業者側のターン(シロアリ山札24枚・防除山札31枚)`);
  closeAllOverlays();
  show('screen-battle'); renderAll();
}

// ───────── ボタンの押し心地(全ボタン共通) ─────────
function bindPress() {
  stage.addEventListener('pointerdown', e => {
    const b = e.target.closest('.btn'); if (!b || b.disabled) return;
    b.getAnimations().forEach(a => a.cancel());
    anim(b, [{ transform: 'none' }, { transform: 'translateY(6px) scale(.94)' }], T.press, 'OutQuad');
    const up = () => { anim(b, [{ transform: 'translateY(6px) scale(.94)' }, { transform: 'none' }], T.release, 'OutBack', { fill: 'none' }); window.removeEventListener('pointerup', up); };
    window.addEventListener('pointerup', up);
  });
}

// ───────── デバッグ ─────────
const DBG = {
  select: () => showSelect(),
  'battle-pest': () => startBattle('pest'),
  'battle-termite': () => startBattle('termite'),
  midgame: () => setupMidgame(S ? S.human : 'pest'),
  'result-win': () => { if (!S) setupMidgame('pest'); S.over = { winner: S.human, text: S.human === 'pest' ? '防除業者側の勝利(シロアリの山札が尽きるまで守りきった)' : 'シロアリ側の勝利(5ポイントを完全侵食)' }; showResult(); },
  draw: () => { ensureBattle(); S.busy = false; drawCards(2, '増援'); },
  cpu: () => { ensureBattle(); closeAllOverlays(); S.busy = false; cpuTurn(); },
  foresight: () => { ensureBattle(); openForesight(); },
  handlimit: async () => { ensureBattle(); await drawCards(3, '増援'); openDiscard(Math.max(1, S.hand.length - 7)); },
  mulligan: () => { ensureBattle(); openMulligan(); },
  scout: () => { ensureBattle(); openScout(); },
  destroy: async () => { ensureBattle(); const i = S.points.findIndex((p, k) => !p.destroyed && !POINTS[k].tree); const sp = POINTS[i].allowed[0]; while (S.points[i].cards.length < req(i) - 1) S.points[i].cards.push({ id: 'worker-' + sp }); renderField(); await sleep(200); await resolve('termite', 'worker-' + sp, i); },
  treefall: async () => { ensureBattle(); const i = 4; while (S.points[i].cards.length < 3) S.points[i].cards.push({ id: 'worker-ie' }); renderField(); await resolve('termite', 'worker-ie', i); },
  promote: async () => { ensureBattle(); const i = S.points.findIndex(p => p.cards.some(c => c.promote)); if (i < 0) { S.points[7].cards.push({ id: 'winged-kanzai', promote: true }); renderField(); return DBG.promote(); } await promote('termite', i, S.points[i].cards.find(c => c.promote)); renderAll(); },
  banner: () => { ensureBattle(); banner('あなたのターン', S.human); },
  win: () => { ensureBattle(); finish(S.human, S.human === 'pest' ? '防除業者側の勝利(シロアリの山札が尽きるまで守りきった)' : 'シロアリ側の勝利(5ポイントを完全侵食)'); },
  lose: () => { ensureBattle(); finish(S.cpu, S.cpu === 'pest' ? '防除業者側の勝利(シロアリの山札が尽きるまで守りきった)' : 'シロアリ側の勝利(5ポイントを完全侵食)'); },
  log: () => { ensureBattle(); openLog(); },
  speed1: () => { SPEED = 1; },
  speed05: () => { SPEED = 0.5; },
};
function ensureBattle() { if (!S || S.over || !$('#screen-battle').classList.contains('on')) setupMidgame(S ? S.human : 'pest'); }

// ───────── 起動 ─────────
function init() {
  fit();
  bindPress(); bindDetail();
  $$('.side-card').forEach(c => c.addEventListener('click', async () => {
    const side = c.dataset.side;
    await anim(c, [{ transform: 'scale(1)' }, { transform: 'scale(1.05)' }, { transform: 'scale(1)' }], 250, 'OutBack', { fill: 'none' });
    startBattle(side);
  }));
  $('#btn-end').addEventListener('click', onEndTurn);
  $('#btn-cancel').addEventListener('click', cancelTarget);
  $('#btn-log').addEventListener('click', openLog);
  $('#btn-log-close').addEventListener('click', closeLog);
  $('#log-shade').addEventListener('click', closeLog);
  $('#res-again').addEventListener('click', () => startBattle(S.human));
  // 決定(2026-11-25):「陣営を変えて遊ぶ」は陣営選択の画面に戻る
  $('#res-swap').addEventListener('click', showSelect);
  $$('#debug button').forEach(b => b.addEventListener('click', () => DBG[b.dataset.dbg]()));
  // 確認用:#run=midgame,foresight のように場面を指定して開ける。&fast=1 で演出を一瞬にする
  const q = new URLSearchParams(location.hash.slice(1));
  if (q.get('fast')) SPEED = 1000;
  showSelect();
  if (q.get('run')) (async () => { for (const k of q.get('run').split(',')) { await DBG[k](); await sleep(50); } })();
}
window.mock = { DBG, get S() { return S; }, setSpeed: v => SPEED = v };
init();
