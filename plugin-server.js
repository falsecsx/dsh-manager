#!/usr/bin/env node
// ============================================================
//  dsh-plugin-server —— DeepSeek Harness 插件中心代理服务器
//  ============================================================
//  功能：
//    GET /                浏览器版插件中心（给别人直接用，无需 VPN）
//    GET /api/plugins     插件列表 JSON（GitHub topic:dsh-plugin，30 分钟缓存）
//    GET /api/plugins/zip 代理下载指定仓库的 ZIP（?repo=owner/name）
//    GET /api/health      存活检查
//
//  部署（任意有 node 且能访问 GitHub 的机器，如境外 VPS）：
//    node plugin-server.js --port 8080
//    国内服务器可配合代理：HTTPS_PROXY=http://127.0.0.1:7897 node plugin-server.js
//    可选 pm2 / systemd 常驻；放行对应端口即可。
//
//  零依赖：仅使用 node 内置模块（http/https/fs/tls）。
// ============================================================
'use strict';

const http = require('http');
const https = require('https');
const tls = require('tls');
const fs = require('fs');
const path = require('path');
const url = require('url');

// ---------------- 配置 ----------------
let PORT = 8080;
let CACHE_FILE = path.join(__dirname, 'plugins-cache.json');
const CACHE_TTL_MS = 30 * 60 * 1000;          // 缓存 30 分钟
const UA = 'dsh-plugin-server/1.0';
const MAX_PAGES = 3;                           // 拉取最近更新的前 300 个

const args = process.argv.slice(2);
for (let i = 0; i < args.length; i++) {
  if (args[i] === '--port') PORT = parseInt(args[i + 1], 10) || PORT;
  if (args[i] === '--cache') CACHE_FILE = args[i + 1];
}

// 服务器机器自身访问外网用的代理（国内服务器 + Clash 场景）
const PROXY = process.env.HTTPS_PROXY || process.env.https_proxy ||
              process.env.HTTP_PROXY || process.env.http_proxy || null;

// ---------------- 分类规则（与 DSH Manager 客户端一致） ----------------
const CATEGORY_RULES = [
  { name: '界面主题',   keywords: ['theme', 'skin', 'ui', 'style', '外观', '主题', '皮肤'] },
  { name: '身份认证',   keywords: ['auth', 'authentication', 'login', 'security', '认证', '登录'] },
  { name: '计费支付',   keywords: ['billing', 'payment', 'pay', '计费', '支付'] },
  { name: '视觉多模态', keywords: ['vision', 'multimodal', 'image', 'ocr', '视觉', '多模态', '图像'] },
  { name: '智能体协作', keywords: ['agent', 'preset', 'relay', 'collaboration', 'multi-agent', '智能体', '协作', '预设'] },
  { name: '网关与集成', keywords: ['gateway', 'api', 'integration', 'bridge', '网关', '集成', '桥接'] },
  { name: '工具命令',   keywords: ['tool', 'command', 'util', 'helper', '工具', '命令'] },
  { name: '合集资源',   keywords: ['awesome', 'list', 'directory', '合集', '清单', '导航'] },
];
function categorize(topics, desc) {
  const hay = (topics || []).join(' ').toLowerCase() + ' ' + String(desc || '').toLowerCase();
  for (const rule of CATEGORY_RULES) {
    for (const kw of rule.keywords) {
      if (hay.indexOf(kw) !== -1) return rule.name;
    }
  }
  return '其他';
}

// ---------------- HTTPS GET（支持 HTTP 代理 CONNECT 隧道） ----------------
function httpsGet(targetUrl, headers, timeoutMs) {
  return new Promise((resolve, reject) => {
    const u = new URL(targetUrl);
    let done = false;
    const finish = (err, res) => { if (!done) { done = true; err ? reject(err) : resolve(res); } };
    const timer = setTimeout(() => finish(new Error('timeout')), timeoutMs || 30000);

    if (PROXY) {
      const p = new URL(PROXY.indexOf('://') === -1 ? 'http://' + PROXY : PROXY);
      const connectReq = http.request({
        host: p.hostname, port: p.port || 80, method: 'CONNECT',
        path: u.host + ':443', headers: { Host: u.host }
      });
      connectReq.on('connect', (res, socket) => {
        if (res.statusCode !== 200) { clearTimeout(timer); finish(new Error('proxy CONNECT ' + res.statusCode)); return; }
        const tlsSocket = tls.connect({ socket, servername: u.hostname }, () => {
          const req2 = https.request({
            createConnection: () => tlsSocket, hostname: u.hostname,
            path: u.pathname + u.search, method: 'GET',
            // 隧道内必须显式带 Host，否则 GitHub 前端返回 400 Bad request
            headers: Object.assign({ Host: u.hostname }, headers)
          });
          req2.on('response', res2 => { clearTimeout(timer); finish(null, res2); });
          req2.on('error', e => { clearTimeout(timer); finish(e); });
          req2.end();
        });
        tlsSocket.on('error', e => { clearTimeout(timer); finish(e); });
      });
      connectReq.on('error', e => { clearTimeout(timer); finish(e); });
      connectReq.end();
    } else {
      const req = https.request(targetUrl, { method: 'GET', headers });
      req.on('response', res2 => { clearTimeout(timer); finish(null, res2); });
      req.on('error', e => { clearTimeout(timer); finish(e); });
      req.end();
    }
  });
}

