# Builds the website's Learn pages from the app's own lessons (knowledge/learn.txt):
#   docs/learn/index.html    - every lesson, grouped, searchable
#   docs/learn/<id>.html     - one page per lesson (title/description for search engines, ladder picture, one-question quiz)
# and adds them to docs/sitemap.xml. Run: python3 tools/learnsite.py   (edit learn.txt, run again - app and site stay the same)
import html, json, os, re, datetime

root = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
SITE = 'https://adeebometer.com/'
KINDS = [('basics', 'The basics'), ('protocol', 'How the internet talks'), ('provider', 'Inside your provider'),
         ('mobile', 'Mobile, LTE and 5G'), ('windows', 'Your Windows PC'), ('apps', 'Apps on your PC'),
         ('crypto', 'Encryption and keys'), ('streaming', 'Streaming'), ('investing', 'Trading and investing')]
KN = dict(KINDS)
E = lambda s: html.escape(s or '', quote=True)

topics = []
for block in open(os.path.join(root, 'knowledge/learn.txt'), encoding='utf-8').read().split('\n== ')[1:]:
    lines = block.split('\n'); d = {'id': re.sub(r'\s+', '-', lines[0].strip().split('|')[0].strip())}
    for l in lines[1:]:
        m = re.match(r'([a-z]+): (.*)', l)
        if m and m.group(1) not in d: d[m.group(1)] = m.group(2).strip()
    if d.get('title') and re.fullmatch(r"[a-z0-9.-]+", d["id"]): topics.append(d)
order = {k: i for i, (k, _) in enumerate(KINDS)}
topics.sort(key=lambda t: order.get(t.get('kind'), 99))   # stable: keeps learn.txt order inside a group

