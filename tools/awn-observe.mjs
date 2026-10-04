#!/usr/bin/env node
// awn-observe — attach to a RUNNING AutoWebNav host app (KdpPublish, JobHunt, Automata, ...) over
// the Chrome DevTools Protocol and report what it is doing, live. No dependencies: Node 22+'s
// built-in WebSocket and fetch, raw CDP.
//
// Every AutoWebNav host registers itself through AutoWebNav.WebView2.LiveObservation into
//   %LocalAppData%\MindAttic\AutoWebNav\instances\<App>-<PID>.json
// with one CDP port per pane (e.g. KdpPublish: panel, board). This tool reads that registry, so
// it always finds the right instance and port — never a guess, and it follows the app across a
// restart/redeploy.
//
// A page may define window.__awnObserve = () => ({ running, summary, log: [...] }) — a host's own
// control panel does — and this tool reports that instead of a generic page summary.
//
// Usage (the app selector is an app-name substring or a PID; optional when one app is running):
//   node awn-observe.mjs list
//   node awn-observe.mjs status [app] [--pane name] [--json]
//   node awn-observe.mjs log    [app] [--pane name] [--tail 40]
//   node awn-observe.mjs watch  [app] [--interval 1000]        one line per event, runs until killed
//   node awn-observe.mjs shot   [app] [--pane name] [--out file.png]
//   node awn-observe.mjs dom    [app]  --pane name [--selector css] [--max 20000]
//   node awn-observe.mjs eval   [app]  --pane name --expr "document.title"
//
// status/log/watch/shot/dom only READ the page. eval runs whatever expression it is given — use it
// to read, not to drive the app behind the person using it.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const REGISTRY = path.join(
  process.env.LOCALAPPDATA || path.join(os.homedir(), 'AppData', 'Local'),
  'MindAttic', 'AutoWebNav', 'instances');

// ── args ─────────────────────────────────────────────────────────────────────────────────────────
const argv = process.argv.slice(2);
const flags = {};
const positional = [];
for (let i = 0; i < argv.length; i++) {
  const a = argv[i];
  if (a.startsWith('--')) {
    const [k, inline] = a.slice(2).split(/=(.*)/s);
    if (inline !== undefined) flags[k] = inline;
    else if (i + 1 < argv.length && !argv[i + 1].startsWith('--')) flags[k] = argv[++i];
    else flags[k] = true;
  } else positional.push(a);
}
const [command = 'help', selector] = positional;

function fail(message) { console.error(message); process.exit(1); }
const stamp = () => new Date().toTimeString().slice(0, 8);

// ── registry ─────────────────────────────────────────────────────────────────────────────────────
function alive(pid) {
  try { process.kill(pid, 0); return true; } catch (e) { return e.code === 'EPERM'; }
}

function instances() {
  if (!fs.existsSync(REGISTRY)) return [];
  const found = [];
  for (const f of fs.readdirSync(REGISTRY).filter(f => f.endsWith('.json'))) {
    try {
      const entry = JSON.parse(fs.readFileSync(path.join(REGISTRY, f), 'utf8'));
      if (alive(entry.pid)) found.push(entry);
    } catch { /* half-written or unreadable — skip this round */ }
  }
  return found.sort((a, b) => a.app.localeCompare(b.app) || a.pid - b.pid);
}

function resolve(sel, { quiet = false } = {}) {
  const all = instances();
  let matches = all;
  if (sel !== undefined) {
    matches = /^\d+$/.test(sel)
      ? all.filter(i => i.pid === Number(sel))
      : all.filter(i => i.app.toLowerCase().includes(String(sel).toLowerCase()));
  }
  if (matches.length === 1) return matches[0];
  if (quiet) return null;
  if (matches.length === 0) {
    fail(all.length === 0
      ? `No AutoWebNav app is registered in ${REGISTRY}. Is one running (built against AutoWebNav 3+)?`
      : `No running app matches "${sel}". Running: ${all.map(i => `${i.app} (PID ${i.pid})`).join(', ')}`);
  }
  fail(`Several apps match — pass a name or PID: ${matches.map(i => `${i.app} (PID ${i.pid})`).join(', ')}`);
}

function panesOf(inst, wanted) {
  const names = Object.keys(inst.panes || {});
  if (!wanted) return names;
  const hit = names.find(n => n.toLowerCase() === String(wanted).toLowerCase());
  if (!hit) fail(`${inst.app} has no pane "${wanted}". Panes: ${names.join(', ') || '(none registered yet)'}`);
  return [hit];
}