function readBody(res, limit) {
  return new Promise((resolve, reject) => {
    let data = '';
    res.on('data', c => {
      data += c;
      if (limit && data.length > limit) { res.destroy(); reject(new Error('body too large')); }
    });
    res.on('end', () => resolve(data));
    res.on('error', reject);
  });
}

// ---------------- 插件数据（GitHub → 分类 → 缓存） ----------------
let memCache = null;   // { fetchedAt, total, plugins }
let inflight = null;

async function fetchFromGithub() {
  const items = [];
  let total = 0;
  for (let page = 1; page <= MAX_PAGES; page++) {
    const q = 'https://api.github.com/search/repositories?q=topic:dsh-plugin&sort=updated&per_page=100&page=' + page;
    const res = await httpsGet(q, { 'User-Agent': UA, 'Accept': 'application/vnd.github+json' }, 30000);
    const body = await readBody(res, 8 * 1024 * 1024);
    if (res.statusCode !== 200) {
      const err = new Error('GitHub API HTTP ' + res.statusCode);
      err.status = res.statusCode;
      throw err;
    }
    const j = JSON.parse(body);
    total = j.total_count || 0;
    (j.items || []).forEach(it => items.push(it));
  }
  const plugins = items.map(it => ({
    fullName: it.full_name, name: it.name, description: it.description || '',
    htmlUrl: it.html_url, stars: it.stargazers_count || 0,
    updatedAt: it.updated_at || '', topics: it.topics || [],
    category: categorize(it.topics, it.description)
  }));
  const result = { fetchedAt: new Date().toISOString(), total, plugins };
  memCache = result;
  try { fs.writeFileSync(CACHE_FILE, JSON.stringify(result)); } catch (e) { /* 缓存写入失败不致命 */ }
  return result;
}

function loadCacheFile() {
  try {
    if (!fs.existsSync(CACHE_FILE)) return null;
    const j = JSON.parse(fs.readFileSync(CACHE_FILE, 'utf8'));
    if (j && Array.isArray(j.plugins)) return j;
  } catch (e) { }
  return null;
}

function freshCache(c) {
  return c && Date.now() - Date.parse(c.fetchedAt) < CACHE_TTL_MS;
}

function getPlugins(force) {
  if (!force && freshCache(memCache)) return Promise.resolve({ data: memCache, stale: false });
  if (!force && !memCache) {
    // 重启后优先使用磁盘缓存（未过期则不再抓 GitHub）
    const disk = loadCacheFile();
    if (freshCache(disk)) {
      memCache = disk;
      return Promise.resolve({ data: disk, stale: false });
    }
  }
  if (!force && inflight) return inflight;
  const p = fetchFromGithub()
    .then(data => ({ data: data, stale: false }))
    .catch(err => {
      // GitHub 不可达时回退到缓存（即使过期），标记 stale 让客户端知道是缓存数据
      const c = memCache || loadCacheFile();
      if (c) return { data: c, stale: true };
      throw err;
    })
    .then(r => { inflight = null; return r; }, err => { inflight = null; throw err; });
  if (!force) inflight = p;
  return p;
}

// ---------------- ZIP 代理下载 ----------------
async function getDefaultBranch(repo) {
  const q = 'https://api.github.com/repos/' + repo;
  const res = await httpsGet(q, { 'User-Agent': UA }, 20000);
  if (res.statusCode !== 200) throw new Error('repo HTTP ' + res.statusCode);
  const j = JSON.parse(await readBody(res, 1024 * 1024));
  return j.default_branch || 'main';
}