CSS = """:root{--ink:#1B2330;--sub:#4A5568;--line:#E3E8EF;--page:#FBFCFE;--card:#FFFFFF;--blue:#1F6FEB;--green:#2DA44E;--red:#CF222E;--amber:#9A6700;--tint:#EEF4FF}
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--ink:#E6EAF0;--sub:#A9B3C1;--line:#2A3340;--page:#0F141B;--card:#161C25;--blue:#58A6FF;--green:#3FB950;--red:#F85149;--amber:#D29922;--tint:#16233A}}
:root[data-theme="dark"]{--ink:#E6EAF0;--sub:#A9B3C1;--line:#2A3340;--page:#0F141B;--card:#161C25;--blue:#58A6FF;--green:#3FB950;--red:#F85149;--amber:#D29922;--tint:#16233A}
*{box-sizing:border-box}
body{margin:0;background:var(--page);color:var(--ink);font:16px/1.6 "Segoe UI",system-ui,-apple-system,sans-serif;-webkit-text-size-adjust:100%}
main{max-width:760px;margin:0 auto;padding:20px 16px calc(56px + env(safe-area-inset-bottom))}
a{color:var(--blue);overflow-wrap:anywhere}
.top{display:flex;gap:14px;flex-wrap:wrap;align-items:center;font-size:15px;margin-bottom:18px}
.top b{margin-right:auto}
h1{font-size:28px;line-height:1.2;margin:0 0 6px;overflow-wrap:anywhere}
h2{font-size:18px;margin:28px 0 8px}
.tag{font-size:18px;color:var(--sub);margin:0 0 18px}
.chip{display:inline-block;font-size:13px;font-weight:600;color:var(--blue);background:var(--tint);border-radius:999px;padding:2px 10px;text-decoration:none}
.card{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 16px;margin:0 0 10px}
.card p{margin:0}
.one{border-left:4px solid var(--green)}
.warn{border-left:4px solid var(--amber)}
.ladder{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:12px 14px}
.lanes{display:flex;justify-content:space-between;font-weight:600;font-size:14px;border-bottom:1px solid var(--line);padding-bottom:6px;margin-bottom:6px;gap:12px}
.lanes span:last-child{text-align:right}
.ladder ol{list-style:none;margin:0;padding:0}.sr{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap}
.msg{padding:6px 0}
.msg p{margin:0 0 3px;font-size:15px;overflow-wrap:anywhere}
.msg.r p{text-align:right}
.arrow{height:10px;position:relative}
.arrow:before{content:"";position:absolute;left:0;right:8px;top:4px;border-top:2px solid var(--blue)}
.arrow:after{content:"";position:absolute;right:0;top:0;border:5px solid transparent;border-left:9px solid var(--blue);border-right:0}
.msg.r .arrow:before{left:8px;right:0}
.msg.r .arrow:after{right:auto;left:0;border-left:0;border-right:9px solid var(--blue)}
.msg.all .arrow:before{border-top-style:dashed}
.quiz button{display:block;width:100%;text-align:left;font:inherit;color:var(--ink);background:var(--card);border:1px solid var(--line);border-radius:10px;padding:11px 14px;margin:8px 0;cursor:pointer;min-height:44px}
.quiz button.ok{border-color:var(--green);box-shadow:inset 4px 0 0 var(--green)}
.quiz button.no{border-color:var(--red);box-shadow:inset 4px 0 0 var(--red)}
.quiz .say{font-weight:600;min-height:1.6em;margin:6px 0 0}
.pn{display:flex;justify-content:space-between;gap:12px;margin-top:28px;font-size:15px}
.pn a{max-width:48%}
.cta{display:inline-block;background:var(--blue);color:#fff;text-decoration:none;font-weight:600;border-radius:999px;padding:11px 20px;margin:6px 10px 0 0}
.ghost{background:transparent;color:var(--blue);border:1px solid var(--line)}
footer{margin-top:40px;color:var(--sub);font-size:14px}
input[type=search]{width:100%;font:inherit;color:var(--ink);background:var(--card);border:1px solid var(--line);border-radius:10px;padding:10px 12px;margin:4px 0 8px}
.grid{display:grid;grid-template-columns:repeat(2,1fr);gap:10px}
.grid a{display:block;text-decoration:none;color:var(--ink);background:var(--card);border:1px solid var(--line);border-radius:12px;padding:12px 14px}
.grid a b{display:block;color:var(--blue);font-size:15px;line-height:1.3}
.grid a span{display:block;color:var(--sub);font-size:14px;margin-top:3px}
.chips{display:flex;gap:6px;overflow-x:auto;padding:4px 0 8px;scrollbar-width:none}
.chips a{white-space:nowrap}
@media (max-width:560px){.grid{grid-template-columns:1fr}h1{font-size:25px}}
"""
PWA = '<!--pwa--><link rel="manifest" href="/manifest.webmanifest"><meta name="theme-color" content="#1F6FEB"><link rel="apple-touch-icon" href="/img/app/apple-touch-icon.png"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-title" content="AdeeboMeter"><script>if("serviceWorker"in navigator)addEventListener("load",function(){navigator.serviceWorker.register("/sw.js").catch(function(){})})</script><!--/pwa-->'
THEME = '<script>try{var t=localStorage.getItem("am-theme");if(t==="dark"||t==="light")document.documentElement.setAttribute("data-theme",t)}catch(e){}</script>'
FOOT = '<footer>AdeeboMeter for Windows · free · no account, no cloud, no telemetry · these lessons are the same ones built into the app</footer>'

def head(title, desc, url, extra=''):
    return ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
            f'<title>{E(title)}</title>\n<meta name="description" content="{E(desc)}">\n<link rel="canonical" href="{url}">\n'
            f'<meta property="og:title" content="{E(title)}">\n<meta property="og:description" content="{E(desc)}">\n<meta property="og:type" content="article">\n'
            f'<link rel="icon" href="../favicon.svg" type="image/svg+xml">\n{PWA}\n<style>\n{CSS}</style>\n{THEME}\n{extra}</head>\n<body>\n<main>\n')

