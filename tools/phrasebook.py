#!/usr/bin/env python3
# Builds docs/kb/r1.json - the phone page's phrasebook - from the app's own text, so the phone and the PC always agree:
#   knowledge/terms.txt   (== tb.<id>: what / how / why)   and   knowledge/examples.txt (advice per result label)
#   src/Ui/FullView.Toolbox.cs (tool id -> title, the name the phone link carries)
# Run it after changing those files. The page fetches the JSON (no cookies, no data sent) and falls back to the link alone.
import json, re, os, sys
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = open(os.path.join(root, 'src/Ui/FullView.Toolbox.cs'), encoding='utf-8').read()
titles = dict(re.findall(r'T\("([a-z0-9]+)", "[^"]*", "([^"]+)"', src))
terms = {}
for block in open(os.path.join(root, 'knowledge/terms.txt'), encoding='utf-8').read().split('\n== ')[1:]:
    lines = block.split('\n'); keys = lines[0].strip().split('|'); d = {}
    for l in lines[1:]:
        m = re.match(r'(\w+): (.*)', l)
        if m: d[m.group(1)] = m.group(2).strip()
    for k in keys: terms[k] = d
advice = {}
cur = None
for l in open(os.path.join(root, 'knowledge/examples.txt'), encoding='utf-8'):
    l = l.rstrip('\n')
    if l.startswith('== '): cur = l[3:].strip(); continue
    p = l.split('|')
    if cur and len(p) >= 4 and re.match(r'^-?\d$', p[0]) and p[3].strip():
        label = re.sub(r'\s*[·(].*$', '', p[1]).strip()   # "Hop 4 · 198.51.100.17" -> "Hop 4"; keep the stable part
        if label and not re.match(r'^Hop \d', label): advice.setdefault(cur, {}).setdefault(label + '|' + p[0], p[3].strip())   # advice only for the same level
tools = {}
for tid, title in titles.items():
    t = terms.get('tb.' + tid, {}); e = {}
    for k in ('tag', 'what', 'how', 'why'):
        if t.get(k) and not (k == 'how' and t.get(k) == t.get('what')): e[k] = t[k][:400]
    if advice.get(tid): e['labels'] = {k: v[:300] for k, v in list(advice[tid].items())[:40]}
    if e: e['id'] = tid; tools[title] = e
out = {'v': 1, 'tools': tools}
os.makedirs(os.path.join(root, 'docs/kb'), exist_ok=True)
with open(os.path.join(root, 'docs/kb/r1.json'), 'w', encoding='utf-8') as f: json.dump(out, f, ensure_ascii=False, separators=(',', ':'))
print(len(tools), 'tools,', sum(len(v.get('labels', {})) for v in tools.values()), 'labels,', os.path.getsize(os.path.join(root, 'docs/kb/r1.json')), 'bytes')

# the Learn topics for the phone remote's Learn tab (docs/kb/learn1.json): the same short lessons as the app
topics = []
for block in open(os.path.join(root, 'knowledge/learn.txt'), encoding='utf-8').read().split('\n== ')[1:]:
    lines = block.split('\n'); d = {'id': lines[0].strip().split('|')[0]}
    for l in lines[1:]:
        m = re.match(r'(title|kind|tag|line|breaks|ladder|check|try): (.*)', l)
        if m and m.group(1) not in d: d[m.group(1)] = m.group(2).strip()[:600]
    if d.get('title'): topics.append(d)
with open(os.path.join(root, 'docs/kb/learn1.json'), 'w', encoding='utf-8') as f: json.dump({'v': 1, 'topics': topics}, f, ensure_ascii=False, separators=(',', ':'))
print(len(topics), 'learn topics,', os.path.getsize(os.path.join(root, 'docs/kb/learn1.json')), 'bytes')