function streamZip(repo, branch, res) {
  const u = 'https://codeload.github.com/' + repo + '/zip/refs/heads/' + branch;
  httpsGet(u, { 'User-Agent': UA }, 90000)
    .then(r => {
      const name = repo.split('/')[1] + '-' + branch + '.zip';
      const h = {
        'Content-Type': r.headers['content-type'] || 'application/zip',
        'Content-Disposition': 'attachment; filename="' + name + '"'
      };
      // codeload 分块传输时无 Content-Length，此时不能写空值头（.NET 客户端会拒绝）
      if (r.headers['content-length']) h['Content-Length'] = r.headers['content-length'];
      res.writeHead(r.statusCode, h);
      r.pipe(res);
      r.on('end', () => res.end());
      r.on('error', () => { try { res.end(); } catch (e) { } });
    })
    .catch(() => {
      try { res.writeHead(502, { 'Content-Type': 'text/plain; charset=utf-8' }); res.end('download failed'); } catch (e) { }
    });
}

// ---------------- 浏览器版插件中心（给别人用，无需 VPN） ----------------
function htmlPage() {
  return `<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>DeepSeek Harness 插件中心</title>
<style>
  * { box-sizing: border-box; }
  body { margin: 0; font-family: "Segoe UI", "Microsoft YaHei", sans-serif; background: #f5f6f8; color: #1b1b1c; }
  header { background: #4176e6; color: #fff; padding: 18px 28px; }
  header h1 { margin: 0; font-size: 20px; }
  header p { margin: 4px 0 0; opacity: .9; font-size: 13px; }
  .toolbar { padding: 12px 28px; display: flex; gap: 10px; align-items: center; flex-wrap: wrap; }
  .toolbar input, .toolbar select { padding: 6px 10px; border: 1px solid #cfd3d6; border-radius: 6px; font-size: 13px; }
  .toolbar input { width: 240px; }
  .toolbar button { padding: 6px 14px; border: 0; border-radius: 6px; background: #4176e6; color: #fff; font-size: 13px; cursor: pointer; }
  .toolbar button:hover { background: #386ce0; }
  #status { font-size: 12px; color: #61666b; }
  .wrap { padding: 0 28px 28px; }
  table { width: 100%; border-collapse: collapse; background: #fff; border-radius: 8px; overflow: hidden; box-shadow: 0 1px 4px rgba(0,0,0,.06); font-size: 13px; }
  th, td { text-align: left; padding: 9px 12px; border-bottom: 1px solid #eef0f2; }
  th { background: #fafbfc; color: #61666b; font-weight: 600; white-space: nowrap; }
  tr:hover td { background: #edf3fe; }
  td a { color: #4176e6; text-decoration: none; }
  td a:hover { text-decoration: underline; }
  .tag { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 12px; background: #edf3fe; color: #4176e6; white-space: nowrap; }
  .star { color: #f5a623; white-space: nowrap; }
  .dl { white-space: nowrap; }
  .desc { color: #61666b; max-width: 380px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  footer { padding: 10px 28px 24px; font-size: 12px; color: #61666b; }
</style>
</head>
<body>
<header>
  <h1>DeepSeek Harness 插件中心</h1>
  <p>数据来源 GitHub topic:dsh-plugin · 本页面由插件服务器提供，无需 VPN</p>
</header>
<div class="toolbar">
  <input id="q" placeholder="搜索插件名称 / 简介..." oninput="render()">
  <select id="cat" onchange="render()"><option value="">全部分类</option></select>
  <button onclick="refresh()">刷新</button>
  <span id="status">加载中...</span>
</div>
<div class="wrap"><table>
  <thead><tr><th style="width:220px">名称</th><th>分类</th><th>简介</th><th style="width:70px">⭐</th><th style="width:110px">最近更新</th><th style="width:90px">操作</th></tr></thead>
  <tbody id="rows"></tbody>
</table></div>
<footer id="foot"></footer>
<script>
let DATA = null;
async function load(force) {
  const s = document.getElementById('status');
  s.textContent = '加载中...';
  try {
    const r = await fetch('/api/plugins' + (force ? '?force=1' : ''), { cache: 'no-store' });
    if (!r.ok) throw new Error('HTTP ' + r.status);
    DATA = await r.json();
    s.textContent = '上次更新: ' + fmt(DATA.fetchedAt) + ' · 共 ' + DATA.total + ' 个（显示最近 ' + DATA.plugins.length + ' 个活跃）' + (DATA.source === 'cache' ? ' · 缓存数据' : '');
    document.getElementById('foot').textContent = '提示：分类为关键词自动归类；「下载 ZIP」由服务器代理下载，无需 VPN。';
    const cats = {};
    DATA.plugins.forEach(p => cats[p.category] = (cats[p.category] || 0) + 1);
    const sel = document.getElementById('cat');
    const cur = sel.value;
    sel.innerHTML = '<option value="">全部分类</option>' + Object.keys(cats).sort((a,b) => cats[b]-cats[a]).map(c => '<option>' + c + '</option>').join('');
    if (cur) sel.value = cur;
    render();
  } catch (e) {
    s.textContent = '获取失败: ' + e.message;
  }
}
function fmt(iso) { return iso ? iso.replace('T', ' ').slice(0, 19) : '--'; }
function render() {
  const q = (document.getElementById('q').value || '').toLowerCase();
  const c = document.getElementById('cat').value;
  const rows = document.getElementById('rows');
  rows.innerHTML = '';
  (DATA ? DATA.plugins : []).forEach(p => {
    if (c && p.category !== c) return;
    if (q && !(p.name + ' ' + p.description).toLowerCase().includes(q)) return;
    const tr = document.createElement('tr');
    tr.innerHTML = '<td><a href="' + p.htmlUrl + '" target="_blank">' + esc(p.name) + '</a></td>' +
      '<td><span class="tag">' + esc(p.category) + '</span></td>' +
      '<td class="desc" title="' + esc(p.description || '') + '">' + esc(p.description || '') + '</td>' +
      '<td class="star">' + p.stars + '</td>' +
      '<td>' + (p.updatedAt || '').slice(0, 10) + '</td>' +
      '<td class="dl"><a href="/api/plugins/zip?repo=' + encodeURIComponent(p.fullName) + '">下载 ZIP</a></td>';
    rows.appendChild(tr);
  });
}
function esc(s) { return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }
function refresh() { load(true); }
load(false);
</script>
</body>
</html>`;
}

