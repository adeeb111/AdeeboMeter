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
# the flat look (docs/ui.css, docs/ui.js): an icon and a short name for each group
KIC = {'basics': 'layers', 'protocol': 'globe', 'provider': 'router', 'mobile': 'signal', 'windows': 'monitor', 'apps': 'apps', 'crypto': 'lock', 'streaming': 'stream', 'investing': 'chart'}
KSHORT = {'basics': 'Basics', 'protocol': 'Protocols', 'provider': 'Provider', 'mobile': 'Mobile', 'windows': 'Windows', 'apps': 'Apps', 'crypto': 'Encryption', 'streaming': 'Streaming', 'investing': 'Trading'}
def lrow(t): return (f'<a class="ui-row" href="{t["id"]}.html" data-id="{t["id"]}"><span class="ui-chip sm" style="--k:var(--c-learn)" data-ic="{KIC.get(t.get("kind"), "book")}"></span>'
                     f'<span class="t"><b>{E(t["title"].split(" - ")[0])}</b><small>{E(KSHORT.get(t.get("kind"), ""))}</small></span><span data-ic="chev"></span></a>')
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
@media (prefers-color-scheme: dark){:root:not([data-theme="light"]){--ink:#E6EAF0;--sub:#93A6C6;--line:#22355A;--page:#0B1426;--card:#121F38;--blue:#58A6FF;--green:#3FB950;--red:#F85149;--amber:#D29922;--tint:#182844}}
:root[data-theme="dark"]{--ink:#E6EAF0;--sub:#93A6C6;--line:#22355A;--page:#0B1426;--card:#121F38;--blue:#58A6FF;--green:#3FB950;--red:#F85149;--amber:#D29922;--tint:#182844}
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
PWA = '<!--pwa--><link rel="manifest" href="/manifest.webmanifest"><meta name="theme-color" content="#1F6FEB"><link rel="apple-touch-icon" href="/img/app/apple-touch-icon.png"><meta name="apple-mobile-web-app-capable" content="yes"><meta name="apple-mobile-web-app-title" content="AdeeboMeter"><style>.inapp nav.top{display:none}a.brandl{display:inline-flex;align-items:center;gap:8px}a.brandl img{display:block}</style><script>if("serviceWorker"in navigator)addEventListener("load",function(){navigator.serviceWorker.register("/sw.js").catch(function(){})})</script><!--/pwa-->'
THEME = '<script>try{var t=localStorage.getItem("am-theme");if(t==="dark"||t==="light")document.documentElement.setAttribute("data-theme",t)}catch(e){}</script>'
FOOT = '<footer>Free · no account · the same lessons as the Windows app</footer>'

def head(title, desc, url, extra='', og_type='article'):
    return ('<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">\n'
            f'<title>{E(title)}</title>\n<meta name="description" content="{E(desc)}">\n<link rel="canonical" href="{url}">\n'
            f'<meta property="og:title" content="{E(title)}">\n<meta property="og:description" content="{E(desc)}">\n<meta property="og:type" content="{og_type}">\n'
            f'<link rel="icon" href="../favicon.svg" type="image/svg+xml">\n{PWA}\n<style>\n{CSS}</style>\n{THEME}\n<link rel="stylesheet" href="/ui.css">\n{extra}<script src="/locale-messages.js"></script><script src="/locale.js"></script><script src="/locale-surfaces.js"></script></head>\n<body data-sec="learn">\n<main>\n')

def top(): return ''
def top_old(): return '<nav class="top"><b><a href="../" class="brandl"><img src="/img/logo.svg" alt="" width="22" height="22">AdeeboMeter</a></b><a href="./">Learn</a><a href="../games.html">Games</a><a href="../live.html">Live</a></nav>\n'

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
var V=window.AMV,locale=window.AdeeboMeterLocale;
function feedback(key,line){var text=locale?locale.t(key):key;return text!==key?text:(V&&V.line(line))||key}
if(ok){done=true;say.textContent=feedback('Right.','quiz.right');try{var k='am-learn-done',s=JSON.parse(localStorage.getItem(k)||'[]');if(s.indexOf(q.dataset.id)<0){s.push(q.dataset.id);localStorage.setItem(k,JSON.stringify(s))}}catch(e){}}
else say.textContent=feedback('Not that one - try again.','quiz.wrong')}})})();
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
    if t['id'] == 'wifi':
        h.append('<p>If you are unsure where the slowdown starts, <a href="../wifi-or-isp.html">check whether the problem is your Wi-Fi or your internet provider</a>.</p>\n')
        h.append('<p>If the connection keeps dropping, compare Wi-Fi with Ethernet and <a href="../isp-report.html">save a connection report for your internet provider</a>.</p>\n')
    if t['id'] == 'icmp':
        h.append('<p>To capture ping and traceroute results together, <a href="../isp-report.html">create a packet loss report for your internet provider</a>.</p>\n')
    if t['id'] == 'speed-vs-delay':
        h.append('<p>To see how both change while you work, <a href="../internet-speed-monitor.html">monitor internet speed on Windows</a>.</p>\n')
    if t['id'] == 'memory-pressure':
        h.append('<p>Compare memory use with CPU and disk activity using <a href="../pc-monitoring.html">AdeeboMeter’s Windows PC monitor</a>.</p>\n')
    if t.get('more'): h.append(f'<h2>Going deeper</h2>\n<p>{E(t["more"])}</p>\n')
    q = [x.strip() for x in t.get('check', '').split(' | ')]
    if len(q) >= 3:
        h.append(f'<h2>Quick check</h2>\n<div class="quiz" data-id="{E(t["id"])}"><p>{E(q[0])}</p>\n')
        h.append(f'<button data-ok>{E(q[1])}</button>\n' + ''.join(f'<button>{E(o)}</button>\n' for o in q[2:]))
        h.append('<p class="say" aria-live="polite"></p></div>\n')
    h.append('<p class="ui-ctas"><a class="cta" href="https://apps.microsoft.com/detail/9PFGXJPL9CLJ">Get AdeeboMeter for Windows</a><a class="cta ghost" href="../games.html">Play the games</a></p>\n')
    if same:
        h.append('<h2>More in ' + E(KSHORT.get(t.get('kind'), 'this group')) + '</h2>\n<div class="ui-rows">' + ''.join(lrow(x) for x in same) + '</div>\n')
    h.append('<div class="pn">' + (f'<a href="{prev["id"]}.html">← {E(prev["title"].split(" - ")[0])}</a>' if prev else '<span></span>') +
             (f'<a href="{nxt["id"]}.html">{E(nxt["title"].split(" - ")[0])} →</a>' if nxt else '') + '</div>\n')
    seen = '<script>try{var k="am-learn-seen",s=JSON.parse(localStorage.getItem(k)||"[]");if(s.indexOf("%s")<0)s.push("%s");localStorage.setItem(k,JSON.stringify(s));localStorage.setItem("am-learn-last","%s")}catch(e){}</script>\n' % (t['id'], t['id'], t['id'])
    h.append(FOOT + '\n</main>\n' + ('<script src="/voice.js"></script>\n' + QUIZ_JS if len(q) >= 3 else '') + seen + '<script src="/ui.js"></script>\n</body>\n</html>\n')
    return ''.join(h)

