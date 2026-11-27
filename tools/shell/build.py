from pathlib import Path
root=Path(__file__).resolve().parents[2]
p=Path(__file__).parent
source=(p/'shell.src.html').read_text(encoding='utf-8-sig')
for name, marker in [('core','CORE'),('ui','UI')]:
    js=(p/(name+'.js')).read_text(encoding='utf-8-sig')
    assert '</script' not in js.lower()
    source=source.replace('/* '+marker+' */',js)
(root/'docs/shell.html').write_text(source,encoding='utf-8',newline='\n')
print('Built docs/shell.html')
