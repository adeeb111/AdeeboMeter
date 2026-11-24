import re,sys,collections
files=[l.strip() for l in open(sys.argv[1]) if l.strip()]
langs={}
for c in ['fr','es','ar','bn']:
    d={}
    for line in open('knowledge/lang-%s.txt'%c,encoding='utf-8'):
        line=line.rstrip('\r\n')
        if not line or line[0]=='#' or '\t' not in line: continue
        k,v=line.split('\t',1)
        d.setdefault(k,v)
    langs[c]=d
rx=re.compile(r'L\.T\(\s*"((?:[^"\\]|\\.)*)"\s*\)')
tot=0; miss=collections.Counter(); per=collections.defaultdict(list)
for f in files:
    s=open(f,encoding='utf-8-sig').read()
    for m in rx.finditer(s):
        k=m.group(1).replace('\\"','"').replace('\\n','\n').replace('\\\\','\\')
        tot+=1
        ms=[c for c in langs if not langs[c].get(k,'').strip()]
        if ms: per[f].append((k,ms))
for f in files:
    if per[f]:
        print('==',f,len(per[f]))
        for k,ms in per[f][:60]: print('   ',','.join(ms),'|',k[:110])
print('total',tot)
