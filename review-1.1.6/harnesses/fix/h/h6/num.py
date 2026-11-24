import random, subprocess
random.seed(3)
cases=[]
for _ in range(2000):
    bits=random.choice([1,4,8,12,16,24,32,40,48,56,63,64])
    v=random.getrandbits(bits)
    k=random.randint(0,7)
    if k==0: s=str(v); kind=v
    elif k==1: s="0x%X"%v; kind=v
    elif k==2: s="%xh"%v; kind=v
    elif k==3: s="0b"+bin(v)[2:]; kind=v
    elif k==4: s=bin(v)[2:]+"b"; kind=v
    elif k==5:
        v&=0xFFFFFFFF; s=".".join(str((v>>s_)&255) for s_ in (24,16,8,0)); kind=v
    elif k==6:
        v&=0xFFFFFFFFFFFF; s=":".join("%02x"%((v>>(40-8*i))&255) for i in range(6)); kind=v
    else:
        s="{:,}".format(v); kind=v
    cases.append((s,kind))
inp="".join("num\t%s\n"%s for s,_ in cases)
out=subprocess.run(["mono","H.exe"],input=inp,capture_output=True,text=True).stdout.splitlines()
mm=0
for (s,v),o in zip(cases,out):
    o=o.lstrip('﻿'); rows={r.split(" | ")[1]:r.split(" | ") for r in o.split(" || ") if len(r.split(" | "))>2}
    ok = "Decimal" in rows and rows["Decimal"][2]==str(v) and rows["Hex"][2]=="0x%X"%v
    if ok:
        b=bin(v)[2:]; w=8 if len(b)<=8 else 16 if len(b)<=16 else 32 if len(b)<=32 else 48 if len(b)<=48 else 64
        b=b.zfill(w); exp=" ".join(b[i:i+8] for i in range(0,w,8))
        ok = rows["Binary"][2]==exp
    if not ok:
        mm+=1
        if mm<15: print("MISMATCH",repr(s),v,rows.get("Read as"),rows.get("Decimal"))
print("cases",len(cases),"mismatches",mm)