def index():
    desc = f'{len(topics)} short, free lessons on how networks, Wi-Fi, 5G, encryption, Windows, streaming and trading connections really work - each with a picture and a quick check.'
    ld = {'@context': 'https://schema.org', '@type': 'ItemList', 'name': 'AdeeboMeter Learn', 'itemListElement':
          [{'@type': 'ListItem', 'position': i + 1, 'url': f'{SITE}learn/{t["id"]}.html', 'name': t['title']} for i, t in enumerate(topics)]}
    preview = (
        f'<meta property="og:url" content="{SITE}learn/">\n'
        f'<meta property="og:image" content="{SITE}img/social/learn.jpg">\n'
        '<meta property="og:image:width" content="1200">\n'
        '<meta property="og:image:height" content="630">\n'
        '<meta property="og:image:alt" content="Learn how your internet works">\n'
        '<meta name="twitter:card" content="summary_large_image">\n'
    )
    h = [head('Learn how your internet works | AdeeboMeter', desc, SITE + 'learn/', preview + f'<script type="application/ld+json">{json.dumps(ld, ensure_ascii=False)}</script>\n', og_type="website"), top()]
    h.append('<header class="ui-head"><span class="ui-chip" style="--k:var(--c-learn)" data-ic="book" data-sz="28"></span><div><h1>Learn</h1><p>Short lessons, a picture each.</p></div></header>\n')
    h.append(f'<a class="ui-hero" id="cont" href="{topics[0]["id"]}.html"><span class="tag" id="ctag">Start here</span><b id="ctit">{E(topics[0]["title"].split(" - ")[0])}</b><small id="cmeta">Lesson 1 of {len([x for x in topics if x.get("kind") == topics[0].get("kind")])} · {E(KSHORT.get(topics[0].get("kind"), ""))}</small><span class="go" data-ic="chev"></span><span class="ui-prog"><i><b id="cbar" style="width:0%"></b></i><span id="cpct">0%</span></span></a>\n')
    h.append('<label class="ui-search" data-ic="search"><input type="search" id="q" placeholder="Search lessons" aria-label="Search lessons"></label>\n')
    h.append('<div class="ui-pills" role="group" aria-label="Topics"><button class="on" data-f="">All</button>' + ''.join(f'<button data-f="{k}">{E(KSHORT.get(k, n))}</button>' for k, n in KINDS if any(t.get('kind') == k for t in topics)) + '</div>\n')
    for k, n in KINDS:
        g = [t for t in topics if t.get('kind') == k]
        if not g: continue
        h.append(f'<section data-k="{k}"><h2 class="ui-h2" id="{k}">{E(n)}</h2>\n<div class="ui-rows">' + ''.join(lrow(t).replace('<a class="ui-row"', f'<a class="ui-row" data-s="{E((t["title"] + " " + t.get("tag", "") + " " + t.get("line", "")).lower())}"', 1) for t in g) + '</div></section>\n')
    L = json.dumps([[t['id'], t['title'].split(' - ')[0], t.get('kind', ''), KSHORT.get(t.get('kind'), '')] for t in topics], ensure_ascii=False, separators=(',', ':'))
    h.append(FOOT + '\n</main>\n<script>\n(function(){var L=' + L + ',f="",q=document.getElementById("q");'
             'function show(){var v=q.value.toLowerCase().trim();[].forEach.call(document.querySelectorAll("main section"),function(s){var n=0;if(!f||s.getAttribute("data-k")===f)[].forEach.call(s.querySelectorAll("a"),function(a){var m=!v||a.getAttribute("data-s").indexOf(v)>=0;a.style.display=m?"":"none";if(m)n++});s.style.display=n?"":"none"})}'
             'q.oninput=show;[].forEach.call(document.querySelectorAll("[data-f]"),function(b){b.onclick=function(){f=b.getAttribute("data-f");[].forEach.call(document.querySelectorAll("[data-f]"),function(x){x.className=x===b?"on":""});show()}});'
             'try{var seen=JSON.parse(localStorage.getItem("am-learn-seen")||"[]"),last=localStorage.getItem("am-learn-last");'
             '[].forEach.call(document.querySelectorAll(".ui-row[data-id]"),function(a){if(seen.indexOf(a.getAttribute("data-id"))>=0){var s=a.querySelector("small");s.innerHTML=s.innerHTML+" · <span class=ok>✓ read</span>"}});'
             'var t=null;L.forEach(function(x){if(x[0]===last)t=x});if(t){var g=L.filter(function(x){return x[2]===t[2]}),i=0,r=0;g.forEach(function(x,k){if(x[0]===t[0])i=k;if(seen.indexOf(x[0])>=0)r++});'
             'var nx=g.filter(function(x){return seen.indexOf(x[0])<0})[0]||t;var j=g.indexOf(nx),p=Math.round(100*r/g.length);document.getElementById("cont").href=nx[0]+".html";document.getElementById("ctag").textContent=nx===t?"Read again":"Continue learning";'
             'document.getElementById("ctit").textContent=nx[1];document.getElementById("cmeta").textContent="Lesson "+(j+1)+" of "+g.length+" · "+nx[3];document.getElementById("cbar").style.width=p+"%";document.getElementById("cpct").textContent=p+"%"}}catch(e){}})();\n</script>\n<script src="/ui.js"></script>\n</body>\n</html>\n')
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
# Keep Bengali static copies and language annotations in sync with generated lessons.
import runpy
runpy.run_path(os.path.join(root, 'tools/bengali-seo.py'))
runpy.run_path(os.path.join(root, 'tools/french-seo.py'))
runpy.run_path(os.path.join(root, 'tools/spanish-seo.py'))

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
for page in ['index.html', 'games.html', 'live.html', 'app.html', 'r.html', 'files.html']:
    f = os.path.join(root, 'docs', page); h = open(f, encoding='utf-8').read()
    h = re.sub(r'<!--pwa-->.*?<!--/pwa-->\n?', '', h, flags=re.S)
    h = h.replace('</head>', PWA + '\n</head>', 1)
    with open(f, 'w', encoding='utf-8', newline='\n') as o: o.write(h)
