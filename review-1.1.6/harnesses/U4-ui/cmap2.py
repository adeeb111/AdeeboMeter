import sys, struct
def cmap(path):
    d=open(path,'rb').read(); n=struct.unpack('>H',d[4:6])[0]; tabs={}
    for i in range(n):
        tag,cs,off,ln=struct.unpack('>4sIII',d[12+16*i:28+16*i]); tabs[tag]=(off,ln)
    off=tabs[b'cmap'][0]; nt=struct.unpack('>H',d[off+2:off+4])[0]; s=set()
    for i in range(nt):
        pid,eid,so=struct.unpack('>HHI',d[off+4+8*i:off+12+8*i]); t=off+so; fmt=struct.unpack('>H',d[t:t+2])[0]
        if fmt==4:
            segx2=struct.unpack('>H',d[t+6:t+8])[0]; sc=segx2//2
            ends=struct.unpack('>%dH'%sc,d[t+14:t+14+segx2]); st=struct.unpack('>%dH'%sc,d[t+16+segx2:t+16+2*segx2])
            for a,b in zip(st,ends): s.update(range(a,b+1))
        elif fmt==12:
            ng=struct.unpack('>I',d[t+12:t+16])[0]
            for j in range(ng):
                a,b,_=struct.unpack('>III',d[t+16+12*j:t+28+12*j]); s.update(range(a,b+1))
    return s
chars="↓↑→←›‹…·★▲▼●✕°×−–—✓⋯≈±²€£¥₹৳⚠↻"
for f in sys.argv[1:]:
    cm=cmap(f); print(f.split('/')[-1], "missing:", " ".join(c for c in chars if ord(c) not in cm))
