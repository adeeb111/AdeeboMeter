# tiny helper: exact, counted replacements that keep a file's BOM and line endings
import sys
def edit(path, pairs):
    raw=open(path,'rb').read(); bom=raw[:3]==b'\xef\xbb\xbf'; s=raw.decode('utf-8-sig'); crlf='\r\n' in s; s=s.replace('\r\n','\n')
    for a,b,*n in pairs:
        n=n[0] if n else 1; c=s.count(a)
        if (n=='all' and c<1) or (n!='all' and c!=n): raise SystemExit(f'{path}: expected {n} of {a[:80]!r}, found {c}')
        s=s.replace(a,b)
    if crlf: s=s.replace('\n','\r\n')
    open(path,'wb').write((b'\xef\xbb\xbf' if bom else b'')+s.encode('utf-8'))
