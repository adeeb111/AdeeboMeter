import re
root='/home/user/AdeeboMeter-dev/'
keys={}
for l in open(root+'knowledge/lang-fr.txt',encoding='utf-8').read().split('\n'):
    l=l.rstrip('\r')
    if l and l[0]!='#' and '\t' in l: k,v=l.split('\t',1); keys[k]=v
src=open(root+'src/Ui/FullView.Toolbox.cs',encoding='utf-8').read()
titles=re.findall(r'T\("(\w+)", "[^"]*", "((?:[^"\\]|\\.)*)"',src)
miss=[t for t in titles if t[1] not in keys]
print('tool titles',len(titles),'untranslated',len(miss)); print([m[1] for m in miss])
fv=open(root+'src/Ui/FullView.cs',encoding='utf-8').read()
tn=re.findall(r'new\[\] \{ "([^"]*)", "([^"]*)" \}',fv)
print('tabNames missing',[x for p in tn for x in p if x not in keys])
secs=re.findall(r'new\[\] \{ "(\w+)", "([^"]+)", "\w+"',fv)
print('Secs missing',[s[1] for s in secs if s[1] not in keys])
for m in ['RECENT','QUICK','Did you mean','Run','Stop','Example','Fill the boxes','Network','Devices','Traffic Lab','Engineer','Windows']:
    print(repr(m),'->',repr(keys.get(m)))
