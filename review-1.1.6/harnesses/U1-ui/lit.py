import re,sys
files=[l.strip() for l in open(sys.argv[1]) if l.strip()]
d={}
for line in open('knowledge/lang-fr.txt',encoding='utf-8'):
    line=line.rstrip('\r\n')
    if '\t' in line and not line.startswith('#'):
        k,v=line.split('\t',1); d.setdefault(k,v)
rx=re.compile(r'"((?:[^"\\]|\\.)*)"')
for f in files:
    for i,line in enumerate(open(f,encoding='utf-8-sig'),1):
        if line.strip().startswith('//'): continue
        code=line.split(' // ')[0]
        for m in rx.finditer(code):
            s=m.group(1)
            if len(s)<4 or ' ' not in s or not re.search('[A-Za-z]{3}',s): continue
            if s in d and d[s].strip(): continue
            if re.match(r'^[\\@^(?\[]',s) or 'Regex' in code[max(0,m.start()-40):m.start()]: continue
            pre=code[max(0,m.start()-12):m.start()]
            if 'L.T(' in pre or 'Log(' in code[max(0,m.start()-30):m.start()]: continue
            print('%s:%d: %s'%(f.split('/')[-1],i,s[:100]))
