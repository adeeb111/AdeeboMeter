import os, re, sys, html
from urllib.parse import urlsplit, unquote
root = sys.argv[1]
ids = {}
pages = []
for b,_,ns in os.walk(root):
    for n in ns:
        if n.endswith('.html'):
            p = os.path.join(b,n); pages.append(p)
def idsof(p):
    if p not in ids:
        try: t = open(p, encoding='utf-8', errors='replace').read()
        except Exception: ids[p] = set(); return ids[p]
        ids[p] = set(re.findall(r'\b(?:id|name)\s*=\s*["\']([^"\']+)["\']', t))
    return ids[p]
bad = {}
for p in pages:
    t = open(p, encoding='utf-8', errors='replace').read()
    t2 = re.sub(r'<script\b[^>]*>.*?</script>', '', t, flags=re.S)  # attributes only in markup
    for m in re.finditer(r'\b(href|src|poster)\s*=\s*["\']([^"\']*)["\']', t2):
        u = html.unescape(m.group(2)).strip()
        if not u or re.match(r'^(https?:|mailto:|data:|javascript:|tel:|blob:|\{|\$)', u, re.I): continue
        s = urlsplit(u)
        if s.path == '':
            tgt = p
        elif s.path.startswith('/'):
            tgt = os.path.join(root, unquote(s.path).lstrip('/'))
        else:
            tgt = os.path.normpath(os.path.join(os.path.dirname(p), unquote(s.path)))
        if os.path.isdir(tgt) or tgt.endswith('/'): tgt = os.path.join(tgt, 'index.html')
        if not os.path.exists(tgt):
            bad.setdefault('missing '+os.path.relpath(tgt, root), []).append(os.path.relpath(p, root)); continue
        if s.fragment and tgt.endswith('.html') and s.fragment not in idsof(tgt):
            bad.setdefault('anchor '+os.path.relpath(tgt, root)+'#'+s.fragment, []).append(os.path.relpath(p, root))
for k,v in sorted(bad.items()):
    print(k, '<-', len(v), 'pages, e.g.', ', '.join(sorted(set(v))[:4]))
print('pages', len(pages), 'problems', len(bad))