// ── CDP ──────────────────────────────────────────────────────────────────────────────────────────
class Cdp {
  static async open(wsUrl) {
    const ws = new WebSocket(wsUrl);
    await new Promise((res, rej) => {
      ws.onopen = res;
      ws.onerror = () => rej(new Error(`could not connect to ${wsUrl}`));
    });
    return new Cdp(ws);
  }

  constructor(ws) {
    this.ws = ws;
    this.nextId = 0;
    this.pending = new Map();
    this.closed = false;
    ws.onmessage = e => {
      const m = JSON.parse(typeof e.data === 'string' ? e.data : e.data.toString());
      const p = m.id !== undefined && this.pending.get(m.id);
      if (!p) return;
      this.pending.delete(m.id);
      clearTimeout(p.timer);
      if (m.error) p.rej(new Error(m.error.message)); else p.res(m.result);
    };
    ws.onclose = () => {
      this.closed = true;
      for (const p of this.pending.values()) { clearTimeout(p.timer); p.rej(new Error('connection closed')); }
      this.pending.clear();
    };
  }

  send(method, params = {}, timeoutMs = 15000) {
    if (this.closed) return Promise.reject(new Error('connection closed'));
    const id = ++this.nextId;
    return new Promise((res, rej) => {
      const timer = setTimeout(() => { this.pending.delete(id); rej(new Error(`${method} timed out`)); }, timeoutMs);
      this.pending.set(id, { res, rej, timer });
      this.ws.send(JSON.stringify({ id, method, params }));
    });
  }

  async evaluate(expression) {
    const r = await this.send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
    if (r.exceptionDetails) {
      throw new Error(r.exceptionDetails.exception?.description || r.exceptionDetails.text || 'evaluation failed');
    }
    return r.result.value;
  }

  close() { try { this.ws.close(); } catch { /* already gone */ } }
}

async function pageTarget(port) {
  const res = await fetch(`http://127.0.0.1:${port}/json/list`);
  const pages = (await res.json()).filter(t => t.type === 'page' && !t.url.startsWith('devtools://'));
  if (pages.length === 0) throw new Error(`no page on port ${port}`);
  return pages.find(p => p.url && p.url !== 'about:blank') || pages[0];
}

async function connect(inst, pane) {
  const target = await pageTarget(inst.panes[pane]);
  return Cdp.open(target.webSocketDebuggerUrl);
}

// ── page scripts ─────────────────────────────────────────────────────────────────────────────────
// What the app says about itself (window.__awnObserve) when the page offers it; otherwise a generic
// read of the page: heading, visible banners/dialogs, enabled buttons, unticked checkboxes.
const SNAPSHOT = String.raw`(() => {
  const vis = el => { const r = el.getBoundingClientRect(); return r.width > 0 && r.height > 0; };
  const t = s => (s || '').trim().replace(/\s+/g, ' ');
  const short = (s, n) => { s = t(s); return s.length > n ? s.slice(0, n) + '…' : s; };
  const uniq = a => [...new Set(a.filter(Boolean))];
  let app = null;
  if (typeof window.__awnObserve === 'function') {
    try { app = window.__awnObserve(); } catch (e) { app = { error: String(e) }; }
  }
  const heading = document.querySelector('h1, h2');
  const banners = uniq([...document.querySelectorAll(
      '[role=alert], [role=dialog], [class*=alert], [class*=error], [class*=banner], [class*=success]')]
    .filter(vis).map(e => short(e.textContent, 160))).slice(0, 8);
  const buttons = uniq([...document.querySelectorAll('button, input[type=submit], input[type=button], [role=button]')]
    .filter(e => vis(e) && !e.disabled)
    .map(e => short(e.tagName === 'INPUT' ? e.value : (e.textContent || e.getAttribute('aria-label')), 40))).slice(0, 25);
  const unchecked = [...document.querySelectorAll('input[type=checkbox], [role=checkbox]')]
    .filter(e => vis(e) && !(e.tagName === 'INPUT' ? e.checked : e.getAttribute('aria-checked') === 'true'))
    .map(e => short((e.closest('label') || e.parentElement || e).textContent, 100));
  return { url: location.href, title: document.title, heading: heading ? short(heading.textContent, 120) : null,
           banners, buttons, unchecked, app };
})()`;

