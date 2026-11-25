#!/usr/bin/env python3
# Lists what a language table still needs - for translators.
#   python3 tools/langcheck.py fr            -> lang-fr-todo.txt at the repo root (English, a TAB, empty) + a summary; changes nothing else
#   python3 tools/langcheck.py fr --write    -> also writes the "#coverage NN" line into the table and the website's tiles
#
# It collects the English the app shows through L.T(...): every L.T("literal") in src/, plus the Toolbox tool titles and
# group names (they're translated by name). Texts the app builds at run time (numbers, joined pieces) and texts reached through a
# variable (menu items, settings choices, topic titles) can't all be listed here - testers find those on screen, so the number
# is "of the texts this script can see" (bug-hunt R2-103). Many table lines are reached through a variable, so a line this
# script doesn't see is NOT unused - keep it.
# Bug hunt round 2: an empty translation is a to-do (R2-179); a key given twice is judged by its FIRST value, as the app
# reads it (R2-180); only --write changes files, and a language that isn't in the app is refused (R2-181); tool titles and
# groups with "?" are seen too (R2-192).
import os, re, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
args = [a for a in sys.argv[1:] if not a.startswith('--')]
write = '--write' in sys.argv
code = args[0] if args else "fr"
known = set(re.findall(r'new\[\]\s*\{\s*"([a-z]{2})"', open(os.path.join(root, 'src', 'Lang.cs'), encoding='utf-8').read()))
if known and code not in known:
    sys.exit('langcheck: "%s" is not a language the app has (%s)' % (code, ', '.join(sorted(known))))
def unescape(s): return s.encode('latin-1', 'backslashreplace').decode('unicode_escape') if '\\' in s else s
want = set()
for d, _, fs in os.walk(os.path.join(root, 'src')):
    for f in fs:
        if not f.endswith('.cs'): continue
        if f == 'SelfTest.cs': continue  # sentinel strings exercise fallback; they are not UI
        t = open(os.path.join(d, f), encoding='utf-8').read()
        for m in re.finditer(r'L\.T\("((?:[^"\\]|\\.)*)"\)', t): want.add(m.group(1).replace('\\"', '"').replace('\\\\', '\\'))
        if f == 'FullView.Toolbox.cs':
            for m in re.finditer(r'T\("[a-z0-9]+", "([A-Z0-9 &/,\'?.-]+)", "((?:[^"\\]|\\.)*)"', t): want.add(m.group(1)); want.add(m.group(2))
want.discard('')
want = {s for s in want if any(c.isalpha() for c in s)}  # bullets and numeric-only strings have no language
path = os.path.join(root, 'knowledge', 'lang-%s.txt' % code)
have = {}
if os.path.exists(path):
    for line in open(path, encoding='utf-8'):
        line = line.rstrip('\r\n')
        if line and not line.startswith('#') and '\t' in line:
            k, v = line.split('\t', 1)
            if k not in have: have[k] = v   # the first one wins, as in the app (L.Parse)
done_set = {w for w in want if have.get(w, '').strip()}
todo = sorted(w for w in want if w not in done_set)   # missing or empty: both still to do
unused = sorted(k for k in have if k not in want)
out = os.path.join(root, 'lang-%s-todo.txt' % code)   # not in knowledge/: the build packs every .txt there into the exe
with open(out, 'w', encoding='utf-8', newline='\n') as o:
    o.write('# Still to translate for lang-%s.txt: put the translation after the TAB, then move the line into lang-%s.txt.\n' % (code, code))
    for w in todo: o.write(w + '\t\n')
print('%s: %d texts this script can see, %d translated, %d to do -> %s' % (code, len(want), len(done_set), len(todo), os.path.relpath(out, root)))
if unused: print('%d lines in lang-%s.txt are reached through variables (not as L.T("...") literals) - keep them' % (len(unused), code))
pct = int(100 * len(done_set) / max(1, len(want)))
print('coverage: %d%%' % pct + ('' if write else '  (add --write to put it in the table and on the website)'))
if not write: sys.exit(0)
# the language picker shows how much is done: keep a "#coverage NN" line in the table (the app reads it; a # line is never a translation)
lines = open(path, encoding='utf-8').read().split('\n') if os.path.exists(path) else []
lines = [l for l in lines if not l.startswith('#coverage ')]
at = 0
while at < len(lines) and lines[at].startswith('#'): at += 1
lines.insert(at, '#coverage %d' % pct)
open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(lines))
print('coverage line written')
# the website's language tiles show the same number: <small data-cov="fr">78%</small> on every home page that has them
for page in ('index.html', 'fr/index.html', 'es/index.html', 'bn/index.html', 'ar/index.html'):
    p = os.path.join(root, 'docs', page)
    if not os.path.exists(p): continue
    t = open(p, encoding='utf-8').read()
    n = re.sub(r'(<small data-cov="%s">)\d+%%(</small>)' % re.escape(code), r'\g<1>%d%%\g<2>' % pct, t)
    if n != t: open(p, 'w', encoding='utf-8', newline='\n').write(n); print('website tile: %s now says %d%%' % (page, pct))