core = ['locale-surfaces.js', 'locale.js', 'locale-messages.js', 'locale.css', 'img/canada.svg', 'fr/index.html', 'app.html', 'ui.css', 'ui.js', 'calc.js', 'journey.js', 'qr.js', 'img/logo.svg', 'files.html', 'index.html', 'games.html', 'live.html', 'remote.html', 'r.html', 'learn/index.html', 'kb/learn1.json', 'kb/r1.json',
        'favicon.svg', 'manifest.webmanifest', 'img/app/icon-192.png', 'img/app/apple-touch-icon.png']
extra = ['bn/index.html', 'bn/blog/index.html', 'bn/wifi-or-isp.html', 'bn/internet-speed-monitor.html', 'bn/pc-monitoring.html', 'bn/isp-report.html', 'bn/video-calls-freeze.html', 'guide.css', 'feature.css', 'lab.html'] + ['bn/learn/index.html'] + [f'bn/learn/{t["id"]}.html' for t in topics] + [f'learn/{t["id"]}.html' for t in topics] + ['voice.js', 'kb/voice/index.json'] + [f'kb/voice/{v}.json' for v in have]
# Restore the public-page analytics loader after lesson generation.
runpy.run_path(os.path.join(root, 'tools/web-analytics.py'), run_name='__main__')
core.append('analytics.js')
extra += sorted('fr/' + os.path.relpath(os.path.join(base, file), os.path.join(root, 'docs/fr')).replace(os.sep, '/')
                for base, _, names in os.walk(os.path.join(root, 'docs/fr'))
                for file in names if file.endswith('.html'))