def top(): return '<nav class="top"><b><a href="../">AdeeboMeter</a></b><a href="./">Learn</a><a href="../games.html">Games</a><a href="../live.html">Live</a></nav>\n'

def ladder(s):
    steps = []
    for part in s.split(' | '):
        m = re.match(r'(.+?) > (.+?): (.*)', part.strip())
        if m: steps.append((m.group(1).strip(), m.group(2).strip(), m.group(3).strip()))
        elif steps: steps.append((steps[-1][0], steps[-1][1], part.strip()))   # a bare note keeps the last direction
    if not steps: return ''
    left = steps[0][0]
    right = next((b for a, b, _ in steps if b != left and b != 'Everyone'), None) or next((a for a, b, _ in steps if a != left), steps[0][1])
    # a real ordered list (AM-CG-325): screen readers hear every message with who sends it to whom; the arrows are decoration
    out = [f'<div class="ladder"><div class="lanes" aria-hidden="true"><span>{E(left)}</span><span>{E(right)}</span></div><ol aria-label="{E("Messages between " + left + " and " + right + ", in order")}">']
    for a, b, msg in steps:
        cls = 'msg r' if a != left else 'msg'
        if b == 'Everyone': cls += ' all'
        shown = a in (left, right) and b in (left, right, 'Everyone')
        who = f'<span class="sr">{E(a)} to {E(b)}: </span>' if shown else f'{E(a)} → {E(b)}: '
        out.append(f'<li class="{cls}"><p>{who}{E(msg)}</p><div class="arrow" aria-hidden="true"></div></li>')
    out.append('</ol></div>')
    return '\n'.join(out)

QUIZ_JS = """<script>
(function(){var q=document.querySelector('.quiz');if(!q)return;var bs=[].slice.call(q.querySelectorAll('button')),say=q.querySelector('.say'),done=false;
for(var i=bs.length-1;i>0;i--){var j=Math.floor(Math.random()*(i+1)),t=bs[i];bs[i]=bs[j];bs[j]=t}
bs.forEach(function(b){q.insertBefore(b,say)});
bs.forEach(function(b){b.onclick=function(){if(done)return;var ok=b.hasAttribute('data-ok');b.className=ok?'ok':'no';
if(ok){done=true;say.textContent='Right.';try{var k='am-learn-done',s=JSON.parse(localStorage.getItem(k)||'[]');if(s.indexOf(q.dataset.id)<0){s.push(q.dataset.id);localStorage.setItem(k,JSON.stringify(s))}}catch(e){}}
else say.textContent='Not that one - try again.'}})})();
</script>
"""

