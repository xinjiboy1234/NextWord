# -*- coding: utf-8 -*-
"""由 output/timeline.json + output/llm-conversations.jsonl 生成交互式查看器：

1. demo/agent-story/timeline.html —— 自包含交互查看器：
   - 左侧：按故事日分组的事件导航（可折叠/按角色过滤/搜索），LLM 调用以内联小条目穿插；
   - 顶部：缩略泳道时间轴（全部节点一屏总览，点击跳转）；
   - 右侧：选中节点完整详情——事件数据 JSON（折叠）或完整 LLM 对话（气泡渲染、
     JSON 美化、token 用量），事件 ↔ 对话互相跳转，←/→ 键盘导航；
2. output/conversations/*.md —— 按序导出的全部 Agent ↔ LLM 对话 markdown。
"""
import io
import json
import sys
from pathlib import Path

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "output"

LANES = ["林晓", "规则引擎", "Profiler Agent", "Verifier Agent",
         "Planner Agent", "Insight Agent", "LLM(qwen-plus)", "系统"]
CALLER_LABEL = {
    "profiler-agent": "Profiler Agent",
    "insight-agent": "Insight Agent",
    "sentence-rating": "造句/测评评分",
    "free-expression-rating": "自由表达评分",
    "reading-lookup": "阅读查词",
    "vocab-extract": "词汇提取",
    "comment-reply": "批注回复",
    "scenario-annotation": "场景标注",
    "unknown": "未识别调用",
}


def load_events():
    p = OUT / "timeline.json"
    return json.loads(p.read_text(encoding="utf-8")) if p.exists() else []


def load_conversations():
    p = OUT / "llm-conversations.jsonl"
    if not p.exists():
        return []
    return [json.loads(line) for line in p.read_text(encoding="utf-8").splitlines() if line.strip()]


def export_conversations_md(convs):
    cdir = OUT / "conversations"
    cdir.mkdir(parents=True, exist_ok=True)
    index = ["# Agent ↔ LLM 对话全记录\n",
             f"共 {len(convs)} 次调用（qwen-plus，经记录代理捕获）。\n"]
    for c in convs:
        caller = c.get("caller", "unknown")
        label = CALLER_LABEL.get(caller, caller)
        fname = f"{c['seq']:03d}-{caller}.md"
        lines = [f"# #{c['seq']} {label}\n",
                 f"- 时间：{c.get('ts')}　耗时：{c.get('latencyMs')}ms　模型：{c.get('model')}"
                 f"　HTTP：{c.get('httpStatus')}",
                 f"- usage：{json.dumps(c.get('usage'), ensure_ascii=False)}\n"]
        for m in c.get("messages") or []:
            lines.append(f"## [{m.get('role')}]\n\n```\n{m.get('content', '')}\n```\n")
        lines.append(f"## [assistant 响应]\n\n```\n{c.get('responseText', '')}\n```\n")
        (cdir / fname).write_text("\n".join(lines), encoding="utf-8")
        preview = (c.get("responseText") or "").replace("\n", " ")[:80]
        index.append(f"- [#{c['seq']} {label}]({fname}) — {c.get('ts')}，{c.get('latencyMs')}ms：{preview}")
    (cdir / "index.md").write_text("\n".join(index), encoding="utf-8")


