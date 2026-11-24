import os, sys, subprocess, json, shutil, datetime
S="repo/.github/scripts/hof_verify.py"; J="repo/docs/hall-of-fame.json"
K="dummy-review-key"
def reset(): json.dump({"about":"x","entries":[]}, open(J,"w"))
def code(n,p,d): return subprocess.run([sys.executable,"-I",S,"--code",n,str(p),d],env={"HOF_KEY":K},capture_output=True,text=True).stdout.strip()
def body(n,p,d,c,w="Nowhere - just my name",h="_No response_"):
    return f"### Name on the card\n\n{n}\n\n### Points on the card\n\n{p}\n\n### Date on the card\n\n{d}\n\n### Claim code\n\n{c}\n\n### Where people can find you (optional)\n\n{w}\n\n### Your handle (optional)\n\n{h}"
def run(b, author="someone"):
    r=subprocess.run([sys.executable,"-I",S],env={"HOF_KEY":K,"ISSUE_BODY":b,"ISSUE_AUTHOR":author,"ISSUE_URL":"https://github.com/adeeb111/AdeeboMeter/issues/7"},capture_output=True,text=True)
    return r.returncode, (r.stdout+r.stderr).strip()[:300]
today=datetime.date.today().isoformat()
reset()
cases=[]
# 1 normal
c=code("Ada Lovelace",512,today); print("normal",run(body("Ada Lovelace",512,today,c)))
# 2 duplicate same code
print("dup",run(body("Ada Lovelace",512,today,c)))
# 3 same person different case
c2=code("ADA LOVELACE",600,today); print("case dup",run(body("ADA LOVELACE",600,today,c2)))
# 4 99999
c3=code("Mallory",99999,today); print("99999",run(body("Mallory",99999,today,c3)))
# 5 900
c4=code("Mallory",900,today); print("900",run(body("Mallory",900,today,c4)))
# 6 html name
n='<img src=x onerror=alert(1)>Eve'; c5=code(n,600,today); print("html",run(body(n,600,today,c5)))
# 7 unicode digits points
c6=code("Bob",512,today); print("arabic-digit pts",run(body("Bob","5١٢",today,c6)))
# 8 huge body
print("huge",run(body("A"*65000,512,today,"x")))
# 9 RTL override name
n="‮evil‬ Carl"; c7=code(n,512,today); print("rtl",run(body(n,512,today,c7)))
# 10 handle injection
n="Dana"; c8=code(n,512,today); print("handle",run(body(n,512,today,c8,"Bluesky","https://evil.com/?q=bsky.app/profile/.. ")))
# 11 zalgo
n="Z"+"́"*40; c9=code(n,512,today); print("zalgo",run(body(n,512,today,c9)))
# 12 lookalike: Cyrillic A
n="Аda Lovelace"; c10=code(n,512,today); print("cyrillic lookalike",run(body(n,512,today,c10)))
# 13 name with injected section in value
n="Fred"; c11=code(n,512,today); b=body(n,512,today,c11)+"\n\n### Name on the card\n\nOther"; print("double field",run(b))
# 14 date unicode digits
print("date arabic",run(body("Gus",512,"٢٠٢٦-١١-١٢","x")))
# 15 author junk
n="Hal"; c12=code(n,512,today); print("author", run(body(n,512,today,c12), author="a;b"))
# 16 instagram ..
n="Ivy"; c13=code(n,512,today); print("insta ..",run(body(n,512,today,c13,"Instagram","..")))
print(json.dumps(json.load(open(J)),ensure_ascii=False,indent=1)[:3000])
