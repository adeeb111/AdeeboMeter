import re,ipaddress
root='/home/user/AdeeboMeter-dev/'
src=open(root+'src/Ui/FullView.Toolbox.cs',encoding='utf-8').read()
body=src[src.index('tools = new List<Tool> {'):src.index('// grouped by the question')]
tools={}
for m in re.finditer(r'T\("(\w+)", "[^"]*", "((?:[^"\\]|\\.)*)", (null|SnmpIn \+ "((?:[^"\\]|\\.)*)"|SnmpIn|"((?:[^"\\]|\\.)*)")',body):
    tid=m.group(1); g3=m.group(3)
    if g3=='null': ins=[]
    elif g3=='SnmpIn': ins=['Device - e.g. 10.0.0.1 or host:161','Community - or v3:user[:authpass[:privpass]]']
    elif g3.startswith('SnmpIn +'): ins=['Device - e.g. 10.0.0.1 or host:161','Community - or v3:user[:authpass[:privpass]]']+m.group(4).split('|')[1:]
    else: ins=m.group(5).replace('\\\\','\\').split('|')
    tools[tid]=ins
print('tools',len(tools))
ex={}; cur=None; order=[]
for raw in open(root+'knowledge/examples.txt',encoding='utf-8').read().split('\n'):
    l=raw.rstrip('\r')
    if not l.strip() or l.startswith('#'): continue
    if l.startswith('== '):
        cur=l[3:].strip(); order.append(cur)
        if cur in ex: print('DUP example',cur)
        ex[cur]={'in':[],'rows':[]}; continue
    if cur is None: continue
    if l.startswith('in: '): ex[cur]['in']=[s.strip() for s in l[4:].split('|')]; continue
    p=l.split('|')
    if len(p)<3: print('SHORT row in',cur,repr(l)); continue
    ex[cur]['rows'].append(p)
print('examples',len(ex))
print('tools without example',[t for t in tools if t not in ex])
print('examples without tool',[e for e in ex if e not in tools])
for t,ins in tools.items():
    e=ex.get(t)
    if not e: continue
    if len(e['in'])>len(ins): print('TOO MANY IN',t,e['in'],ins)
    for i,v in enumerate(e['in']):
        lab=ins[i] if i<len(ins) else ''
        sec=lab.startswith(('Shared secret','Community','Password','Code'))
        if sec and v: print('SECRET BOX FILLED? (skipped by fill)',t,lab,'=',v)
        for tok in re.findall(r'[\w.:\[\]-]+',v):
            tok2=tok.strip('[]')
            h=tok2
            try:
                ip=ipaddress.ip_address(h)
                doc=any(ip in ipaddress.ip_network(n) for n in ['192.0.2.0/24','198.51.100.0/24','203.0.113.0/24','2001:db8::/32'])
                if not doc and not ip.is_private: print('PUBLIC non-doc IP',t,i,repr(v),'| label:',lab)
                elif not doc: print('private IP (real LAN)',t,i,repr(v),'| label:',lab)
            except ValueError:
                hp=h.rsplit(':',1)[0] if h.count(':')==1 else h
                try:
                    ip=ipaddress.ip_address(hp)
                    doc=any(ip in ipaddress.ip_network(n) for n in ['192.0.2.0/24','198.51.100.0/24','203.0.113.0/24'])
                    if not doc: print('IP:port non-doc',t,i,repr(v))
                    continue
                except ValueError: pass
                if re.match(r'^[a-z0-9-]+(\.[a-z0-9-]+)*\.[a-z]{2,}$',hp,re.I) and not re.search(r'(^|\.)example\.(com|net|org)$|\.(pcapng|pcap|crt|cer|pfx|csr|zip|pdf|adeebo|txt|p7m|adeebokey|pem|key|lab|test|invalid|local|lan|internal|home\.arpa)$',hp,re.I):
                    print('real-looking name',t,i,repr(v),'| label:',lab)
    for r in e['rows']:
        try: lv=int(r[0])
        except: print('BAD lv',t,r); continue
        if lv<-1 or lv>3: print('lv out of range',t,r[:3])
lay=src[src.index('static readonly string[][] TbLayout'):src.index('static readonly Dictionary<string, string> GroupSection')]
ids=[]
for m in re.finditer(r'new\[\] \{ "([^"]+)", ([^}]*)\}',lay):
    ids+= re.findall(r'"(\w+)"',m.group(2))
print('not in layout',[t for t in tools if t not in ids]); print('layout not tools',[i for i in ids if i not in tools]); import collections; print('dup layout',[k for k,v in collections.Counter(ids).items() if v>1])