def build_items(events, convs):
    """合并剧本事件与 LLM 调用为统一节点序列，并建立双向关联。

    归属规则：带 Agent 角色的调用（profiler/insight）在时间窗口内优先归属到
    对应 Agent 的剧情事件；其余调用（评分/查词等）归属到其后最近的剧情事件。
    （时间戳粒度为秒，同一秒可能落多个事件，纯 ts 比较会错挂。）
    """
    CALLER_ACTOR = {"profiler-agent": "Profiler Agent", "insight-agent": "Insight Agent"}

    def to_epoch(ts):
        import datetime
        return datetime.datetime.strptime(ts, "%Y-%m-%dT%H:%M:%S").timestamp()

    # 预计算每条 LLM 调用的父事件下标
    parent_of = {}
    for ci, c in enumerate(convs):
        want = CALLER_ACTOR.get(c.get("caller"))
        best, best_dt = None, 301
        cts = to_epoch(c["ts"])
        if want:
            for ei, e in enumerate(events):
                if e.get("actor") != want:
                    continue
                dt = to_epoch(e["ts"]) - cts
                if -30 <= dt < best_dt:  # 允许事件略早于调用返回（同一轮）
                    best, best_dt = ei, dt
        if best is None:
            for ei, e in enumerate(events):
                if e["ts"] >= c["ts"]:
                    best = ei
                    break
        parent_of[ci] = best  # None = 尾部游离

    items = []
    llm_sorted = sorted(range(len(convs)), key=lambda i: convs[i]["ts"])

    def make_llm(c, story, parent):
        return {"kind": "llm", "ts": c["ts"], "story": story,
                "actor": "LLM(qwen-plus)", "seq": c["seq"],
                "caller": c.get("caller"), "callerLabel": CALLER_LABEL.get(c.get("caller"), c.get("caller")),
                "action": f"LLM #{c['seq']} {CALLER_LABEL.get(c.get('caller'), c.get('caller'))}",
                "latencyMs": c.get("latencyMs"), "model": c.get("model"),
                "httpStatus": c.get("httpStatus"), "usage": c.get("usage"),
                "messages": c.get("messages") or [],
                "responseText": c.get("responseText") or "",
                "conversationFile": f"output/conversations/{c['seq']:03d}-{c.get('caller')}.md",
                "parent": parent}

    for ei, e in enumerate(events):
        attached = [ci for ci in llm_sorted if parent_of[ci] == ei]
        refs = []
        for ci in attached:
            c = convs[ci]
            items.append(make_llm(c, e.get("story", ""), len(items) + (len(attached) - attached.index(ci))))
            refs.append(len(items) - 1)
        items.append({"kind": "event", "seq": e.get("seq"), "ts": e["ts"], "story": e.get("story", ""),
                      "actor": e.get("actor", "系统"), "action": e.get("action", ""),
                      "detail": e.get("detail", ""), "data": e.get("data"),
                      "llmRefs": refs})
    for ci in llm_sorted:  # 尾部游离调用
        if parent_of[ci] is None:
            items.append(make_llm(convs[ci], "", None))
    return items


def build_html(events, convs):
    items = build_items(events, convs)
    # 故事日色带（按节点下标范围）
    bands, seen = [], {}
    for idx, it in enumerate(items):
        s = it.get("story") or None
        if s and s not in seen:
            seen[s] = len(bands)
            bands.append({"name": s, "start": idx, "end": idx})
        elif s:
            bands[seen[s]]["end"] = idx
    total_tokens = sum((c.get("usage") or {}).get("total_tokens", 0) for c in convs)
    total_ms = sum(c.get("latencyMs", 0) for c in convs)
    stats = {"events": sum(1 for i in items if i["kind"] == "event"),
             "llm": len(convs), "tokens": total_tokens, "llmSeconds": round(total_ms / 1000, 1)}
    data = {"lanes": LANES, "items": items, "bands": bands, "stats": stats}
    payload = json.dumps(data, ensure_ascii=False).replace("</", "<\\/")
    html = TEMPLATE.replace("__DATA__", payload)
    (ROOT / "timeline.html").write_text(html, encoding="utf-8")
    print(f"timeline.html 生成：{len(items)} 个节点（事件 {stats['events']} + LLM {stats['llm']}），"
          f"{len(html) // 1024} KB")


TEMPLATE = r"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>《林晓的七天》— NextWord Agent 协作查看器</title>
<style>
:root {
  --c-user:#4f8ef7; --c-rule:#8a94a6; --c-profiler:#9b59d0; --c-verifier:#d0a020;
  --c-planner:#2fae6e; --c-insight:#e05c7a; --c-llm:#f0762b; --c-sys:#5b6470;
  --bg:#f4f6f9; --panel:#fff; --line:#e3e7ee; --text:#232a33; --muted:#7a8494;
}
* { box-sizing: border-box; }
html,body { height: 100%; margin: 0; }
body { font-family: "Microsoft YaHei","PingFang SC",sans-serif; background: var(--bg); color: var(--text);
       display: flex; flex-direction: column; overflow: hidden; }