// ── commands ─────────────────────────────────────────────────────────────────────────────────────
async function cmdList() {
  const all = instances();
  if (all.length === 0) { console.log(`No AutoWebNav app running (registry: ${REGISTRY}).`); return; }
  for (const i of all) {
    const panes = Object.entries(i.panes || {}).map(([n, p]) => `${n}:${p}`).join('  ');
    console.log(`${i.app.padEnd(14)} PID ${String(i.pid).padEnd(7)} started ${new Date(i.startedUtc).toLocaleString()}   ${panes}`);
  }
}

function printSnapshot(pane, s, tail) {
  console.log(`[${pane}] ${s.url}`);
  if (s.title) console.log(`  title:     ${s.title}`);
  if (s.app && !s.app.error) {
    if (s.app.running !== undefined) console.log(`  running:   ${s.app.running}`);
    if (s.app.summary) console.log(`  summary:   ${s.app.summary}`);
    for (const [k, v] of Object.entries(s.app)) {
      if (['running', 'summary', 'log'].includes(k)) continue;
      console.log(`  ${(k + ':').padEnd(10)} ${typeof v === 'string' ? v : JSON.stringify(v)}`);
    }
    const log = Array.isArray(s.app.log) ? s.app.log : [];
    if (log.length) {
      console.log(`  log (last ${Math.min(tail, log.length)} of ${log.length}):`);
      for (const line of log.slice(-tail)) console.log(`    ${line}`);
    }
    return;
  }
  if (s.app?.error) console.log(`  __awnObserve threw: ${s.app.error}`);
  if (s.heading) console.log(`  heading:   ${s.heading}`);
  if (s.banners.length) { console.log('  banners:'); for (const b of s.banners) console.log(`    - ${b}`); }
  if (s.unchecked.length) { console.log('  unticked checkboxes:'); for (const c of s.unchecked) console.log(`    - ${c}`); }
  if (s.buttons.length) console.log(`  buttons:   ${s.buttons.join(' | ')}`);
}

async function cmdStatus() {
  const inst = resolve(selector);
  const tail = Number(flags.tail) || 15;
  console.log(`${inst.title || inst.app}  (registered ${new Date(inst.startedUtc).toLocaleString()})`);
  for (const pane of panesOf(inst, flags.pane)) {
    let cdp;
    try {
      cdp = await connect(inst, pane);
      const s = await cdp.evaluate(SNAPSHOT);
      if (flags.json) console.log(JSON.stringify({ pane, ...s }, null, 2)); else printSnapshot(pane, s, tail);
    } catch (e) {
      console.log(`[${pane}] unavailable on port ${inst.panes[pane]}: ${e.message}`);
    } finally { cdp?.close(); }
  }
}

async function cmdLog() {
  const inst = resolve(selector);
  const tail = Number(flags.tail) || 40;
  for (const pane of panesOf(inst, flags.pane)) {
    const cdp = await connect(inst, pane).catch(() => null);
    if (!cdp) continue;
    try {
      const s = await cdp.evaluate(SNAPSHOT);
      const log = s.app && Array.isArray(s.app.log) ? s.app.log : null;
      if (!log) continue;
      console.log(`[${pane}] last ${Math.min(tail, log.length)} of ${log.length}:`);
      for (const line of log.slice(-tail)) console.log(line);
    } finally { cdp.close(); }
  }
}

async function cmdShot() {
  const inst = resolve(selector);
  const [pane] = panesOf(inst, flags.pane || Object.keys(inst.panes).at(-1));
  const cdp = await connect(inst, pane);
  try {
    const { data } = await cdp.send('Page.captureScreenshot', { format: 'png' });
    const out = typeof flags.out === 'string' ? flags.out
      : path.join(os.tmpdir(), `awn-${inst.app}-${pane}-${Date.now()}.png`);
    fs.writeFileSync(out, Buffer.from(data, 'base64'));
    console.log(out);
  } finally { cdp.close(); }
}

async function cmdDom() {
  const inst = resolve(selector);
  if (!flags.pane) fail('dom needs --pane.');
  const [pane] = panesOf(inst, flags.pane);
  const sel = typeof flags.selector === 'string' ? flags.selector : 'body';
  const max = Number(flags.max) || 20000;
  const cdp = await connect(inst, pane);
  try {
    const html = await cdp.evaluate(`(() => { const el = document.querySelector(${JSON.stringify(sel)}); return el ? el.outerHTML : null; })()`);
    if (html === null) fail(`Nothing matches ${sel}.`);
    console.log(html.slice(0, max));
    if (html.length > max) console.log(`\n… (${html.length} chars total, truncated at ${max})`);
  } finally { cdp.close(); }
}