extra += sorted('es/' + os.path.relpath(os.path.join(base, file), os.path.join(root, 'docs/es')).replace(os.sep, '/')
                for base, _, names in os.walk(os.path.join(root, 'docs/es'))
                for file in names if file.endswith('.html'))
extra += sorted('ar/' + os.path.relpath(os.path.join(base, file), os.path.join(root, 'docs/ar')).replace(os.sep, '/')
                for base, _, names in os.walk(os.path.join(root, 'docs/ar'))
                for file in names if file.endswith('.html'))   # R2-084: Arabic is kept offline too
extra = list(dict.fromkeys(x for x in extra if x not in core))
ver = hashlib.sha256(b''.join(open(os.path.join(root, 'docs', x), 'rb').read() for x in core + extra)).hexdigest()[:12]
sw = """// AdeeboMeter phone app - offline copy of the site's own pages (generated by tools/learnsite.py; don't edit by hand).
// Keeps only our public pages and lessons. Never touches other sites (Live data, the remote relay) and holds nothing about the person.
const V = "am-%s", CORE = %s, EXTRA = %s;
self.addEventListener("install", e => e.waitUntil(caches.open(V).then(c => c.addAll(CORE).then(() =>
  Promise.all(EXTRA.map(u => c.add(u).catch(() => {}))))).then(() => self.skipWaiting())));
self.addEventListener("activate", e => e.waitUntil(caches.keys().then(k => Promise.all(k.filter(x => x.startsWith("am-") && x !== V).map(x => caches.delete(x)))).then(() => self.clients.claim())));
// SEC-05 (security hunt): a fresh copy replaces the kept one only if it really is that page - not redirected, from this
// site, and the same kind of file (a captive portal's own 200 page must never take a kept page's place)
function same(res, h) { const t = x => (x.headers.get("Content-Type") || "").split(";")[0].trim().toLowerCase(); return !res.redirected && res.type === "basic" && new URL(res.url).origin === location.origin && t(res) === t(h); }
function key(u) { let p = u.pathname; if (p.endsWith("/")) p += "index.html"; else if (!/\\.[a-z0-9]+$/i.test(p)) p += ".html"; return p; }
const OFF = () => new Response('<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Offline</title><body style="font:17px system-ui;background:#0B1426;color:#EAF1FF;display:grid;place-items:center;min-height:90vh;text-align:center;padding:20px"><div><img src="/img/logo.svg" width="72" height="72" alt=""><h1>You’re offline</h1><p>This page isn’t kept on this phone. <a style="color:#7FB0FF" href="/app.html">Open the app</a> or <a style="color:#7FB0FF" href="/learn/">Learn</a> - they work offline.</p></div>', { status: 503, headers: { "Content-Type": "text/html; charset=utf-8" } });
self.addEventListener("fetch", e => {
  const r = e.request, u = new URL(r.url);
  if (r.method !== "GET" || u.origin !== location.origin) return;           // other sites go straight to the network
  const k = key(u);
  // pages: the network first (a fresh page after an update, AM-CG-335), the kept copy when offline or slow (4 s);
  // a page that isn't kept says it's offline instead of showing a different page (AM-CG-334)
  if (r.mode === "navigate") {
    e.respondWith(caches.open(V).then(c => new Promise(done => {
      const t = setTimeout(() => { c.match(k).then(h => h && done(h)); }, 4000);
      fetch(r).then(res => { clearTimeout(t); if (res.ok) { const copy = res.clone(); e.waitUntil(c.match(k).then(h => { if (h && same(res, h)) return c.put(k, copy); }).catch(() => {})); } done(res); })   // the page goes to the screen first, always; keeping a copy is a side job
        .catch(() => { clearTimeout(t); c.match(k).then(h => done(h || OFF())); });
    })));
    return;
  }
  e.respondWith(caches.open(V).then(c => c.match(k).then(hit => {
    const net = fetch(r).then(res => { if (res.ok && hit && same(res, hit)) c.put(k, res.clone()); return res; });   // refresh what we keep, quietly
    if (hit) { e.waitUntil(net.catch(() => {})); return hit; }
    return net.catch(() => Response.error());
  })));
});
""" % (ver, json.dumps(['/' + x for x in core]), json.dumps(['/' + x for x in extra]))
with open(os.path.join(root, 'docs/sw.js'), 'w', encoding='utf-8', newline='\n') as f: f.write(sw)
print('sw.js', ver, len(core) + len(extra), 'files')

