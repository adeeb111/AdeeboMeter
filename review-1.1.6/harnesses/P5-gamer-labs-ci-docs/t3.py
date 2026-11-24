from playwright.sync_api import sync_playwright
import sys
B="http://127.0.0.1:8765/"
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/opt/pw-browsers/chromium-1243/chrome-linux64/chrome", args=["--no-sandbox"])
    for pg,w in [("ar/index.html",360),("index.html",360),("ar/index.html",768)]:
        ctx=br.new_context(viewport={"width":w,"height":800}, service_workers="block")
        page=ctx.new_page(); page.route("**/*", lambda r,q: r.continue_() if q.url.startswith(B) else r.abort())
        page.goto(B+pg); page.wait_for_timeout(800)
        print(pg, w, "open before:", page.evaluate("document.querySelector('details.navmore')&&document.querySelector('details.navmore').open"))
        page.click("details.navmore > summary"); page.wait_for_timeout(400)
        print(page.evaluate("""()=>{const d=document.querySelector('details.navmore > div'); const r=d.getBoundingClientRect(); return 'menu L='+Math.round(r.left)+' R='+Math.round(r.right)+' vw='+innerWidth}"""))
        page.screenshot(path="menu-%s-%d.png"%(pg.replace('/','_'),w))
        ctx.close()
    br.close()