header { background:#1f2733; color:#fff; padding: 10px 18px; display:flex; align-items:center; gap:18px; flex-wrap:wrap; }
header h1 { font-size:16px; margin:0; white-space:nowrap; }
header .stats { font-size:12px; color:#aab4c2; }
header .filters { display:flex; gap:6px; flex-wrap:wrap; align-items:center; }
.chip { border:1px solid #3a4657; background:#2a3546; color:#cfd8e3; border-radius:12px;
        padding:2px 10px; font-size:12px; cursor:pointer; user-select:none; }
.chip.off { opacity:.35; }
.chip .dotc { display:inline-block; width:8px; height:8px; border-radius:50%; margin-right:5px; }
#search { border:1px solid #3a4657; background:#141b25; color:#fff; border-radius:12px;
          padding:3px 12px; font-size:12px; width:170px; outline:none; }
#overview { background:var(--panel); border-bottom:1px solid var(--line); padding:6px 14px 2px; }
#overview svg { display:block; width:100%; height:120px; }
#bands { display:flex; gap:2px; font-size:11px; color:var(--muted); margin-bottom:2px; }
#bands span { overflow:hidden; white-space:nowrap; text-overflow:ellipsis; border-left:3px solid var(--line); padding-left:4px; }
#main { flex:1; display:flex; min-height:0; }
#sidebar { width:360px; min-width:360px; overflow-y:auto; background:var(--panel); border-right:1px solid var(--line); }
.day > .day-head { position:sticky; top:0; background:#eef1f6; padding:6px 12px; font-size:12px; font-weight:700;
                   cursor:pointer; display:flex; justify-content:space-between; z-index:2; border-bottom:1px solid var(--line); }
.day-items { }
.item { padding:7px 12px 7px 10px; border-bottom:1px solid #f0f2f6; cursor:pointer; display:flex; gap:8px; align-items:flex-start; }
.item:hover { background:#f2f7ff; }
.item.active { background:#e3efff; border-left:3px solid var(--c-user); padding-left:7px; }
.item .adot { flex:none; width:10px; height:10px; border-radius:50%; margin-top:4px; }
.item .txt { flex:1; min-width:0; }
.item .act { font-size:13px; line-height:1.35; display:-webkit-box; -webkit-line-clamp:2; -webkit-box-orient:vertical; overflow:hidden; }
.item .meta { font-size:11px; color:var(--muted); margin-top:2px; }
.item.llm { background:#fdf6ef; }
.item.llm.active { background:#ffe9d6; border-left:3px solid var(--c-llm); }
.item.llm .adot { border-radius:2px; }
#detail { flex:1; overflow-y:auto; padding:22px 28px; }
.card { max-width:980px; margin:0 auto; background:var(--panel); border:1px solid var(--line);
        border-radius:10px; padding:22px 26px; }
.card h2 { margin:0 0 6px; font-size:18px; line-height:1.4; }
.badges { display:flex; gap:8px; flex-wrap:wrap; margin-bottom:12px; }
.badge { font-size:12px; border-radius:10px; padding:2px 10px; background:#eef1f6; color:#55607a; }
.badge.actor { color:#fff; }
.detail-text { font-size:14px; line-height:1.7; background:#f8f9fc; border-radius:8px; padding:12px 14px; white-space:pre-wrap; word-break:break-word; }
.nav { display:flex; gap:10px; margin:16px 0 0; }
button.navb { border:1px solid var(--line); background:#fff; border-radius:8px; padding:6px 16px;
              font-size:13px; cursor:pointer; }
button.navb:hover { background:#eef4ff; }
.refs { margin-top:16px; }
.refs h3, .data h3 { font-size:13px; color:var(--muted); margin:14px 0 6px; }
.refb { display:inline-block; margin:0 8px 8px 0; border:1px solid #f3c9a0; background:#fdf3e7; color:#a55a18;
        border-radius:8px; padding:5px 12px; font-size:12px; cursor:pointer; }
.refb:hover { background:#ffe7cc; }
details.data { margin-top:14px; }
details.data summary { font-size:13px; color:var(--muted); cursor:pointer; }
pre.json { background:#0f1722; color:#d3e0f0; border-radius:8px; padding:14px; font-size:12px;
           overflow:auto; max-height:420px; line-height:1.5; }
.msg { margin:10px 0; border-radius:10px; overflow:hidden; border:1px solid var(--line); }
.msg .role { font-size:12px; font-weight:700; padding:5px 12px; }
.msg.system .role { background:#e8ebf1; color:#5b6470; }
.msg.user .role { background:#dbe9ff; color:#2f5ea8; }
.msg.assistant .role { background:#ffe3c7; color:#a55a18; }
.msg pre { margin:0; padding:12px 14px; font-size:12.5px; line-height:1.6; white-space:pre-wrap;
           word-break:break-word; max-height:480px; overflow-y:auto; background:#fbfcfe; }
.kv { display:flex; gap:16px; flex-wrap:wrap; font-size:12.5px; color:#556; margin:8px 0 14px; }
.kv b { color:var(--text); }
.hidden { display:none !important; }
.empty { color:var(--muted); font-size:13px; padding:20px; text-align:center; }
</style>
</head>
<body>
<header>
  <h1>《林晓的七天》Agent 协作查看器</h1>
  <div class="stats" id="stats"></div>
  <div class="filters" id="filters"></div>
  <input id="search" placeholder="搜索事件 / 对话…">
</header>
<div id="overview">
  <div id="bands"></div>
  <svg id="ov" preserveAspectRatio="none"></svg>
</div>
<div id="main">
  <div id="sidebar"></div>
  <div id="detail"><div class="empty">← 点击左侧事件或顶部时间轴节点开始浏览（←/→ 键翻页）</div></div>
</div>
<script>
const DATA = __DATA__;
const LANES = DATA.lanes, ITEMS = DATA.items, BANDS = DATA.bands;
const COLORS = {"林晓":"#4f8ef7","规则引擎":"#8a94a6","Profiler Agent":"#9b59d0","Verifier Agent":"#d0a020",
  "Planner Agent":"#2fae6e","Insight Agent":"#e05c7a","LLM(qwen-plus)":"#f0762b","系统":"#5b6470"};
const SHORT = {"林晓":"林晓","规则引擎":"规则","Profiler Agent":"画像","Verifier Agent":"核查",
  "Planner Agent":"计划","Insight Agent":"洞察","LLM(qwen-plus)":"LLM","系统":"系统"};
let activeFilters = new Set(LANES);
let query = "";
let sel = -1;

/* ── 过滤 ── */
function visible(it) {
  if (!activeFilters.has(it.actor)) return false;
  if (query) {
    const hay = (it.action + " " + (it.detail||"") + " " + (it.callerLabel||"") + " " +
                 (it.responseText||"")).toLowerCase();
    if (!hay.includes(query)) return false;
  }
  return true;
}
const visibleIdx = () => ITEMS.map((it,i)=>visible(it)?i:-1).filter(i=>i>=0);

/* ── 头部 ── */
document.getElementById('stats').textContent =
  `剧本事件 ${DATA.stats.events} · LLM 调用 ${DATA.stats.llm} · ${DATA.stats.tokens} tokens · LLM 耗时 ${DATA.stats.llmSeconds}s`;
const filtersEl = document.getElementById('filters');
LANES.forEach(l => {
  const c = document.createElement('span');
  c.className = 'chip'; c.dataset.lane = l;
  c.innerHTML = `<span class="dotc" style="background:${COLORS[l]}"></span>${l}`;
  c.onclick = () => { activeFilters.has(l) ? activeFilters.delete(l) : activeFilters.add(l);
                      c.classList.toggle('off'); renderSidebar(); renderOverview(); };
  filtersEl.appendChild(c);
});
document.getElementById('search').oninput = e => { query = e.target.value.trim().toLowerCase(); renderSidebar(); renderOverview(); };

/* ── 顶部缩略时间轴 ── */
const NS = 'http://www.w3.org/2000/svg';
function renderOverview() {
  const svg = document.getElementById('ov');
  const W = svg.clientWidth || 1000, H = 120, TOP = 6;
  svg.setAttribute('viewBox', `0 0 ${W} ${H}`);
  svg.innerHTML = '';
  const laneH = (H - TOP - 4) / LANES.length;
  LANES.forEach((l, li) => {
    const y = TOP + li * laneH + laneH/2;
    const line = document.createElementNS(NS,'line');
    line.setAttribute('x1',0); line.setAttribute('x2',W);
    line.setAttribute('y1',y); line.setAttribute('y2',y);
    line.setAttribute('stroke','#e8ecf2');
    svg.appendChild(line);
    const t = document.createElementNS(NS,'text');
    t.setAttribute('x',2); t.setAttribute('y',y+3); t.setAttribute('font-size',9);
    t.setAttribute('fill', COLORS[l]); t.textContent = SHORT[l] || l;
    svg.appendChild(t);
  });
  const n = ITEMS.length;
  ITEMS.forEach((it, i) => {
    if (!visible(it)) return;
    const li = LANES.indexOf(it.actor); if (li < 0) return;
    const x = 70 + (W-85) * (n<=1?0:i/(n-1));
    const y = TOP + li * laneH + laneH/2;
    const c = document.createElementNS(NS, it.kind==='llm'?'rect':'circle');
    if (it.kind==='llm') { c.setAttribute('x',x-3); c.setAttribute('y',y-3);
      c.setAttribute('width',6); c.setAttribute('height',6); c.setAttribute('rx',1.5); }
    else { c.setAttribute('cx',x); c.setAttribute('cy',y); c.setAttribute('r', i===sel?6:4); }
    c.setAttribute('fill', COLORS[it.actor]);
    if (i===sel) { c.setAttribute('stroke','#1f2733'); c.setAttribute('stroke-width',1.5); }
    c.style.cursor = 'pointer';
    const title = document.createElementNS(NS,'title');
    title.textContent = `[${it.story||'—'}] ${it.action}`;
    c.appendChild(title);
    c.onclick = () => select(i);
    svg.appendChild(c);
  });
  // 色带标注
  const bandsEl = document.getElementById('bands');
  bandsEl.innerHTML = '';
  BANDS.forEach(b => {
    const s = document.createElement('span');
    s.style.width = ((b.end-b.start+1)/ITEMS.length*100) + '%';
    s.textContent = b.name;
    bandsEl.appendChild(s);
  });
}

/* ── 左侧导航 ── */
function renderSidebar() {
  const sb = document.getElementById('sidebar');
  sb.innerHTML = '';
  let curDay = null, dayBox = null, dayCount = 0;
  ITEMS.forEach((it, i) => {
    if (!visible(it)) return;
    const day = it.story || '（无剧情标签）';
    if (day !== curDay) {
      curDay = day;
      const dayEl = document.createElement('div'); dayEl.className = 'day';
      const head = document.createElement('div'); head.className = 'day-head';
      head.innerHTML = `<span>${day}</span><span class="cnt"></span>`;
      dayBox = document.createElement('div'); dayBox.className = 'day-items';
      head.onclick = () => dayBox.classList.toggle('hidden');
      dayEl.appendChild(head); dayEl.appendChild(dayBox);
      sb.appendChild(dayEl);
      dayCount = 0;
    }
    dayCount++;
    dayBox.previousSibling.querySelector('.cnt').textContent = dayCount;
    const el = document.createElement('div');
    el.className = 'item' + (it.kind==='llm'?' llm':'') + (i===sel?' active':'');
    el.dataset.idx = i;
    const color = COLORS[it.actor] || '#888';
    const meta = it.kind==='llm'
      ? `${it.ts.slice(11)} · ${it.latencyMs}ms · ${((it.usage||{}).total_tokens)||'?'} tok`
      : `${it.ts.slice(11)} · ${it.actor}`;
    el.innerHTML = `<span class="adot" style="background:${color}"></span>
      <span class="txt"><span class="act">${esc(it.action)}</span>
      <span class="meta">${meta}</span></span>`;
    el.onclick = () => select(i);
    dayBox.appendChild(el);
  });
}

/* ── 详情 ── */
function esc(s){ return String(s??'').replace(/&/g,'&amp;').replace(/</g,'&lt;'); }
function tryJson(s){ try { return JSON.stringify(JSON.parse(s), null, 2); } catch { return null; } }

function select(i) {
  sel = i;
  const it = ITEMS[i];
  document.querySelectorAll('.item').forEach(el => el.classList.toggle('active', +el.dataset.idx===i));
  const active = document.querySelector('.item.active');
  if (active) active.scrollIntoView({block:'nearest'});
  renderDetail(it, i);
  renderOverview();
}

function navButtons(i) {
  const vis = visibleIdx();
  const pos = vis.indexOf(i);
  const prev = pos>0 ? vis[pos-1] : null, next = pos>=0 && pos<vis.length-1 ? vis[pos+1] : null;
  return `<div class="nav">
    ${prev!==null?`<button class="navb" onclick="select(${prev})">← 上一个</button>`:''}
    ${next!==null?`<button class="navb" onclick="select(${next})">下一个 →</button>`:''}
  </div>`;
}

function renderDetail(it, i) {
  const d = document.getElementById('detail');
  const color = COLORS[it.actor] || '#888';
  let html = `<div class="card"><h2>${esc(it.action)}</h2>
    <div class="badges">
      <span class="badge actor" style="background:${color}">${esc(it.actor)}</span>
      <span class="badge">故事时间：${esc(it.story||'—')}</span>
      <span class="badge">真实时间：${esc(it.ts)}</span>
      ${it.kind==='llm'?`<span class="badge">${esc(it.callerLabel||'')}</span>`:''}
    </div>`;
  if (it.kind === 'event') {
    if (it.detail) html += `<div class="detail-text">${esc(it.detail)}</div>`;
    if (it.llmRefs && it.llmRefs.length) {
      html += `<div class="refs"><h3>本节点前后的 LLM 调用（${it.llmRefs.length}）</h3>` +
        it.llmRefs.map(r => `<span class="refb" onclick="select(${r})">⚡ ${esc(ITEMS[r].action)} · ${ITEMS[r].latencyMs}ms</span>`).join('') + `</div>`;
    }
    if (it.data) html += `<details class="data"><summary>原始数据（JSON）</summary>
      <pre class="json">${esc(JSON.stringify(it.data, null, 2))}</pre></details>`;
  } else {
    html += `<div class="kv"><span>模型：<b>${esc(it.model||'')}</b></span>
      <span>耗时：<b>${it.latencyMs}ms</b></span>
      <span>HTTP：<b>${it.httpStatus}</b></span>
      <span>tokens：<b>${JSON.stringify(it.usage||{})}</b></span></div>`;
    if (it.parent !== null && it.parent !== undefined)
      html += `<div class="refs"><span class="refb" onclick="select(${it.parent})">↩ 所属剧情节点：${esc(ITEMS[it.parent].action)}</span></div>`;
    (it.messages||[]).forEach(m => {
      const pretty = tryJson(m.content||'');
      html += `<div class="msg ${esc(m.role)}"><div class="role">${esc(m.role)}</div>
        <pre>${esc(pretty ?? m.content ?? '')}</pre></div>`;
    });
    const pretty = tryJson(it.responseText||'');
    html += `<div class="msg assistant"><div class="role">assistant（响应${pretty?' · 已美化 JSON':''}）</div>
      <pre>${esc(pretty ?? it.responseText ?? '')}</pre></div>`;
    if (it.conversationFile) html += `<div class="kv"><span>完整对话文件：<b>${esc(it.conversationFile)}</b></span></div>`;
  }
  html += navButtons(i) + `</div>`;
  d.innerHTML = html;
  d.scrollTop = 0;
}

document.addEventListener('keydown', e => {
  if (e.target.tagName === 'INPUT') return;
  const vis = visibleIdx(); if (!vis.length) return;
  let pos = vis.indexOf(sel);
  if (e.key === 'ArrowRight') select(pos<0?vis[0]:vis[Math.min(pos+1, vis.length-1)]);
  if (e.key === 'ArrowLeft') select(pos<0?vis[0]:vis[Math.max(pos-1, 0)]);
});
window.addEventListener('resize', renderOverview);

renderSidebar(); renderOverview();
// 默认选中第一个事件
const first = visibleIdx()[0];
if (first !== undefined) select(first);
</script>
</body>
</html>
"""


def main():
    events = load_events()
    convs = load_conversations()
    export_conversations_md(convs)
    build_html(events, convs)
    print(f"对话导出 {len(convs)} 篇 -> output/conversations/")


if __name__ == "__main__":
    main()