// ---------------- 美化插件过滤（与 DSH Manager 客户端 IsBeautifyPlugin 规则一致） ----------------
const BEAUTIFY_RULES = [
  { name: '二次元动漫', re: /miku|初音|anime|touhou|灵梦|东方|原神|genshin|waifu|二次元|vocaloid|崩铁|崩坏|星穹|碧蓝|galgame|立绘/i },
  { name: '赛博朋克',   re: /cyber|赛博|霓虹|night ?city/i },
  { name: '怀旧复古',   re: /qq20|怀旧|复古|retro|win95|vista|2005|2006|2007|2008/i },
  { name: '玻璃质感',   re: /glass|frosted|玻璃|磨砂|毛玻璃|transparent/i },
  { name: '换肤系统',   re: /skin|theme|皮肤|主题|换肤|美化|cosmetic|配色|色系/i },
];
function beautifyStyle(p) {
  const hay = String(p.name || '') + ' ' + String(p.description || '') + ' ' + (p.topics || []).join(' ');
  for (const r of BEAUTIFY_RULES) if (r.re.test(hay)) return r.name;
  return null;
}

// ---------------- HTTP 服务 ----------------
const server = http.createServer((req, res) => {
  const u = url.parse(req.url, true);
  try {
    if (u.pathname === '/api/health') {
      res.writeHead(200, { 'Content-Type': 'application/json' });
      res.end('{"ok":true}');
      return;
    }
    if (u.pathname === '/api/plugins' || u.pathname === '/api/beautify') {
      const force = u.query.force === '1';
      getPlugins(force)
        .then(r => {
          let data = r.data;
          if (u.pathname === '/api/beautify') {
            const beauties = (r.data.plugins || [])
              .map(p => Object.assign({}, p, { category: beautifyStyle(p) || '其他' }))
              .filter(p => p.category !== '其他');
            data = { fetchedAt: r.data.fetchedAt, total: beauties.length, plugins: beauties };
          }
          res.writeHead(200, { 'Content-Type': 'application/json; charset=utf-8' });
          res.end(JSON.stringify(Object.assign({}, data, { source: r.stale ? 'cache' : 'live' })));
        })
        .catch(err => {
          res.writeHead(502, { 'Content-Type': 'application/json; charset=utf-8' });
          res.end(JSON.stringify({ error: String(err.message || err) }));
        });
      return;
    }
    if (u.pathname === '/api/plugins/zip') {
      const repo = String(u.query.repo || '').trim();
      if (!/^[\w.-]+\/[\w.-]+$/.test(repo)) { res.writeHead(400); res.end('bad repo'); return; }
      getDefaultBranch(repo)
        .then(branch => streamZip(repo, branch, res))
        .catch(() => { res.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' }); res.end('repo not found'); });
      return;
    }
    if (u.pathname === '/') {
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      res.end(htmlPage());
      return;
    }
    res.writeHead(404); res.end('not found');
  } catch (e) {
    try { res.writeHead(500); res.end('server error'); } catch (e2) { }
  }
});

server.listen(PORT, () => {
  console.log('[dsh-plugin-server] listening on http://0.0.0.0:' + PORT);
  console.log('[dsh-plugin-server] proxy=' + (PROXY || 'none') + '  cache=' + CACHE_FILE);
});