def lesson(t, prev, nxt, same):
    url = f'{SITE}learn/{t["id"]}.html'
    name = t['title'].split(' - ')[0]
    desc = (t.get('tag', '') + ' ' + t.get('line', '')).strip()
    if len(desc) < 120 and t.get('breaks'): desc += ' ' + t['breaks']
    desc = desc[:158].rsplit(' ', 1)[0] + ('…' if len(desc) > 158 else '')
    ld = {'@context': 'https://schema.org', '@type': 'LearningResource', 'name': t['title'], 'description': desc, 'url': url,
          'learningResourceType': 'Lesson', 'educationalLevel': 'Beginner', 'inLanguage': 'en', 'isAccessibleForFree': True,
          'about': KN.get(t.get('kind'), ''), 'isPartOf': {'@type': 'WebSite', 'name': 'AdeeboMeter', 'url': SITE}}
    h = [head(f'{t["title"]} | Learn | AdeeboMeter', desc, url, f'<script type="application/ld+json">{json.dumps(ld, ensure_ascii=False)}</script>\n'), top()]
    h.append(f'<a class="chip" href="./#{E(t.get("kind", ""))}">{E(KN.get(t.get("kind"), "Learn"))}</a>\n<h1>{E(t["title"])}</h1>\n<p class="tag">{E(t.get("tag"))}</p>\n')
    h.append(f'<div class="card one"><p>{E(t.get("line"))}</p></div>\n')
    if t.get('ladder'): h.append(f'<h2>How the conversation goes</h2>\n{ladder(t["ladder"])}\n')
    if t.get('breaks'): h.append(f'<h2>When it goes wrong</h2>\n<div class="card warn"><p>{E(t["breaks"])}</p></div>\n')
    if t.get('more'): h.append(f'<h2>Going deeper</h2>\n<p>{E(t["more"])}</p>\n')
    q = [x.strip() for x in t.get('check', '').split(' | ')]
    if len(q) >= 3:
        h.append(f'<h2>Quick check</h2>\n<div class="quiz" data-id="{E(t["id"])}"><p>{E(q[0])}</p>\n')
        h.append(f'<button data-ok>{E(q[1])}</button>\n' + ''.join(f'<button>{E(o)}</button>\n' for o in q[2:]))
        h.append('<p class="say" aria-live="polite"></p></div>\n')
    h.append('<h2>See it on your own PC</h2>\n<p>')
    if t.get('live'):
        h.append(f'In the AdeeboMeter app, Learn › {E(name)} › <b>See it live</b> records these messages on your own network')
        h.append(f' while you {E(t["try"][0].lower() + t["try"][1:].rstrip("."))}.' if t.get('try') else '.')
    else:
        h.append(f'This lesson is built into the AdeeboMeter app, next to the checks that test {E(name)} on your own connection.')
    h.append('</p>\n<a class="cta" href="https://apps.microsoft.com/detail/9PFGXJPL9CLJ">Get AdeeboMeter for Windows</a><a class="cta ghost" href="../games.html">Play the network games</a>\n')
    if same:
        h.append('<h2>Also in ' + E(KN.get(t.get('kind'), 'this group')) + '</h2>\n<div class="grid">' +
                 ''.join(f'<a href="{s["id"]}.html"><b>{E(s["title"].split(" - ")[0])}</b><span>{E(s.get("tag"))}</span></a>' for s in same) + '</div>\n')
    h.append('<div class="pn">' + (f'<a href="{prev["id"]}.html">← {E(prev["title"].split(" - ")[0])}</a>' if prev else '<span></span>') +
             (f'<a href="{nxt["id"]}.html">{E(nxt["title"].split(" - ")[0])} →</a>' if nxt else '') + '</div>\n')
    h.append(FOOT + '\n</main>\n' + (QUIZ_JS if len(q) >= 3 else '') + '</body>\n</html>\n')
    return ''.join(h)

def index():
    desc = f'{len(topics)} short, free lessons on how networks, Wi-Fi, 5G, encryption, Windows, streaming and trading connections really work - each with a picture and a quick check.'
    ld = {'@context': 'https://schema.org', '@type': 'ItemList', 'name': 'AdeeboMeter Learn', 'itemListElement':
          [{'@type': 'ListItem', 'position': i + 1, 'url': f'{SITE}learn/{t["id"]}.html', 'name': t['title']} for i, t in enumerate(topics)]}
    h = [head('Learn how your internet works | AdeeboMeter', desc, SITE + 'learn/', f'<script type="application/ld+json">{json.dumps(ld, ensure_ascii=False)}</script>\n'), top()]
    h.append(f'<h1>Learn how your internet works</h1>\n<p class="tag">{len(topics)} lessons, two lines and a picture each. Free, no sign-up - the same lessons that sit inside the app.</p>\n')
    h.append('<p><a class="cta" href="../games.html">Test yourself: network games</a></p>\n')
    h.append('<input type="search" id="q" placeholder="Search lessons (DNS, Wi-Fi, 5G, VPN…)" aria-label="Search lessons">\n<p id="done" style="color:var(--sub);font-size:14px;margin:0"></p>\n')
    h.append('<div class="chips">' + ''.join(f'<a class="chip" href="#{k}">{E(n)}</a>' for k, n in KINDS if any(t.get('kind') == k for t in topics)) + '</div>\n')
    for k, n in KINDS:
        g = [t for t in topics if t.get('kind') == k]
        if not g: continue
        h.append(f'<section data-k="{k}"><h2 id="{k}">{E(n)}</h2>\n<div class="grid">')
        h.append(''.join(f'<a href="{t["id"]}.html" data-s="{E((t["title"] + " " + t.get("tag", "") + " " + t.get("line", "")).lower())}"><b>{E(t["title"])}</b><span>{E(t.get("tag"))}</span></a>' for t in g))
        h.append('</div></section>\n')
    h.append(FOOT + '\n</main>\n<script>\n(function(){var q=document.getElementById("q");q.oninput=function(){var v=q.value.toLowerCase().trim();'
             '[].forEach.call(document.querySelectorAll("section"),function(s){var n=0;[].forEach.call(s.querySelectorAll("a"),function(a){var m=!v||a.getAttribute("data-s").indexOf(v)>=0;a.style.display=m?"":"none";if(m)n++});s.style.display=n?"":"none"})};'
             f'try{{var d=JSON.parse(localStorage.getItem("am-learn-done")||"[]");if(d.length)document.getElementById("done").textContent="You have answered "+d.length+" of {len(topics)} quick checks on this device.";}}catch(e){{}}}})();\n</script>\n</body>\n</html>\n')
    return ''.join(h)

