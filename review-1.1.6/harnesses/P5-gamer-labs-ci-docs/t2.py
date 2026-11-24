from playwright.sync_api import sync_playwright
B="http://127.0.0.1:8765/"
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/opt/pw-browsers/chromium-1243/chrome-linux64/chrome", args=["--no-sandbox"])
    ctx=br.new_context(viewport={"width":360,"height":800}, service_workers="block")
    page=ctx.new_page(); page.route("**/*", lambda r,q: r.continue_() if q.url.startswith(B) else r.abort())
    page.goto(B+"ar/index.html"); page.wait_for_timeout(800)
    print("LS after ar/index:", page.evaluate("JSON.stringify(Object.assign({},localStorage))"))
    info=page.evaluate("""()=>{const out=[];for(const e of document.querySelectorAll('body *')){const r=e.getBoundingClientRect(); if(r.width>0&&r.left<-1){const s=getComputedStyle(e); out.push(e.tagName+'.'+e.className+' vis='+s.visibility+' op='+s.opacity+' disp='+s.display+' pos='+s.position+' L='+Math.round(r.left)+' T='+Math.round(r.top)+' parent='+e.parentElement.tagName+'.'+e.parentElement.className)}} return out.slice(0,12)}""")
    print("\n".join(info))
    page.screenshot(path="ar360.png")
    page.goto(B+"app.html"); page.wait_for_timeout(1000)
    print("app dir:", page.evaluate("document.documentElement.dir+' '+document.documentElement.lang"))
    page.goto(B+"index.html"); page.wait_for_timeout(1000)
    print("index dir:", page.evaluate("document.documentElement.dir+' '+document.documentElement.lang+' '+document.title"))
    page.screenshot(path="en-after-ar.png")
    br.close()
