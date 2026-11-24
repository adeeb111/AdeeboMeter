from playwright.sync_api import sync_playwright
import re, time, os, sys
D=sys.argv[1]; B="http://127.0.0.1:8777/"
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/opt/pw-browsers/chromium-1243/chrome-linux64/chrome", args=["--no-sandbox"])
    ctx=br.new_context()
    page=ctx.new_page(); page.route("**/*", lambda r,q: r.continue_() if q.url.startswith(B) else r.abort())
    page.goto(B+"app.html"); page.wait_for_timeout(1500)
    page.evaluate("navigator.serviceWorker.ready.then(()=>1)"); page.wait_for_timeout(6000)
    print("controlled:", page.evaluate("!!navigator.serviceWorker.controller"), "caches:", page.evaluate("caches.keys()"))
    # "release": new HTML marker + new home.js/brand-app.css content + new sw version
    h=open(D+"/app.html",encoding='utf-8').read(); open(D+"/app.html","w",encoding='utf-8').write(h.replace("</body>","<div id=relmark>v2</div></body>",1))
    open(D+"/voice.js","a").write("\nwindow.__ver='v2';\n")
    s=open(D+"/sw.js").read(); open(D+"/sw.js","w").write(re.sub(r'const V = "am-[0-9a-f]+"','const V = "am-release2"',s))
    page.reload(); page.wait_for_timeout(800)
    print("after release, 1st load: html marker:", page.evaluate("!!document.getElementById('relmark')"), " voice.js version:", page.evaluate("window.__ver||'v1 (old cached copy)'"))
    page.wait_for_timeout(8000); page.reload(); page.wait_for_timeout(800)
    print("2nd load: html marker:", page.evaluate("!!document.getElementById('relmark')"), " voice.js version:", page.evaluate("window.__ver||'v1 (old cached copy)'"), "caches:", page.evaluate("caches.keys()"))
    br.close()
