import ipaddress, random, subprocess, re
random.seed(11)
cases=[]
for _ in range(1500):
    pre=random.randint(0,30)
    base=random.getrandbits(32)
    k=random.randint(1,10)
    maxh=max(1,(1<<(32-pre))//random.choice([1,2,4,8,16,64]))
    sizes=[random.randint(1,min(maxh,16777214)) for _ in range(k)]
    if random.random()<0.3: sizes=[random.choice([1,2,3,4,5,6,7,14,15,30,31,62,63,126,127,254,255,510]) for _ in range(k)]
    cases.append((str(ipaddress.IPv4Address(base))+"/"+str(pre), sizes))
inp="".join("vlsm\t%s\t%s\n"%(b,", ".join(map(str,s))) for b,s in cases)
out=subprocess.run(["mono","H.exe"],input=inp,capture_output=True,text=True).stdout.splitlines()
def fmt(n): return "{:,}".format(n)
def ref(block,sizes):
    net=ipaddress.IPv4Network(block,strict=False)
    at=int(net.network_address); end=at+net.num_addresses; res={}; used=0; full=False
    for i,n in sorted(enumerate(sizes),key=lambda x:(-x[1],x[0])):
        if n<=2: p=30
        else:
            p=30
            while p>0 and (1<<(32-p))-2<n: p-=1
        size=1<<(32-p)
        a=(at+size-1)//size*size
        if a+size>end: res[i]=None; full=True; continue
        sn=ipaddress.IPv4Network((a,p))
        hosts=list((sn.network_address+1, sn.broadcast_address-1))
        res[i]="%s/%d  ·  %s - %s  ·  gateway %s  ·  broadcast %s"%(sn.network_address,p,hosts[0],hosts[1],hosts[0],sn.broadcast_address)
        res[i]+=" | %s usable, %s spare · mask %s"%(fmt(size-2),fmt(size-2-n),sn.netmask)
        at=a+size; used+=size
    tot=net.num_addresses
    u="%s of %s addresses"%(fmt(used),fmt(tot))+(" · free from %s"%ipaddress.IPv4Address(at) if at<end and not full else "")
    return res,u
mm=0
for (b,s),o in zip(cases,out):
    o=o.lstrip('﻿')
    got={}; gu=None
    for r in o.split(" || "):
        f=r.split(" | ")
        if len(f)>1 and f[1]=="Used": gu=f[2]
        m=re.match(r"Subnet (\d+) ",f[1]) if len(f)>1 else None
        if m:
            i=int(m.group(1))-1
            got[i]=None if "doesn't fit" in f[2] else f[2]+" | "+f[3]
    e,eu=ref(b,s)
    if got!=e or gu!=eu:
        mm+=1
        if mm<8: print("MISMATCH",b,s,"\n got",got,gu,"\n exp",e,eu)
print("cases",len(cases),"mismatches",mm)
