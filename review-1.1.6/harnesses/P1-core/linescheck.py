import re,glob,os,collections
K='/home/user/AdeeboMeter-dev/knowledge/'
KEYS=set(re.search(r'Keys = \{(.*?)\};',open('/home/user/AdeeboMeter-dev/src/Lines.cs',encoding='utf-8').read(),re.S).group(1).replace('"','').replace(' ','').replace('\n','').split(','))
def parse(path):
    p=collections.OrderedDict(); bad=[]
    for n,l0 in enumerate(open(path,encoding='utf-8').read().split('\n'),1):
        l=l0.strip()
        if not l or l[0]=='#': continue
        b=l.find('|')
        if b<=0: bad.append((n,l[:60])); continue
        k=l[:b].strip(); t=l[b+1:].strip()
        if not k or not t: bad.append((n,l[:60])); continue
        p.setdefault(k,[]).append(t)
    return p,bad
ids=sorted(set(os.path.basename(f)[6:-4] for f in glob.glob(K+'lines-*.txt') if not re.search(r'-(ar|bn|es|fr|zh)\.txt$',f)))
print(len(ids),'ids')
for i in ids:
    en,b=parse(K+'lines-%s.txt'%i)
    if b: print(i,'en bad',b)
    for k in en:
        if k not in KEYS: print(i,'en unknown key',k)
    for c in ['es','ar','bn']:
        f=K+'lines-%s-%s.txt'%(i,c)
        if not os.path.exists(f): print('MISSING',f); continue
        tr,b=parse(f)
        if b: print(i,c,'bad',b)
        if set(tr)!=set(en): print(i,c,'keys only-tr',set(tr)-set(en),'only-en',set(en)-set(tr))
        for k in tr:
            if k not in KEYS: print(i,c,'unknown key',k)
            if k in en and len(tr[k])!=len(en[k]): print(i,c,k,'variants',len(en[k]),'->',len(tr[k]))
            for t in tr[k]:
                for m in re.findall(r'\{[^}]*\}',t):
                    if m not in('{v}','{addr}','{Addr}'): print(i,c,k,'BAD PH',m,t)
                if t.count('{')!=t.count('}'): print(i,c,k,'unbalanced',t)
                if len(t)>110: print(i,c,k,'len',len(t))
            if k in en:
                env=sum('{v}' in t for t in en[k]); trv=sum('{v}' in t for t in tr[k])
                if env!=trv: print(i,c,k,'{v} count',env,'->',trv)
                # all-variants-with-{v}: then no line without v
                if all('{v}' in t for t in tr[k]) and not all('{v}' in t for t in en[k]): print(i,c,k,'all tr need {v}, en has v-less')
                ena=sum('{addr}' in t.lower() or '{addr}' in t for t in en[k]); tra=sum('{addr}' in t or '{Addr}' in t for t in tr[k])
                if ena!=tra: print(i,c,k,'{addr} count',ena,'->',tra)
