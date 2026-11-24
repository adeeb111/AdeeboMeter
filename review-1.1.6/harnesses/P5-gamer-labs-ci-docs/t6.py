from playwright.sync_api import sync_playwright
import re,sys
B="http://127.0.0.1:8765/"
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/opt/pw-browsers/chromium-1243/chrome-linux64/chrome", args=["--no-sandbox"])
    for pg in sys.argv[1].split(','):
        ctx=br.new_context(viewport={"width":1366,"height":900}, service_workers="block")
        page=ctx.new_page(); page.route("**/*", lambda r,q: r.continue_() if q.url.startswith(B) else r.abort())
        page.goto(B+pg); page.wait_for_timeout(1000)
        txt=page.evaluate("""()=>{const out=[];const w=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT);let n;while(n=w.nextNode()){const t=n.textContent.trim(); if(!t) continue; const e=n.parentElement; if(!e||e.closest('script,style,code,pre,noscript')) continue; const s=getComputedStyle(e); if(s.display==='none'||s.visibility==='hidden'||e.getClientRects().length===0) continue; out.push(t);} return out}""")
        eng=[t for t in txt if len(re.findall(r'\b[a-z]{3,}\b',t))>=4]
        print("==",pg,len(txt),"text nodes;",len(eng),"English-looking")
        for t in eng[:12]: print("   ",t[:150])
        ctx.close()
    br.close()
