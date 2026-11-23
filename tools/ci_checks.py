#!/usr/bin/env python3
# Cheap source checks that run on every build (hunt-5): each guards a mistake that already shipped once.
#  1. code swallowed by a // comment on a one-line statement (football keys, the Bluetooth send links, a lost comma)
#  2. every script in the site's pages and the two workers parses (the phone page had no automated run at all)
#  3. the phone page and the PC agree: the same 256 matching words, and both sign the same pairing fields (R-1)
import glob, json, os, re, subprocess, sys, tempfile

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
os.chdir(root)
bad = []

# 1 ---------------------------------------------------------------------------------------------------------------
stmt = re.compile(r'(\b(?:public|private|internal|static|readonly|protected)\b[^;]*[=;]|\belse\b\s*\{|\bif\s*\(.*\)\s*\{?.*;|\w+\s*\(.*\)\s*;\s*\}|=\s*new\s+\w+.*;|\breturn\b[^.]*;\s*\}|^\s*\w[\w.]*\s*\(.*\)\s*;)')
def comment_of(line):
    i, ins, verb = 0, None, False
    while i < len(line):
        c = line[i]
        if ins:
            if c == '\\' and not verb: i += 2; continue
            if c == ins:
                if verb and line[i + 1:i + 2] == '"': i += 2; continue
                ins = None
        else:
            if line.startswith('//', i): return line[:i], line[i + 2:]
            if line.startswith('/*', i):
                j = line.find('*/', i + 2); i = len(line) if j < 0 else j + 2; continue
            if c in '"\'': ins, verb = c, i > 0 and line[i - 1] == '@'
        i += 1
    return line, None
for f in glob.glob('src/**/*.cs', recursive=True):
    for n, l in enumerate(open(f, encoding='utf-8', errors='replace'), 1):
        code, com = comment_of(l.rstrip('\n'))
        if com is None or not code.strip(): continue
        if stmt.search(com):   # round 6: no word in front of the code excuses it
            bad.append('%s:%d: code after // looks swallowed: %s' % (f, n, com.strip()[:120]))

# 2 ---------------------------------------------------------------------------------------------------------------
tmp = tempfile.mkdtemp()
checked = [0]
def node_check(src, label):
    checked[0] += 1
    p = os.path.join(tmp, 'x.js'); open(p, 'w', encoding='utf-8').write(src)
    r = subprocess.run(['node', '--check', p], capture_output=True, text=True)
    if r.returncode != 0: bad.append('%s: script does not parse: %s' % (label, (r.stderr.strip().splitlines() or ['?'])[-1][:200]))
for f in sorted(glob.glob('docs/*.html')):
    html = open(f, encoding='utf-8').read()
    for k, m in enumerate(re.finditer(r'<script(?![^>]*\bsrc=)(?![^>]*type="(?!text/javascript|module)[^"]*")[^>]*>([\s\S]*?)</script>', html)):
        if m.group(1).strip(): node_check(m.group(1), '%s script %d' % (f, k + 1))
for f in ['tools/relay/worker.js', 'tools/remote/worker.js'] + glob.glob('docs/*.js'):
    if os.path.exists(f):
        r = subprocess.run(['node', '--check', f], capture_output=True, text=True)
        if r.returncode != 0: bad.append('%s: does not parse: %s' % (f, r.stderr.strip()[-200:]))

# 3 ---------------------------------------------------------------------------------------------------------------
cs = open('src/Remote/RemoteWords.cs', encoding='utf-8').read()
pc_words = re.search(r'List = "([^"]+)"', cs).group(1).split(' ')
app = open('docs/app.html', encoding='utf-8').read()
m = re.search(r'WORDS\s*=\s*"([^"]+)"\.split\(" "\)', app) or re.search(r'WORDS\s*=\s*(\[[^\]]+\])', app)
phone_words = (m.group(1).split(' ') if m and not m.group(1).startswith('[') else json.loads(m.group(1).replace("'", '"'))) if m else None
if not phone_words or phone_words != pc_words: bad.append('docs/app.html: the matching-word list differs from src/Remote/RemoteWords.cs')
rem = open('src/Remote/Remote.cs', encoding='utf-8').read()
if 'm.wv !== 2' not in app: bad.append('docs/app.html: the phone no longer refuses fewer matching words than it asked for (R-1)')
for label in ['"hi2|', '"key2|']:
    if label not in rem or label.replace('"', '"') not in app: bad.append('pairing: %s is signed on one side only (Remote.cs vs app.html)' % label)

print('\n'.join(bad) if bad else 'ci checks: all clean (%d scripts parsed, %d matching words agree)' % (checked[0], len(pc_words)))
sys.exit(1 if bad else 0)
