import os, sys, subprocess, json, datetime
S="repo/.github/scripts/hof_verify.py"; J="repo/docs/hall-of-fame.json"; K="dummy-review-key"
def reset(): json.dump({"about":"x","entries":[]}, open(J,"w"))
def code(n,p,d): return subprocess.run([sys.executable,"-I",S,"--code",n,str(p),d],env={"HOF_KEY":K},capture_output=True,text=True).stdout.strip()
def body(n,p,d,c,w="Nowhere - just my name",h="_No response_"):
    return f"### Name on the card\n\n{n}\n\n### Points on the card\n\n{p}\n\n### Date on the card\n\n{d}\n\n### Claim code\n\n{c}\n\n### Where people can find you (optional)\n\n{w}\n\n### Your handle (optional)\n\n{h}"
def run(b, author="someone"):
    out=os.path.abspath("gho.txt"); open(out,"w").close()
    r=subprocess.run([sys.executable,"-I",S],env={"HOF_KEY":K,"ISSUE_BODY":b,"ISSUE_AUTHOR":author,"ISSUE_URL":"https://github.com/x/y/issues/7","GITHUB_OUTPUT":out},capture_output=True,text=True)
    return r.returncode, (r.stdout+r.stderr).strip()[:200], open(out).read().strip()[:200]
t=datetime.date.today().isoformat(); reset()
c=code("Ada Lovelace",512,t)
print("#9 thief first (X)", run(body("Ada Lovelace",512,t,c,"X","thief_handle"),author="thief"))
print("   entry:", [ (e.get("site"),e.get("handle"),e.get("by"),e.get("disputed")) for e in json.load(open(J))["entries"]])
print("#9 owner second (X)", run(body("Ada Lovelace",512,t,c,"X","ada_real"),author="ada"))
print("   entry:", [ (e.get("site"),e.get("handle"),e.get("by"),e.get("disputed")) for e in json.load(open(J))["entries"]])
reset(); print("#9 variant github handle not author", run(body("Ada Lovelace",512,t,c,"GitHub","ada_real"),author="thief"))
print("   entry:", [ (e.get("site"),e.get("handle"),e.get("by")) for e in json.load(open(J))["entries"]])
print("#23 empty name", run(body("",512,t,code("",512,t))))
print("#23 emoji name", run(body("🙂🙂",512,t,code("🙂🙂",512,t))))
reset(); print("#100 U+2011", run(body("Ada Lovelace",512,t,c.replace("-","‑"))))
print("#100 fullwidth", run(body("Ada Lovelace",512,t,"".join(chr(ord(ch)+0xFEE0) if 'A'<=ch<='Z' else ch for ch in c))))
print("#100 variant: cyrillic A in code", run(body("Ada Lovelace",512,t,c.replace("A","А",1))))
reset(); print("R2#5 cyrillic lookalike", run(body("Аda Lovelace",512,t,code("Аda Lovelace",512,t))))
