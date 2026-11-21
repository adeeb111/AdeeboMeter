#!/usr/bin/env python3
# Lists what a language table still needs - for translators.
#   python3 tools/langcheck.py fr        -> lang-fr-todo.txt at the repo root (English, a TAB, empty) + a summary
#
# It collects the English the app shows through L.T(...): every L.T("literal") in src/, plus the Toolbox tool titles and
# group names (they're translated by name). Texts the app builds at run time (numbers, joined pieces) can't be listed here -
# testers find those on screen. Lines already in knowledge/lang-<code>.txt are left out. Many table lines are reached
# through a variable (menu items, settings choices, buddy answers), so a line this script doesn't see is NOT unused - keep it.
import os, re, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
code = sys.argv[1] if len(sys.argv) > 1 else "fr"
def unescape(s): return s.encode('latin-1', 'backslashreplace').decode('unicode_escape') if '\\' in s else s
want = set()
for d, _, fs in os.walk(os.path.join(root, 'src')):
    for f in fs:
        if not f.endswith('.cs'): continue
        if f == 'SelfTest.cs': continue  # sentinel strings exercise fallback; they are not UI
        t = open(os.path.join(d, f), encoding='utf-8').read()
        for m in re.finditer(r'L\.T\("((?:[^"\\]|\\.)*)"\)', t): want.add(m.group(1).replace('\\"', '"').replace('\\\\', '\\'))
        if f == 'FullView.Toolbox.cs':
            for m in re.finditer(r'T\("[a-z0-9]+", "([A-Z0-9 &/,\'-]+)", "((?:[^"\\]|\\.)*)"', t): want.add(m.group(1)); want.add(m.group(2))
want.discard('')
want = {s for s in want if any(c.isalpha() for c in s)}  # bullets and numeric-only strings have no language
path = os.path.join(root, 'knowledge', 'lang-%s.txt' % code)
have = {}
if os.path.exists(path):
    for line in open(path, encoding='utf-8'):
        line = line.rstrip('\r\n')
        if line and not line.startswith('#') and '\t' in line: k, v = line.split('\t', 1); have[k] = v
todo = sorted(w for w in want if w not in have)
unused = sorted(k for k in have if k not in want)
out = os.path.join(root, 'lang-%s-todo.txt' % code)   # not in knowledge/: the build packs every .txt there into the exe
with open(out, 'w', encoding='utf-8', newline='\n') as o:
    o.write('# Still to translate for lang-%s.txt: put the translation after the TAB, then move the line into lang-%s.txt.\n' % (code, code))
    for w in todo: o.write(w + '\t\n')
print('%s: %d texts in the app, %d translated, %d to do -> %s' % (code, len(want), len(want) - len(todo), len(todo), os.path.relpath(out, root)))
if unused: print('%d lines in lang-%s.txt are reached through variables (not as L.T("...") literals) - keep them' % (len(unused), code))
# the language picker shows how much is done: keep a "#coverage NN" line in the table (the app reads it; a # line is never a translation)
done = sum(1 for w in want if have.get(w, '').strip())
pct = int(100 * done / max(1, len(want)))
lines = open(path, encoding='utf-8').read().split('\n') if os.path.exists(path) else []
lines = [l for l in lines if not l.startswith('#coverage ')]
at = 0
while at < len(lines) and lines[at].startswith('#'): at += 1
lines.insert(at, '#coverage %d' % pct)
open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines))
print('coverage line: %d%%' % pct)
# the website's language tiles show the same number: <small data-cov="fr">78%</small> on every home page that has them
for page in ('index.html', 'fr/index.html', 'es/index.html', 'bn/index.html', 'ar/index.html'):
    p = os.path.join(root, 'docs', page)
    if not os.path.exists(p): continue
    t = open(p, encoding='utf-8').read()
    n = re.sub(r'(<small data-cov="%s">)\d+%%(</small>)' % re.escape(code), r'\g<1>%d%%\g<2>' % pct, t)
    if n != t: open(p, 'w', encoding='utf-8', newline='\n').write(n); print('website tile: %s now says %d%%' % (page, pct))
