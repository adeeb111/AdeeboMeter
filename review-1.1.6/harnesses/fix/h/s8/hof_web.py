import json
from playwright.sync_api import sync_playwright
entries=[
 {"name":"<img src=x onerror=alert(1)>","points":512,"date":"2026-11-13","code":"x","site":"github","handle":"ada"},
 {"name":"W"*32,"points":600,"date":"2026-11-13","code":"y","site":"linkedin","handle":"a"*100},
 {"name":"محمد عبدالله Adeeb","points":700,"date":"2026-11-14","code":"z","site":"bluesky","handle":"a"*253},
 {"name":"Z"+"́"*31,"points":512,"date":"2026-11-12","code":"q","site":"instagram","handle":".."},
 {"name":"Eve","points":512,"date":"2026-11-12","code":"w","site":"x","handle":"javascript:alert(1)"},
 {"name":"Mal","points":512,"date":"2026-11-12","code":"v","site":"constructor","handle":"x"},
]
import sys
mode=sys.argv[1]
if mode=="proto": data={"entries":entries}
else: data={"entries":[e for e in entries if e["site"]!="constructor"]}
json.dump(data,open("site/hall-of-fame.json","w"),ensure_ascii=False)
with sync_playwright() as p:
    br=p.chromium.launch(executable_path="/usr/bin/google-chrome", args=["--no-sandbox"])
    for w in [360,1366]:
        pg=br.new_page(viewport={"width":w,"height":900}); errs=[]; dialogs=[]
        pg.on("console", lambda m: errs.append(m.text[:150]) if m.type=="error" else None)
        pg.on("pageerror", lambda e: errs.append("pageerror "+str(e)[:150]))
        pg.on("dialog", lambda d: (dialogs.append(d.message), d.dismiss()))
        pg.on("response", lambda r: errs.append("HTTP %d %s"%(r.status,r.url)) if r.status>=400 else None)
        pg.goto("http://127.0.0.1:8765/hall-of-fame.html"); pg.wait_for_timeout(1000)
        print(w, "count:", pg.inner_text("#count"), "| cards:", pg.locator(".winner").count(), "| dialogs:", dialogs, "| errs:", errs)
        print("  sw", pg.evaluate("document.documentElement.scrollWidth"))
        for c in pg.locator(".winner").all():
            b=c.bounding_box(); n=c.locator(".n").bounding_box(); a=c.locator("a")
            print("  card h=%d nameh=%d link=%s"%(b["height"],n["height"], (a.get_attribute("href") if a.count() else None)), "| overflowLink:", pg.evaluate("e=>{const a=e.querySelector('a'); return a? a.scrollWidth>e.clientWidth : false}", c.element_handle()))
        pg.screenshot(path=f"hof_{mode}_{w}.png", full_page=False)
    br.close()