out = os.path.join(root, 'docs/learn'); os.makedirs(out, exist_ok=True)
for i, t in enumerate(topics):
    same = [s for s in topics if s.get('kind') == t.get('kind') and s is not t][:4]
    with open(os.path.join(out, t['id'] + '.html'), 'w', encoding='utf-8', newline='\n') as f:
        f.write(lesson(t, topics[i - 1] if i else None, topics[i + 1] if i + 1 < len(topics) else None, same))
with open(os.path.join(out, 'index.html'), 'w', encoding='utf-8', newline='\n') as f: f.write(index())

# sitemap: keep the hand-written pages, replace the learn ones
sm = os.path.join(root, 'docs/sitemap.xml'); today = datetime.date.today().isoformat()
keep = [l for l in open(sm, encoding='utf-8').read().splitlines() if '<url>' in l and '/learn/' not in l and 'games.html' not in l and 'live.html' not in l and 'app.html' not in l and 'files.html' not in l]
new = [f'  <url><loc>{SITE}{p}</loc><lastmod>{today}</lastmod></url>' for p in ['app.html', 'files.html', 'learn/', 'games.html', 'live.html'] + [f'learn/{t["id"]}.html' for t in topics]]
with open(sm, 'w', encoding='utf-8', newline='\n') as f:
    f.write('<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n' + '\n'.join(keep + new) + '\n</urlset>\n')
print(len(topics), 'lesson pages + index; sitemap', len(keep) + len(new), 'urls')

# ---- the companion's voices for the website and the phone: the app's own line files (knowledge/lines-*.txt) as small
# JSON packs, one per voice, plus the list. Only the words change with a voice - numbers and facts stay as they are.
ls = open(os.path.join(root, 'src/Lines.cs'), encoding='utf-8').read()
moods = re.findall(r'"([a-z-]+)"', re.search(r'Moods = \{([^}]*)\}', ls).group(1)); mnames = re.findall(r'"([^"]+)"', re.search(r'MoodNames = \{([^}]*)\}', ls).group(1))
pers = re.findall(r'new\[\] \{ "([a-z-]+)", "([^"]+)" \}', re.search(r'Personas = \{(.*?)\};', ls, re.S).group(1))
vdir = os.path.join(root, 'docs/kb/voice'); os.makedirs(vdir, exist_ok=True); have = []
for vid in [m for m in moods] + [p[0] for p in pers]:
    f = os.path.join(root, 'knowledge', 'lines-' + vid + '.txt')
    if not os.path.exists(f): continue
    pack = {}
    for l in open(f, encoding='utf-8'):
        l = l.strip()
        if not l or l.startswith('#') or '|' not in l: continue
        k, t = l.split('|', 1); k, t = k.strip(), t.strip()
        if k and t: pack.setdefault(k, []).append(t)
    with open(os.path.join(vdir, vid + '.json'), 'w', encoding='utf-8', newline='\n') as o: json.dump(pack, o, ensure_ascii=False, separators=(',', ':'))
    have.append(vid)
