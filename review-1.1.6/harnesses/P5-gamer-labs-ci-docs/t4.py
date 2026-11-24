from playwright.sync_api import sync_playwright
B="http://127.0.0.1:8765/"
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/opt/pw-browsers/chromium-1243/chrome-linux64/chrome", args=["--no-sandbox"])
    for pg in ["ar/index.html","index.html"]:
        ctx=br.new_context(viewport={"width":360,"height":800}, service_workers="block")
        page=ctx.new_page(); page.route("**/*", lambda r,q: r.continue_() if q.url.startswith(B) else r.abort())
        page.goto(B+pg); page.wait_for_timeout(800)
        print(pg, page.evaluate("""()=>{const t=document.querySelector('.v5ltiles'); const r=t.getBoundingClientRect(); const s=getComputedStyle(t); return 'tiles L='+Math.round(r.left)+' R='+Math.round(r.right)+' ovx='+s.overflowX+' sw='+t.scrollWidth+' cw='+t.clientWidth+' kids='+[...t.children].map(c=>Math.round(c.getBoundingClientRect().left)+'..'+Math.round(c.getBoundingClientRect().right)).join(' ')}"""))
        page.evaluate("document.querySelector('.v5ltiles').scrollIntoView({block:'center'})"); page.wait_for_timeout(300)
        page.screenshot(path="tiles-%s.png"%pg.replace('/','_'))
        ctx.close()
    br.close()
