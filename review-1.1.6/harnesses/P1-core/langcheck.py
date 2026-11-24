import re,sys,glob,os,collections
K='/home/user/AdeeboMeter-dev/knowledge/'
ph=re.compile(r'\{[^{}]*\}')
def fmt_ok(s):
    # .NET composite format validity: braces balanced with {{ }} escapes
    i=0;n=len(s)
    while i<n:
        c=s[i]
        if c=='{':
            if i+1<n and s[i+1]=='{': i+=2; continue
            j=s.find('}',i)
            if j<0: return False
            if not re.match(r'^\{\d+(,-?\d+)?(:[^{}]*)?\}$', s[i:j+1]): return False
            i=j+1; continue
        if c=='}':
            if i+1<n and s[i+1]=='}': i+=2; continue
            return False
        i+=1
    return True
tables={}
for code in ['fr','es','ar','bn','zh']:
    d=collections.OrderedDict(); dup=[];bad=[]
    for ln,raw in enumerate(open(K+'lang-%s.txt'%code,encoding='utf-8').read().split('\n'),1):
        line=raw.rstrip('\r')
        if not line or line[0]=='#': continue
        t=line.find('\t')
        if t<=0: bad.append(ln); continue
        k=line[:t]; v=line[t+1:]
        if k in d: dup.append(k); continue
        d[k]=v
    tables[code]=d
    print('==',code,len(d),'dups',len(dup),'bad',bad[:5])
    for x in dup[:5]: print('  dup',repr(x))
    empty=[k for k,v in d.items() if not v.strip()]
    print('  empty',len(empty), [e for e in empty[:5]])
    same=[k for k,v in d.items() if v==k and re.search('[a-z]{4}',k)]
    print('  identical-to-English',len(same))
    for k,v in d.items():
        a=sorted(ph.findall(k)); b=sorted(ph.findall(v))
        if a!=b and v.strip():
            print('  PH',repr(k),'->',repr(v))
        if '\t' in v: print('  TAB in value',repr(k))
        if v!=v.strip() and v.strip(): 
            if (k!=k.strip())!=(v!=v.strip()): print('  WS',repr(k),repr(v))
        if k.endswith(' ') != v.endswith(' ') and v.strip(): print('  trailspace',repr(k),repr(v))
        if '{' in k and fmt_ok(k) and not fmt_ok(v) and v.strip(): print('  BADFMT',repr(k),repr(v))
# keysets
keys={c:set(d) for c,d in tables.items()}
for c in keys:
    for c2 in keys:
        if c<c2 and c in('es','ar','bn') and c2 in('es','ar','bn'):
            print(c,c2,'only in',c,len(keys[c]-keys[c2]),'only in',c2,len(keys[c2]-keys[c]))
print('fr missing vs es',len(keys['es']-keys['fr']),'fr extra',len(keys['fr']-keys['es']))