async function cmdEval() {
  const inst = resolve(selector);
  if (!flags.pane || typeof flags.expr !== 'string') fail('eval needs --pane and --expr.');
  const [pane] = panesOf(inst, flags.pane);
  const cdp = await connect(inst, pane);
  try {
    const v = await cdp.evaluate(flags.expr);
    console.log(typeof v === 'string' ? v : JSON.stringify(v, null, 2));
  } finally { cdp.close(); }
}

// One line per event, for a person or an agent following the app live:
//   UP / GONE        the app (re)appeared in, or left, the registry
//   NAV <pane>       the pane navigated
//   STATE <pane>     the app's own running flag changed
//   SUMMARY <pane>   the app's own one-line summary changed
//   LOG <pane>       a new line in the app's own log
//   BANNER <pane>    a banner/alert/dialog became visible (pages without __awnObserve)
// Keeps running across restarts and redeploys: it re-resolves the app from the registry each tick.
async function cmdWatch() {
  const interval = Number(flags.interval) || 1000;
  const appFilter = selector;
  const say = line => console.log(`${stamp()} ${line}`);
  let inst = null;
  const conns = new Map();   // pane -> Cdp
  const prev = new Map();    // pane -> { url, running, summary, logLen, banners:Set }

  const dropAll = () => { for (const c of conns.values()) c.close(); conns.clear(); prev.clear(); };

  for (;;) {
    const current = resolve(appFilter, { quiet: true });
    if (!current) {
      if (inst) { say(`GONE ${inst.app} PID ${inst.pid}`); dropAll(); inst = null; }
    } else {
      if (!inst || inst.pid !== current.pid) {
        if (inst) { say(`GONE ${inst.app} PID ${inst.pid}`); dropAll(); }
        inst = current;
        say(`UP ${inst.app} PID ${inst.pid} panes ${Object.entries(inst.panes).map(([n, p]) => `${n}:${p}`).join(' ')}`);
      }
      inst = current;   // pick up panes registered after startup
      for (const pane of Object.keys(inst.panes)) {
        try {
          let cdp = conns.get(pane);
          if (!cdp || cdp.closed) { cdp = await connect(inst, pane); conns.set(pane, cdp); }
          const s = await cdp.evaluate(SNAPSHOT);
          const p = prev.get(pane);
          const app = s.app && !s.app.error ? s.app : null;
          const log = app && Array.isArray(app.log) ? app.log : [];
          if (!p) {
            say(`NAV ${pane} ${s.url}`);
            if (app?.running !== undefined) say(`STATE ${pane} running=${app.running}`);
            if (app?.summary) say(`SUMMARY ${pane} ${app.summary}`);
          } else {
            if (s.url !== p.url) say(`NAV ${pane} ${s.url}`);
            if (app && app.running !== p.running) say(`STATE ${pane} running=${app.running}`);
            if (app && app.summary !== p.summary) say(`SUMMARY ${pane} ${app.summary}`);
            if (log.length < p.logLen) say(`LOG ${pane} (log cleared/reloaded)`);
            else for (const line of log.slice(p.logLen)) say(`LOG ${pane} ${line}`);
            if (!app) for (const b of s.banners) if (!p.banners.has(b)) say(`BANNER ${pane} ${b}`);
          }
          prev.set(pane, { url: s.url, running: app?.running, summary: app?.summary,
                           logLen: log.length, banners: new Set(s.banners) });
        } catch {
          conns.get(pane)?.close();
          conns.delete(pane);   // reconnect next tick (pane reloading, or the app is shutting down)
        }
      }
    }
    await new Promise(r => setTimeout(r, interval));
  }
}

const commands = { list: cmdList, status: cmdStatus, log: cmdLog, watch: cmdWatch, shot: cmdShot, dom: cmdDom, eval: cmdEval };
if (!commands[command]) {
  console.log(fs.readFileSync(new URL(import.meta.url), 'utf8').split('\n')
    .filter(l => l.startsWith('//')).map(l => l.slice(3)).join('\n'));
  process.exit(command === 'help' ? 0 : 1);
}
await commands[command]().catch(e => fail(e.message));
