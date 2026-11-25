import sys
def patch(path, pairs):
    s = open(path, encoding='utf-8').read()
    for i,(a,b) in enumerate(pairs):
        n = s.count(a)
        if n != 1: print("FAIL", path, i, n, a[:90]); sys.exit(1)
        s = s.replace(a, b)
    open(path, 'w', encoding='utf-8').write(s); print("ok", path, len(pairs))
