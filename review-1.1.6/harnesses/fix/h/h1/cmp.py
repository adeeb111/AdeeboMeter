import base64,sys,ast
sys.path.insert(0,'/home/user/AdeeboMeter-dev/.github/scripts')
import hof_verify as h
names=ast.literal_eval(open('names.json').read())
bad=0
for n,l in zip(names,open('cs.out').read().splitlines()):
    b,k=l.split(' ')
    c=base64.b64decode(b).decode('utf-8')
    pc=h.clean_name(n); pk=h.code("testkey",n,512,"2026-11-12")
    pk2=h.code("testkey",c,512,"2026-11-12")  # what the user types from the card
    if c!=pc or k!=pk2:
        bad+=1
        if bad<15: print(repr(n),'| cs',repr(c),'| py',repr(pc), k==pk, k==pk2)
print("mismatches",bad,"of",len(names))
