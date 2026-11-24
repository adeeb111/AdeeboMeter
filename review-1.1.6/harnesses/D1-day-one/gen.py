import re,os,sys
src='/home/user/AdeeboMeter-dev/src'
ents={}
pat=re.compile(r'DllImport\("([A-Za-z0-9_.]+)"([^\]]*)\]\s*(?:\[[^\]]*\]\s*)*(?:(?:public|private|internal|static|extern|unsafe)\s+)+([\w.\[\]<>]+)\s+(\w+)\s*\(',re.S)
for r,d,fs in os.walk(src):
  for f in fs:
    if f.endswith('.cs'):
      t=open(os.path.join(r,f),encoding='utf-8-sig').read()
      for m in pat.finditer(t):
        dll,attrs,ret,name=m.groups()
        ep=re.search(r'EntryPoint\s*=\s*"(\w+)"',attrs)
        n=ep.group(1) if ep else name
        cs='CharSet.Unicode' in attrs or 'CharSet = System.Runtime.InteropServices.CharSet.Unicode' in attrs
        ents.setdefault(dll.lower(),set()).add(n)
        # mono probes W/A suffix variants too; add both
        ents[dll.lower()].add(n+'W'); ents[dll.lower()].add(n+'A')
ones={'IsWindowEnabled','IsWindowEnabledW','IsWindowEnabledA'}
neg={'GetCurrentProcess'}
out=['#include <stdint.h>']
names=set()
errdll={}
for dll,s in ents.items():
  names|=s
  for n in s: errdll[n]=dll
for n in sorted(names):
  if n.startswith('GetTickCount64'):
    out.append('#include <time.h>\nint64_t %s(){struct timespec t;clock_gettime(CLOCK_MONOTONIC,&t);return (int64_t)t.tv_sec*1000+t.tv_nsec/1000000;}'%n); continue
  v='1' if n in ones else ('-1' if n in neg else '0')
  if errdll[n].split('.')[0] in ('iphlpapi','wlanapi','dhcpcsvc','dnsapi','netapi32','cfgmgr32','bcrypt','powrprof') or n.startswith('SHQueryUserNotificationState') or n.startswith('SHGetKnownFolderPath') or n.startswith('CreateDXGIFactory') or n.startswith('WinVerifyTrust'): v='50'
  if n.startswith('Icmp') and 'SendEcho' in n: v='0'
  out.append('int64_t %s(){return %s;}'%(n,v))
open('shim.c','w').write('\n'.join(out)+'\n')
cfg=['<configuration>']
for dll in ents:
  cfg.append('<dllmap dll="%s" target="%s/libshim.so"/>'%(dll,os.getcwd()))
  cfg.append('<dllmap dll="%s" target="%s/libshim.so"/>'%(dll.replace('.dll',''),os.getcwd()))
cfg.append('</configuration>')
open('AdeeboMeter.exe.config','w').write('\n'.join(cfg))
print(len(names), sorted(ents))
