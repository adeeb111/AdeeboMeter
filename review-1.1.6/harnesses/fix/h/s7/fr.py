import re,glob,collections,sys
root='/home/user/AdeeboMeter-dev/'
fr=open(root+'knowledge/lang-fr.txt',encoding='utf-8').read().split('\n')
keys=collections.OrderedDict(); dups=[]; bad=[]
for n,raw in enumerate(fr,1):
    l=raw.rstrip('\r')
    if not l or l[0]=='#': continue
    t=l.find('\t')
    if t<=0 or t==len(l)-1: bad.append((n,l)); continue
    k=l[:t]; v=l[t+1:]
    if k in keys: dups.append((n,k,keys[k][1],v))
    keys[k]=(n,v)
print('entries',len(keys),'dups',len(dups),'bad',len(bad))
for d in dups: print('DUP line',d[0],repr(d[1]),'old=',repr(d[2]),'new=',repr(d[3]))
for b in bad: print('BAD',b)
def unesc(s):
    return s.encode('utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8') if '\\' in s else s
lits=collections.defaultdict(list)
pat_lt=re.compile(r'L\.T\(\s*"((?:[^"\\]|\\.)*)"\s*\)')
pat_pill=re.compile(r'new PillButton\(\s*"((?:[^"\\]|\\.)*)"')
pat_round=re.compile(r'new RoundBox\([^;]*?,\s*"((?:[^"\\]|\\.)*)"\s*\)')
pat_text=re.compile(r'(\w+)\.Text\s*=\s*"((?:[^"\\]|\\.)*)"')
for f in glob.glob(root+'src/**/*.cs',recursive=True):
    src=open(f,encoding='utf-8').read()
    for i,line in enumerate(src.split('\n'),1):
        for m in pat_lt.finditer(line): lits[unesc(m.group(1))].append((f.replace(root,''),i,'LT'))
        for m in pat_pill.finditer(line): lits[unesc(m.group(1))].append((f.replace(root,''),i,'Pill'))
        for m in pat_round.finditer(line): lits[unesc(m.group(1))].append((f.replace(root,''),i,'Cue'))
miss=[(k,v) for k,v in lits.items() if k not in keys and k.strip()]
print('literals',len(lits),'missing',len(miss))
for k,v in sorted(miss,key=lambda x:x[1][0]): print('MISSING',repr(k),v[0], v[0][2], ('x%d'%len(v) if len(v)>1 else ''))
# unused keys
used=set(lits)
print('fr keys never used as a literal:',sum(1 for k in keys if k not in used))
# length ratio
long=[(k,v[1],len(v[1])/max(1,len(k))) for k,v in keys.items() if len(k)>=4]
long.sort(key=lambda x:-x[2])
for k,v,r in long[:25]: print('RATIO %.2f'%r,repr(k),'->',repr(v))