idx = {'moods': [[m, n] for m, n in zip(moods, mnames) if m in have], 'personas': [[p, n] for p, n in pers if p in have]}
with open(os.path.join(vdir, 'index.json'), 'w', encoding='utf-8', newline='\n') as o: json.dump(idx, o, ensure_ascii=False, separators=(',', ':'))
print('voices', len(have))

# ---- the phone app (PWA): the same PWA tags on the hand-written pages, and the offline cache (docs/sw.js)
import hashlib
for page in ['index.html', 'games.html', 'live.html', 'remote.html', 'r.html', 'files.html']:
    f = os.path.join(root, 'docs', page); h = open(f, encoding='utf-8').read()
    h = re.sub(r'<!--pwa-->.*?<!--/pwa-->\n?', '', h, flags=re.S)
    h = h.replace('</head>', PWA + '\n</head>', 1)
    with open(f, 'w', encoding='utf-8', newline='\n') as o: o.write(h)
core = ['app.html', 'files.html', 'index.html', 'games.html', 'live.html', 'remote.html', 'r.html', 'learn/index.html', 'kb/learn1.json', 'kb/r1.json',
        'favicon.svg', 'manifest.webmanifest', 'img/app/icon-192.png', 'img/app/apple-touch-icon.png']
extra = [f'learn/{t["id"]}.html' for t in topics] + ['voice.js', 'kb/voice/index.json'] + [f'kb/voice/{v}.json' for v in have]
ver = hashlib.sha256(b''.join(open(os.path.join(root, 'docs', x), 'rb').read() for x in core + extra)).hexdigest()[:12]
sw = """// AdeeboMeter phone app - offline copy of the site's own pages (generated by tools/learnsite.py; don't edit by hand).
// Keeps only our public pages and lessons. Never touches other sites (Live data, the remote relay) and holds nothing about the person.
const V = "am-%s", CORE = %s, EXTRA = %s;
self.addEventListener("install", e => e.waitUntil(caches.open(V).then(c => c.addAll(CORE).then(() =>
  Promise.all(EXTRA.map(u => c.add(u).catch(() => {}))))).then(() => self.skipWaiting())));
self.addEventListener("activate", e => e.waitUntil(caches.keys().then(k => Promise.all(k.filter(x => x.startsWith("am-") && x !== V).map(x => caches.delete(x)))).then(() => self.clients.claim())));
function key(u) { let p = u.pathname; if (p.endsWith("/")) p += "index.html"; else if (!/\\.[a-z0-9]+$/i.test(p)) p += ".html"; return p; }
self.addEventListener("fetch", e => {
  const r = e.request, u = new URL(r.url);
  if (r.method !== "GET" || u.origin !== location.origin) return;           // other sites go straight to the network
  const k = key(u);
  e.respondWith(caches.open(V).then(c => c.match(k).then(hit => {
    const net = fetch(r).then(res => { if (res.ok && hit) c.put(k, res.clone()); return res; });   // refresh what we keep, quietly
    if (hit) { e.waitUntil(net.catch(() => {})); return hit; }
    return net.catch(() => r.mode === "navigate" ? c.match("/app.html") : Response.error());
  })));
});
""" % (ver, json.dumps(['/' + x for x in core]), json.dumps(['/' + x for x in extra]))
with open(os.path.join(root, 'docs/sw.js'), 'w', encoding='utf-8', newline='\n') as f: f.write(sw)
print('sw.js', ver, len(core) + len(extra), 'files')

