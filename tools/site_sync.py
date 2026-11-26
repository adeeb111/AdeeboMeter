#!/usr/bin/env python3
# One site bar on every page (24 Nov, Adeeb: "synchronize everything").
#   python3 tools/site_sync.py          -> rewrites docs/ in place, prints what it changed
#   python3 tools/site_sync.py --check  -> changes nothing; exit 1 if a page isn't in step (for tools/ci_checks.py)
# Every page except the home pages' own header gets the same bar right after <body> (docs/site.js fills it in the page's
# language, docs/sitebar.css draws it) and loses the header it had: the blog's old bar, the feature pages' "Made for" bar, the
# guides' breadcrumb, Learn / Games / Live / Files' "nav.top", privacy's back link, the Hall of Fame's header and the
# "English" links (the language picker is in the bar now). The home pages keep their header and get site.js for the download
# window only. Runs last in tools/site.sh: the language scripts and learnsite.py rewrite pages before it.
import os, re, sys
root = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'docs')
SKIP = {'app.html', 'r.html', 'remote.html', 'lab.html'}   # 24 Nov: the missing-page page has the bar too, so you can always get somewhere
BAR = '<!--sitebar--><nav class="sitebar" id="sitebar" aria-label="Site"><a class="sb-brand" href="{home}"><img src="/img/logo.svg" width="24" height="24" alt=""><span>Adeebo<b>Meter</b></span></a></nav><script src="/site.js"></script><!--/sitebar-->'
CSS = '<link rel="stylesheet" href="/sitebar.css">'
# the theme rule the home page uses, on every page, before anything is drawn: a kept choice (am-theme) wins, otherwise dark.
# Inside the PC app's window (a frame) the app's own choice applies, so this stays out of the way there.
THEME = '<!--theme--><script>try{if(window.top===window.self){var t=localStorage.getItem("am-theme");if(!t||t==="light"||t==="dark")document.documentElement.setAttribute("data-theme",t||"dark")}}catch(e){}</script><!--/theme-->'
OLD = [
    (r'\n?<!--sitebar-->.*?<!--/sitebar-->', 'the bar from the last run'),
    (r'<nav class="top sitebar"[^>]*>.*?</nav>\s*', "the blog's old bar"),
    (r'<header class="ftop">.*?</header>\s*', 'the "Made for" bar'),
    (r'<nav aria-label="[^"]*"><a href="/(?:[a-z]{2}/)?">AdeeboMeter</a> · .*?</nav>\s*', "the guide's breadcrumb"),
    (r'<nav class="top">.*?</nav>\s*', 'nav.top'),
    (r'<nav class="language-links"[^>]*>.*?</nav>\s*', 'the "English" link'),
    (r'<p><a href="[^"]*">← AdeeboMeter</a></p>\s*', 'the back link'),
    (r'<header class="top"><div class="wrap">.*?</div></header>\s*', "the Hall of Fame's header"),
    (r'<p class="story-kicker">[^<]*</p>', 'the kicker over the heading'),
    (r'<span class="ftag">[^<]*</span>\s*', 'the kicker over the heading'),
]
# the guides' and the blog's little card icons: drawn, one stroke and weight, not emoji (each system drew those differently)
def ic(d): return '<span class="scenario-emoji" aria-hidden="true"><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' + d + '</svg></span>'
ICONS = {
    '📶': ic('<path d="M2.5 9a14 14 0 0 1 19 0M5.5 12.5a9.5 9.5 0 0 1 13 0M8.6 16a5 5 0 0 1 6.8 0"/><circle cx="12" cy="19.3" r=".9" fill="currentColor"/>'),
    '⏱️': ic('<circle cx="12" cy="13.5" r="7.5"/><path d="M12 13.5V9.5M9.5 2.5h5M19 6l-1.4 1.4"/>'),
    '💻': ic('<rect x="4" y="5" width="16" height="11" rx="1.5"/><path d="M2 19h20"/>'),
    '📋': ic('<rect x="5" y="4.5" width="14" height="16.5" rx="2"/><path d="M9 4.5V3.5h6v1M8.5 10h7M8.5 13.5h7M8.5 17h4"/>'),
    '📹': ic('<rect x="3" y="7" width="12.5" height="10" rx="2"/><path d="M15.5 10.5 21 7.5v9l-5.5-3"/>'),
}
def pages():
    for d, dirs, fs in os.walk(root):
        dirs[:] = [x for x in dirs if x not in ('media', 'img', 'fonts', 'kb')]
        for f in fs:
            if f.endswith('.html'): yield os.path.join(d, f)
def sync(p):
    rel = os.path.relpath(p, root).replace(os.sep, '/'); name = os.path.basename(p)
    if rel in SKIP or re.sub(r"^(?:fr|es|ar|bn)/", "", rel) in {"app.html", "lab.html"}: return None, []
    s = open(p, encoding='utf-8').read(); t = s; did = []
    m = re.match(r'(fr|es|ar|bn)/', rel); home = '/' + m.group(1) + '/' if m else '/'
    is_home = rel in ('index.html', 'fr/index.html', 'es/index.html', 'ar/index.html', 'bn/index.html')
    if CSS not in t: t = t.replace('</head>', CSS + '\n</head>', 1); did.append('sitebar.css')
    if not is_home:
        t2 = re.sub(r'<!--theme-->.*?<!--/theme-->\n?', '', t, flags=re.S)
        h = re.search(r'<head[^>]*>', t2)
        if h: t2 = t2[:h.end()] + '\n' + THEME + t2[h.end():]
        if t2 != t and THEME not in t: did.append('theme rule')
        t = t2
    if is_home:
        if '<script src="/site.js" defer></script>' not in t: t = t.replace('</head>', '<script src="/site.js" defer></script>\n</head>', 1); did.append('download window')
        return (t if t != s else None), did
    for e, svg in ICONS.items():
        old = '<span class="scenario-emoji" aria-hidden="true">' + e + '</span>'
        if old in t: t = t.replace(old, svg); did.append('icons')
    for rx, what in OLD:
        t2 = re.sub(rx, '', t, count=1 if 'kicker' not in what else 0, flags=re.S)
        if t2 != t and what != 'the bar from the last run': did.append('- ' + what)
        t = t2
    b = re.search(r'<body[^>]*>', t)
    if not b: return None, ['no <body>']
    t = t[:b.end()] + '\n' + BAR.format(home=home) + t[b.end():]
    return (t if t != s else None), did
check = '--check' in sys.argv; bad = 0; n = 0
for p in sorted(pages()):
    t, did = sync(p)
    if t is None: continue
    n += 1
    if check: bad += 1; print('not in step:', os.path.relpath(p, root)); continue
    open(p, 'w', encoding='utf-8').write(t)
    if did: print(os.path.relpath(p, root) + ': ' + ', '.join(did))
print(('%d page(s) out of step' % bad) if check else ('%d page(s) written' % n))
sys.exit(1 if check and bad else 0)
