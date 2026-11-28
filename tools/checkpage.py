"""Build docs/check.html and its five translations (1.1.7, D10): a checkup in any browser - phones, Macs, Chromebooks,
locked work PCs. Its own small generator on purpose: tools/site.sh and learnsite.py rewrite hundreds of committed pages.
python3 tools/checkpage.py  - then python3 tools/site_cache.py"""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "docs"
LANGS = ["en", "fr", "es", "ar", "bn", "zh"]

# every visible text, per language: [en, fr, es, ar, bn, zh]
S = {
 "title": ["Check my connection", "Vérifier ma connexion", "Revisar mi conexión", "افحص اتصالي", "আমার কানেকশন দেখুন", "检查我的网络"],
 "meta": ["A free connection checkup in any browser - phone, Mac, Chromebook or a locked work PC. Delay, jitter, speed, delay under load, DNS and IPv6, in plain words. Nothing installed, nothing stored.",
          "Un bilan de connexion gratuit dans n’importe quel navigateur - téléphone, Mac, Chromebook ou PC de bureau verrouillé. Délai, gigue, débit, délai sous charge, DNS et IPv6, en clair. Rien à installer, rien de conservé.",
          "Una revisión gratuita de tu conexión en cualquier navegador: móvil, Mac, Chromebook o un PC de trabajo bloqueado. Retardo, jitter, velocidad, retardo con carga, DNS e IPv6, en palabras sencillas. Nada que instalar, nada guardado.",
          "فحص مجاني للاتصال في أي متصفح - هاتف أو Mac أو Chromebook أو حاسوب عمل مقفل. التأخير والتذبذب والسرعة والتأخير تحت الحمل وDNS وIPv6 بكلمات بسيطة. لا تثبيت ولا تخزين.",
          "যেকোনো ব্রাউজারে বিনামূল্যে কানেকশন চেকআপ - ফোন, ম্যাক, ক্রোমবুক বা লক করা অফিস পিসি। ডিলে, জিটার, স্পিড, লোডে ডিলে, DNS ও IPv6, সহজ ভাষায়। কিছু ইনস্টল নয়, কিছু রাখা হয় না।",
          "在任何浏览器里免费检查网络——手机、Mac、Chromebook 或锁定的公司电脑。延迟、抖动、速度、负载下的延迟、DNS 和 IPv6，用大白话说明。无需安装，不保存任何数据。"],
 "lead": ["Your connection, measured from this browser. Nothing installed, nothing stored.", "Votre connexion, mesurée depuis ce navigateur. Rien à installer, rien de conservé.", "Tu conexión, medida desde este navegador. Nada que instalar, nada guardado.", "اتصالك مقيسًا من هذا المتصفح. لا تثبيت ولا تخزين.", "এই ব্রাউজার থেকে মাপা আপনার কানেকশন। কিছু ইনস্টল নয়, কিছু রাখা হয় না।", "从这个浏览器测量你的网络。无需安装，不保存任何数据。"],
 "start": ["Start the checkup", "Lancer le bilan", "Empezar la revisión", "ابدأ الفحص", "চেকআপ শুরু করুন", "开始检查"],
 "light": ["Light test (~5 MB)", "Test léger (~5 Mo)", "Prueba ligera (~5 MB)", "فحص خفيف (~5 ميغابايت)", "হালকা টেস্ট (~৫ MB)", "轻量测试（约 5 MB）"],
 "full": ["Full test (~50 MB)", "Test complet (~50 Mo)", "Prueba completa (~50 MB)", "فحص كامل (~50 ميغابايت)", "পুরো টেস্ট (~৫০ MB)", "完整测试（约 50 MB）"],
 "data": ["On mobile data? The light test uses about 5 MB, the full one about 50 MB.", "Sur données mobiles ? Le test léger utilise environ 5 Mo, le complet environ 50 Mo.", "¿Con datos móviles? La prueba ligera usa unos 5 MB y la completa unos 50 MB.", "على بيانات الجوال؟ الفحص الخفيف يستهلك نحو 5 ميغابايت والكامل نحو 50.", "মোবাইল ডেটায়? হালকা টেস্টে প্রায় ৫ MB, পুরোটায় প্রায় ৫০ MB লাগে।", "使用移动数据？轻量测试约 5 MB，完整测试约 50 MB。"],
 "again": ["Run it again", "Recommencer", "Repetir", "أعد الفحص", "আবার চালান", "再测一次"],
 "running": ["Measuring…", "Mesure en cours…", "Midiendo…", "جارٍ القياس…", "মাপা হচ্ছে…", "正在测量…"],
 "s_lat": ["Delay and jitter", "Délai et gigue", "Retardo y jitter", "التأخير والتذبذب", "ডিলে ও জিটার", "延迟和抖动"],
 "s_down": ["Download", "Réception", "Descarga", "التنزيل", "ডাউনলোড", "下载"],
 "s_up": ["Upload", "Envoi", "Subida", "الرفع", "আপলোড", "上传"],
 "s_load": ["Delay while busy", "Délai sous charge", "Retardo con carga", "التأخير أثناء الانشغال", "ব্যস্ত অবস্থায় ডিলে", "繁忙时的延迟"],
 "s_dns": ["Names (DNS)", "Noms (DNS)", "Nombres (DNS)", "الأسماء (DNS)", "নাম (DNS)", "域名解析（DNS）"],
 "s_v6": ["IPv6", "IPv6", "IPv6", "IPv6", "IPv6", "IPv6"],
 "s_video": ["Video", "Vidéo", "Vídeo", "الفيديو", "ভিডিও", "视频"],
 "good": ["Good", "Bon", "Bien", "جيد", "ভালো", "良好"], "ok": ["OK", "Correct", "Aceptable", "مقبول", "চলনসই", "一般"], "bad": ["Poor", "Faible", "Mala", "ضعيف", "খারাপ", "较差"],
 "lat_g": ["Fast replies and steady - calls and games feel instant.", "Réponses rapides et régulières - appels et jeux semblent instantanés.", "Respuestas rápidas y estables: llamadas y juegos se sienten al instante.", "ردود سريعة وثابتة - المكالمات والألعاب فورية.", "দ্রুত ও স্থির উত্তর - কল আর গেম তাৎক্ষণিক লাগে।", "响应快且稳定——通话和游戏几乎没有延迟。"],
 "lat_o": ["Fine for browsing and video; fast games may notice it.", "Bien pour naviguer et la vidéo ; les jeux rapides peuvent le sentir.", "Bien para navegar y vídeo; los juegos rápidos pueden notarlo.", "جيد للتصفح والفيديو؛ قد تلاحظه الألعاب السريعة.", "ব্রাউজিং ও ভিডিওর জন্য ঠিক আছে; দ্রুত গেমে টের পাওয়া যেতে পারে।", "浏览和视频没问题；节奏快的游戏可能会感觉到。"],
 "lat_b": ["Slow or uneven replies - calls may break up and games lag.", "Réponses lentes ou irrégulières - les appels peuvent couper et les jeux ramer.", "Respuestas lentas o irregulares: las llamadas pueden cortarse y los juegos ir con retraso.", "ردود بطيئة أو غير منتظمة - قد تتقطع المكالمات وتتأخر الألعاب.", "ধীর বা অসমান উত্তর - কল কেটে যেতে পারে, গেম ল্যাগ করবে।", "响应慢或不稳定——通话可能断续，游戏会卡。"],
 "load_g": ["Stays calm when the line is full - a call won't stutter while someone downloads.", "Reste calme quand la ligne est pleine - un appel ne saccadera pas pendant un téléchargement.", "Se mantiene estable con la línea llena: una llamada no se entrecortará mientras alguien descarga.", "يبقى هادئًا عند امتلاء الخط - لن تتقطع المكالمة أثناء تنزيل أحدهم.", "লাইন ভরা থাকলেও শান্ত - কেউ ডাউনলোড করলেও কল আটকাবে না।", "线路占满时依然平稳——有人下载时通话也不会卡。"],
 "load_o": ["Rises a little when the line is full - most calls cope.", "Augmente un peu quand la ligne est pleine - la plupart des appels tiennent.", "Sube un poco con la línea llena: la mayoría de las llamadas aguantan.", "يرتفع قليلًا عند امتلاء الخط - تتحمل معظم المكالمات.", "লাইন ভরলে একটু বাড়ে - বেশিরভাগ কল সামলে নেয়।", "线路占满时略有上升——大多数通话能应付。"],
 "load_b": ["Jumps when the line is full - calls stutter while someone downloads or uploads (bufferbloat).", "Bondit quand la ligne est pleine - les appels saccadent pendant un téléchargement (bufferbloat).", "Se dispara con la línea llena: las llamadas se entrecortan mientras alguien descarga (bufferbloat).", "يقفز عند امتلاء الخط - تتقطع المكالمات أثناء التنزيل أو الرفع (bufferbloat).", "লাইন ভরলে লাফিয়ে বাড়ে - কেউ ডাউনলোড বা আপলোড করলে কল আটকায় (বাফারব্লোট)।", "线路占满时猛增——有人下载或上传时通话会卡（缓冲膨胀）。"],
 "dns_g": ["Names answer quickly.", "Les noms répondent vite.", "Los nombres responden rápido.", "الأسماء تُجاب بسرعة.", "নাম দ্রুত উত্তর দেয়।", "域名解析很快。"],
 "dns_b": ["Name lookups are slow or failing - pages start late.", "Les recherches de noms sont lentes ou échouent - les pages démarrent tard.", "Las búsquedas de nombres son lentas o fallan: las páginas tardan en empezar.", "البحث عن الأسماء بطيء أو يفشل - تبدأ الصفحات متأخرة.", "নাম খোঁজা ধীর বা ব্যর্থ - পেজ দেরিতে শুরু হয়।", "域名解析慢或失败——网页打开慢。"],
 "v6_y": ["Yes - this connection has IPv6.", "Oui - cette connexion a l’IPv6.", "Sí: esta conexión tiene IPv6.", "نعم - هذا الاتصال يدعم IPv6.", "হ্যাঁ - এই কানেকশনে IPv6 আছে।", "是——这个网络支持 IPv6。"],
 "v6_n": ["No IPv6 here - everything still works over IPv4.", "Pas d’IPv6 ici - tout fonctionne quand même en IPv4.", "Aquí no hay IPv6: todo sigue funcionando por IPv4.", "لا يوجد IPv6 هنا - كل شيء يعمل عبر IPv4.", "এখানে IPv6 নেই - সব IPv4-এ চলে।", "这里没有 IPv6——一切仍通过 IPv4 正常工作。"],
 "vid_4k": ["4K streaming should be smooth.", "La 4K devrait être fluide.", "El 4K debería ir fluido.", "يجب أن يكون بث 4K سلسًا.", "৪K স্ট্রিমিং মসৃণ হওয়া উচিত।", "4K 视频应该流畅。"],
 "vid_hd": ["HD is fine; 4K may drop down.", "La HD passe ; la 4K peut baisser.", "El HD va bien; el 4K puede bajar.", "الدقة العالية جيدة؛ قد تنخفض 4K.", "HD ঠিক আছে; ৪K নেমে যেতে পারে।", "高清没问题；4K 可能会降画质。"],
 "vid_sd": ["Standard quality; HD may buffer.", "Qualité standard ; la HD peut charger.", "Calidad estándar; el HD puede cargar.", "جودة عادية؛ قد يتوقف HD للتحميل.", "সাধারণ মান; HD বাফার করতে পারে।", "标清；高清可能会缓冲。"],
 "vid_no": ["Video will struggle.", "La vidéo aura du mal.", "El vídeo tendrá problemas.", "سيعاني الفيديو.", "ভিডিও আটকাবে।", "视频会很吃力。"],
 "fail": ["couldn't measure from this browser", "impossible à mesurer depuis ce navigateur", "no se pudo medir desde este navegador", "تعذّر القياس من هذا المتصفح", "এই ব্রাউজার থেকে মাপা গেল না", "无法在此浏览器中测量"],
 "why": ["What is this?", "C’est quoi ?", "¿Qué es esto?", "ما هذا؟", "এটা কী?", "这是什么？"],
 "honest": ["Speed here is to the nearest test server, not your plan's number - and one run is a snapshot. Run it again when it feels slow, and compare.", "Le débit mesuré ici va jusqu’au serveur de test le plus proche, pas celui de votre forfait - et une mesure n’est qu’un instantané. Recommencez quand c’est lent et comparez.", "La velocidad aquí es hasta el servidor de prueba más cercano, no la de tu plan, y una medición es solo una foto. Repítela cuando vaya lento y compara.", "السرعة هنا إلى أقرب خادم اختبار وليست رقم باقتك - والقياس الواحد لقطة. أعده عندما يبطؤ وقارن.", "এখানের স্পিড কাছের টেস্ট সার্ভার পর্যন্ত, আপনার প্ল্যানের সংখ্যা নয় - আর একবারের মাপ একটা ঝলক। ধীর লাগলে আবার চালিয়ে তুলনা করুন।", "这里的速度是到最近测试服务器的速度，不是套餐标称值——一次测量只是快照。觉得慢时再测一次并比较。"],
 "cant": ["What a browser can't see: your Wi-Fi signal, your router and your device. That's the next step.", "Ce qu’un navigateur ne voit pas : votre signal Wi-Fi, votre routeur et votre appareil. C’est l’étape suivante.", "Lo que un navegador no ve: tu señal Wi-Fi, tu router y tu dispositivo. Ese es el siguiente paso.", "ما لا يراه المتصفح: إشارة Wi-Fi وجهاز التوجيه وجهازك. هذه هي الخطوة التالية.", "ব্রাউজার যা দেখতে পায় না: আপনার ওয়াই-ফাই সিগন্যাল, রাউটার আর ডিভাইস। সেটাই পরের ধাপ।", "浏览器看不到的：你的 Wi-Fi 信号、路由器和设备。这就是下一步。"],
 "next": ["Next step", "Étape suivante", "Siguiente paso", "الخطوة التالية", "পরের ধাপ", "下一步"],
 "win": ["On this Windows PC, AdeeboMeter sees all of it - Wi-Fi, router, provider and apps - and says whose fault it is.", "Sur ce PC Windows, AdeeboMeter voit tout - Wi-Fi, routeur, fournisseur et applications - et dit d’où vient le problème.", "En este PC con Windows, AdeeboMeter lo ve todo (Wi-Fi, router, proveedor y apps) y dice de quién es la culpa.", "على حاسوب Windows هذا يرى AdeeboMeter كل شيء - Wi-Fi وجهاز التوجيه والمزوّد والتطبيقات - ويقول أين الخلل.", "এই Windows পিসিতে AdeeboMeter সব দেখে - ওয়াই-ফাই, রাউটার, প্রোভাইডার আর অ্যাপ - আর বলে দোষ কার।", "在这台 Windows 电脑上，AdeeboMeter 能看到全部——Wi-Fi、路由器、运营商和应用——并告诉你问题出在哪。"],
 "getfull": ["Get the full picture", "Avoir la vue complète", "Ver el panorama completo", "احصل على الصورة الكاملة", "পুরো ছবি দেখুন", "查看完整情况"],
 "phone": ["Your Wi-Fi details live in Settings: on iPhone, Settings › Wi-Fi › the (i) next to your network; on Android, Settings › Network › Wi-Fi › your network. For the full picture, AdeeboMeter runs on a Windows PC.", "Les détails Wi-Fi sont dans les Réglages : sur iPhone, Réglages › Wi-Fi › le (i) à côté du réseau ; sur Android, Paramètres › Réseau › Wi-Fi › votre réseau. Pour la vue complète, AdeeboMeter tourne sur un PC Windows.", "Los detalles del Wi-Fi están en Ajustes: en iPhone, Ajustes › Wi-Fi › la (i) junto a tu red; en Android, Ajustes › Red › Wi-Fi › tu red. Para el panorama completo, AdeeboMeter funciona en un PC con Windows.", "تفاصيل Wi-Fi في الإعدادات: على iPhone الإعدادات › Wi-Fi › (i) بجانب شبكتك؛ وعلى Android الإعدادات › الشبكة › Wi-Fi › شبكتك. وللصورة الكاملة يعمل AdeeboMeter على حاسوب Windows.", "ওয়াই-ফাইয়ের খুঁটিনাটি সেটিংসে: iPhone-এ Settings › Wi-Fi › নেটওয়ার্কের পাশের (i); Android-এ Settings › Network › Wi-Fi › আপনার নেটওয়ার্ক। পুরো ছবির জন্য AdeeboMeter চলে Windows পিসিতে।", "Wi-Fi 详情在设置里：iPhone 上是 设置 › Wi-Fi › 网络旁的 (i)；Android 上是 设置 › 网络 › Wi-Fi › 你的网络。要看完整情况，请在 Windows 电脑上使用 AdeeboMeter。"],
 "mac_h": ["Your Mac already has good tools - here's how to read them", "Votre Mac a déjà de bons outils - voici comment les lire", "Tu Mac ya trae buenas herramientas: así se leen", "جهاز Mac لديك فيه أدوات جيدة - إليك كيف تقرؤها", "আপনার ম্যাকে আগেই ভালো টুল আছে - যেভাবে পড়বেন", "你的 Mac 自带好用的工具——看懂它们的方法"],
 "mac1": ["Hold Option and click the Wi-Fi icon: RSSI (signal) -50 to -65 dBm is good, below -75 is weak; Noise around -90; Channel shows 2.4 or 5 GHz.", "Maintenez Option et cliquez sur l’icône Wi-Fi : un RSSI (signal) de -50 à -65 dBm est bon, sous -75 c’est faible ; le bruit vers -90 ; Canal indique 2,4 ou 5 GHz.", "Mantén Opción y pulsa el icono Wi-Fi: un RSSI (señal) de -50 a -65 dBm es bueno, por debajo de -75 es débil; ruido cerca de -90; Canal indica 2,4 o 5 GHz.", "اضغط Option مع النقر على أيقونة Wi-Fi: RSSI (الإشارة) من -50 إلى -65 dBm جيد، وأقل من -75 ضعيف؛ الضوضاء نحو -90؛ القناة تبيّن 2.4 أو 5 GHz.", "Option চেপে ওয়াই-ফাই আইকনে ক্লিক করুন: RSSI (সিগন্যাল) -৫০ থেকে -৬৫ dBm ভালো, -৭৫-এর নিচে দুর্বল; Noise প্রায় -৯০; Channel বলে ২.৪ না ৫ GHz।", "按住 Option 点击 Wi-Fi 图标：RSSI（信号）-50 到 -65 dBm 为好，低于 -75 为弱；噪声约 -90；信道显示 2.4 或 5 GHz。"],
 "mac2": ["In Terminal, type networkQuality (macOS 12 or newer): speed both ways and Responsiveness - high RPM means calls stay smooth when the line is busy.", "Dans Terminal, tapez networkQuality (macOS 12 ou plus) : débit dans les deux sens et Responsiveness - un RPM élevé veut dire des appels fluides même ligne chargée.", "En Terminal, escribe networkQuality (macOS 12 o posterior): velocidad en ambos sentidos y Responsiveness; un RPM alto significa llamadas fluidas con la línea ocupada.", "في Terminal اكتب networkQuality (macOS 12 أو أحدث): السرعة في الاتجاهين وResponsiveness - رقم RPM مرتفع يعني مكالمات سلسة حتى مع انشغال الخط.", "Terminal-এ লিখুন networkQuality (macOS 12 বা নতুন): দুই দিকের স্পিড আর Responsiveness - বেশি RPM মানে লাইন ব্যস্ত থাকলেও কল মসৃণ।", "在终端输入 networkQuality（macOS 12 或更新）：双向速度和 Responsiveness——RPM 越高，线路繁忙时通话越流畅。"],
 "mac3": ["Wi-Fi keeps dropping? Option-click the Wi-Fi icon › Open Wireless Diagnostics - it records what happens and names the cause.", "Le Wi-Fi décroche ? Option-clic sur l’icône Wi-Fi › Ouvrir Diagnostics sans fil - il enregistre et nomme la cause.", "¿Se corta el Wi-Fi? Opción-clic en el icono Wi-Fi › Abrir Diagnóstico inalámbrico: registra lo que pasa y nombra la causa.", "ينقطع Wi-Fi؟ انقر مع Option على أيقونة Wi-Fi › فتح تشخيصات اللاسلكي - يسجّل ما يحدث ويسمّي السبب.", "ওয়াই-ফাই বারবার কাটে? Option চেপে আইকনে ক্লিক › Open Wireless Diagnostics - কী ঘটছে রেকর্ড করে কারণ বলে।", "Wi-Fi 老是断？按住 Option 点击 Wi-Fi 图标 › 打开无线诊断——它会记录并指出原因。"],
 "cros": ["On a Chromebook, Wi-Fi details are in Settings › Network › Wi-Fi › your network. On Linux, nmcli dev wifi shows the networks and their signal.", "Sur Chromebook, les détails Wi-Fi sont dans Paramètres › Réseau › Wi-Fi › votre réseau. Sous Linux, nmcli dev wifi montre les réseaux et leur signal.", "En un Chromebook, los detalles del Wi-Fi están en Ajustes › Red › Wi-Fi › tu red. En Linux, nmcli dev wifi muestra las redes y su señal.", "على Chromebook تفاصيل Wi-Fi في الإعدادات › الشبكة › Wi-Fi › شبكتك. وعلى Linux يعرض nmcli dev wifi الشبكات وقوة إشارتها.", "ক্রোমবুকে ওয়াই-ফাইয়ের খুঁটিনাটি Settings › Network › Wi-Fi › আপনার নেটওয়ার্ক-এ। লিনাক্সে nmcli dev wifi নেটওয়ার্ক আর সিগন্যাল দেখায়।", "在 Chromebook 上，Wi-Fi 详情在 设置 › 网络 › Wi-Fi › 你的网络。在 Linux 上，nmcli dev wifi 会显示网络及其信号。"],
 "sim": ["Measured with web requests to Cloudflare and Google, not a game or a ping - a browser can't do those.", "Mesuré par des requêtes web vers Cloudflare et Google, pas un jeu ni un ping - un navigateur ne peut pas les faire.", "Medido con peticiones web a Cloudflare y Google, no un juego ni un ping: un navegador no puede hacerlos.", "مقيس بطلبات ويب إلى Cloudflare وGoogle، لا لعبة ولا ping - المتصفح لا يستطيع ذلك.", "Cloudflare ও Google-এ ওয়েব রিকোয়েস্ট দিয়ে মাপা, গেম বা পিং নয় - ব্রাউজার সেগুলো পারে না।", "用访问 Cloudflare 和 Google 的网页请求测量，不是游戏或 ping——浏览器做不到那些。"],
}
LEARN = {"lat": "speed-vs-delay.html", "load": "streaming-vs-calls.html", "dns": "dns.html", "v6": "ipv6.html", "video": "stream-4k.html", "down": "speed-vs-delay.html", "up": "speed-vs-delay.html"}

PAGE = r'''<!doctype html>
<html lang="{lang}"{dir} data-language-route="/check.html" data-language-variants="en bn fr es ar zh">
<head>
<!--theme--><script>try{{if(window.top===window.self){{var t=localStorage.getItem("am-theme");if(!t||t==="light"||t==="dark")document.documentElement.setAttribute("data-theme",t||"dark")}}}}catch(e){{}}</script><!--/theme-->
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>{title} | AdeeboMeter</title>
<meta name="description" content="{meta}">
<link rel="canonical" href="https://adeebometer.com{pre}/check.html">
<link rel="icon" href="/favicon.svg" type="image/svg+xml">
<style>
:root{{--ink:#1B2330;--sub:#4A5568;--line:#E3E8EF;--page:#FBFCFE;--card:#FFFFFF;--blue:#1F6FEB;--green:#2DA44E;--red:#CF222E;--amber:#9A6700;--tint:#EEF4FF;--c-check:#1A8A6E}}
@media (prefers-color-scheme: dark){{:root:not([data-theme="light"]){{--ink:#E6EAF0;--sub:#93A6C6;--line:#22355A;--page:#0B1426;--card:#121F38;--blue:#58A6FF;--green:#3FB950;--red:#F85149;--amber:#D29922;--tint:#182844;--c-check:#4FD1A8}}}}
:root[data-theme="dark"]{{--ink:#E6EAF0;--sub:#93A6C6;--line:#22355A;--page:#0B1426;--card:#121F38;--blue:#58A6FF;--green:#3FB950;--red:#F85149;--amber:#D29922;--tint:#182844;--c-check:#4FD1A8}}
*{{box-sizing:border-box}}
body{{margin:0;background:var(--page);color:var(--ink);font:16px/1.55 "Segoe UI",system-ui,-apple-system,sans-serif;-webkit-text-size-adjust:100%}}
main{{max-width:880px;margin:0 auto;padding:20px 16px calc(80px + env(safe-area-inset-bottom))}}
a{{color:var(--blue)}}
.cards{{display:grid;grid-template-columns:repeat(2,1fr);gap:12px}}
.card{{background:var(--card);border:1px solid var(--line);border-radius:14px;padding:14px 16px;min-width:0}}.card.wide{{grid-column:1/-1}}
.card h2{{font-size:16px;margin:0 0 4px;color:var(--sub);font-weight:600}}
.big{{font-size:34px;font-weight:700;line-height:1.1;font-variant-numeric:tabular-nums}}
.big small{{font-size:16px;font-weight:600;color:var(--sub)}}
.say{{margin:6px 0 0;font-size:15px}}.mut{{color:var(--sub);font-size:14px}}
.lv{{display:inline-block;font-size:13px;font-weight:700;border-radius:999px;padding:1px 9px;margin-inline-start:8px;vertical-align:middle}}
.lv.g{{background:rgba(45,164,78,.15);color:var(--green)}}.lv.o{{background:rgba(154,103,0,.15);color:var(--amber)}}.lv.b{{background:rgba(207,34,46,.14);color:var(--red)}}
.go{{display:flex;flex-wrap:wrap;gap:10px;margin:6px 0 18px}}
.btn{{font:inherit;font-weight:700;color:#fff;background:var(--blue);border:0;border-radius:999px;padding:12px 22px;cursor:pointer;min-height:46px;transition:transform 140ms cubic-bezier(.23,1,.32,1)}}
.btn.line{{background:transparent;color:var(--blue);border:1px solid var(--line)}}.btn:active{{transform:scale(.97)}}.btn:disabled{{opacity:.55;cursor:default}}
.bar{{height:6px;border-radius:3px;background:var(--line);overflow:hidden;margin:10px 0 0}}.bar i{{display:block;height:6px;width:0;background:var(--c-check);transition:width 300ms ease-out}}
.next ul{{margin:8px 0 0;padding-inline-start:20px}}.next li{{margin:6px 0}}code{{font-family:ui-monospace,Menlo,Consolas,monospace;background:var(--tint);border-radius:6px;padding:1px 6px}}
.cta{{display:inline-block;background:var(--blue);color:#fff;text-decoration:none;font-weight:700;border-radius:999px;padding:11px 20px;margin-top:12px}}
@media (max-width:640px){{.cards{{grid-template-columns:1fr}}.big{{font-size:30px}}}}
</style>
<link rel="stylesheet" href="/ui.css">
<link rel="stylesheet" href="/sitebar.css">
<!--pwa--><link rel="manifest" href="/manifest.webmanifest"><meta name="theme-color" content="#1155E0"><link rel="apple-touch-icon" href="/img/app/apple-touch-icon.png">
{alts}
</head>
<body data-sec="check">
<!--sitebar--><nav class="sitebar" id="sitebar" aria-label="Site"><a class="sb-brand" href="/"><img src="/img/logo.svg" width="24" height="24" alt=""><span>Adeebo<b>Meter</b></span></a></nav><script src="/site.js"></script><!--/sitebar-->
<main>
<header class="ui-head"><span class="ui-chip" style="--k:var(--c-check)" data-ic="signal" data-sz="28"></span><div><h1>{title}</h1><p>{lead}</p></div></header>
<div class="go" id="go"></div>
<p class="mut" id="datanote" hidden>{data}</p>
<div class="bar" id="bar" hidden><i></i></div>
<div class="cards" id="res" hidden></div>
<p class="mut" id="honest" hidden>{honest}</p>
<section class="card wide next" id="next" hidden></section>
<p class="mut">{sim}</p>
</main>
<script>
(function () {{
  "use strict";
  var T = {strings}, PRE = "{pre}", LEARN = {learn};
  var API = "https://adeebometer-live.orkoadeeb.workers.dev", CF = "https://speed.cloudflare.com";
  var $ = function (id) {{ return document.getElementById(id); }}, ua = navigator.userAgent;
  var PHONE = (matchMedia("(pointer: coarse)").matches && Math.min(screen.width, screen.height) < 900) || /Android|iPhone|iPad|iPod|Mobile/i.test(ua);
  var dev = /iPhone|iPad|iPod|Android/i.test(ua) || (/Macintosh/.test(ua) && navigator.maxTouchPoints > 1) ? "phone" : /Macintosh|Mac OS X/.test(ua) ? "mac" : /CrOS|Linux/.test(ua) && !/Android/.test(ua) ? "cros" : "win";
  var cell = navigator.connection && (navigator.connection.type === "cellular" || navigator.connection.saveData);
  function esc(s) {{ return String(s).replace(/[&<>"]/g, function (c) {{ return {{ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }}[c]; }}); }}
  function wait(ms) {{ return new Promise(function (r) {{ setTimeout(r, ms); }}); }}
  function timed(url, opt, ms) {{ var c = typeof AbortController === "function" ? new AbortController() : null; if (c) setTimeout(function () {{ c.abort(); }}, ms || 15000); opt = opt || {{}}; if (c) opt.signal = c.signal; opt.cache = "no-store"; return fetch(url, opt); }}
  function ping() {{ var s = performance.now(); return timed(API + "/ping?" + Math.random(), {{}}, 5000).then(function (r) {{ if (!r.ok) throw 0; return r.text(); }}).then(function () {{ return performance.now() - s; }}, function () {{ return NaN; }}); }}
  function pings(n) {{ var out = [], p = Promise.resolve(); for (var i = 0; i < n; i++) p = p.then(ping).then(function (v) {{ if (!isNaN(v)) out.push(v); }}); return p.then(function () {{ return out; }}); }}
  function med(a) {{ var b = a.slice().sort(function (x, y) {{ return x - y; }}); return b.length ? b[Math.floor(b.length / 2)] : NaN; }}
  function down(bytes, n) {{ var s = performance.now(), got = 0, jobs = []; for (var i = 0; i < n; i++) jobs.push(timed(CF + "/__down?bytes=" + bytes + "&r=" + Math.random(), {{}}, 30000).then(function (r) {{ if (!r.ok) throw 0; return r.arrayBuffer(); }}).then(function (b) {{ got += b.byteLength; }}));
    return Promise.all(jobs).then(function () {{ return got * 8 / ((performance.now() - s) / 1000) / 1e6; }}, function () {{ return NaN; }}); }}
  function up(bytes, n) {{ var s = performance.now(), body = new Uint8Array(bytes), jobs = []; for (var i = 0; i < n; i++) jobs.push(timed(CF + "/__up?r=" + Math.random(), {{ method: "POST", body: body }}, 30000).then(function (r) {{ if (!r.ok) throw 0; }}));
    return Promise.all(jobs).then(function () {{ return bytes * n * 8 / ((performance.now() - s) / 1000) / 1e6; }}, function () {{ return NaN; }}); }}
  function dns() {{ var t = [], ok = 0, p = Promise.resolve();
    for (var i = 0; i < 3; i++) p = p.then(function () {{ var s = performance.now(); return timed("https://dns.google/resolve?name=" + Math.random().toString(36).slice(2, 10) + ".adeebometer.com&type=A", {{}}, 6000).then(function (r) {{ return r.json(); }}).then(function (j) {{ t.push(performance.now() - s); if (j && typeof j.Status === "number") ok++; }}, function () {{}}); }});
    return p.then(function () {{ return {{ ms: med(t), ok: ok }}; }}); }}
  function v6() {{ return timed("https://api6.ipify.org?format=json", {{}}, 5000).then(function (r) {{ return r.ok; }}, function () {{ return false; }}); }}
  function card(k, title, big, lv, say) {{ return '<section class="card"><h2>' + esc(title) + (lv ? '<span class="lv ' + lv + '">' + esc(lv === "g" ? T.good : lv === "o" ? T.ok : T.bad) + "</span>" : "") + '</h2><div class="big">' + big + '</div><p class="say">' + esc(say) + '</p><p class="mut"><a href="' + PRE + "/learn/" + LEARN[k] + '">' + esc(T.why) + "</a></p></section>"; }}
  function n0(v) {{ return isNaN(v) ? "—" : (v >= 100 ? Math.round(v) : v.toFixed(1)).toLocaleString(); }}
  function next() {{
    var h = '<h2 style="color:var(--ink)">' + esc(T.next) + "</h2><p>" + esc(T.cant) + "</p>";
    if (dev === "mac") h += "<p><b>" + esc(T.mac_h) + "</b></p><ul><li>" + esc(T.mac1) + "</li><li>" + esc(T.mac2).replace("networkQuality", "<code>networkQuality</code>") + "</li><li>" + esc(T.mac3) + "</li></ul>";
    else if (dev === "phone") h += "<p>" + esc(T.phone) + '</p><a class="cta dl" data-download href="https://github.com/adeeb111/AdeeboMeter/releases/latest">' + esc(T.getfull) + "</a>";
    else if (dev === "cros") h += "<p>" + esc(T.cros).replace("nmcli dev wifi", "<code>nmcli dev wifi</code>") + "</p>";
    else h += "<p>" + esc(T.win) + '</p><a class="cta dl" data-download href="https://github.com/adeeb111/AdeeboMeter/releases/latest">' + esc(T.getfull) + "</a>";
    $("next").innerHTML = h; $("next").hidden = false;
  }}
  var busy = false;
  function run(size) {{
    if (busy) return; busy = true; [].forEach.call(document.querySelectorAll("#go button"), function (b) {{ b.disabled = true; }});
    var bar = $("bar"), fill = bar.firstChild, R = {{}}; bar.hidden = false; fill.style.width = "4%"; $("res").hidden = true;
    var step = function (p) {{ fill.style.width = p + "%"; }};
    var big = size === "full", db = big ? 10e6 : 1e6, ub = big ? 2e6 : 25e4;
    pings(8).then(function (t) {{ R.lat = med(t); R.jit = t.length > 1 ? t.slice(1).reduce(function (a, v, i) {{ return a + Math.abs(v - t[i]); }}, 0) / (t.length - 1) : NaN; step(20); return down(db, 4); }})
      .then(function (d) {{ R.down = d; step(45); return up(ub, 2); }})
      .then(function (u) {{ R.up = u; step(60);
        // delay while busy: the same requests while a download fills the line
        var load = down(db, 4), lp = wait(300).then(function () {{ return pings(6); }}); return Promise.all([load, lp]).then(function (x) {{ R.loaded = med(x[1]); }}); }})
      .then(function () {{ step(80); return dns(); }})
      .then(function (d) {{ R.dns = d; step(90); return v6(); }})
      .then(function (v) {{ R.v6 = v; step(100); show(R); }})
      .then(function () {{ busy = false; bar.hidden = true; buttons(true); }}, function () {{ busy = false; bar.hidden = true; buttons(true); }});
  }}
  function show(R) {{
    var h = "", f = esc(T.fail);
    var latLv = isNaN(R.lat) ? "" : R.lat < 40 && R.jit < 10 ? "g" : R.lat < 100 && R.jit < 30 ? "o" : "b";
    h += card("lat", T.s_lat, isNaN(R.lat) ? "—" : Math.round(R.lat) + " <small>ms</small> · " + Math.round(R.jit) + " <small>ms</small>", latLv, isNaN(R.lat) ? T.fail : T["lat_" + latLv]);
    var rise = R.loaded - R.lat, ldLv = isNaN(rise) ? "" : rise < 30 ? "g" : rise < 100 ? "o" : "b";
    h += card("load", T.s_load, isNaN(rise) ? "—" : "+" + Math.max(0, Math.round(rise)) + " <small>ms</small>", ldLv, isNaN(rise) ? T.fail : T["load_" + ldLv]);
    h += card("down", T.s_down, n0(R.down) + " <small>Mb/s</small>", "", isNaN(R.down) ? T.fail : "");
    h += card("up", T.s_up, n0(R.up) + " <small>Mb/s</small>", "", isNaN(R.up) ? T.fail : "");
    var vd = isNaN(R.down) ? "" : R.down >= 25 ? "4k" : R.down >= 8 ? "hd" : R.down >= 3 ? "sd" : "no";
    h += card("video", T.s_video, vd === "" ? "—" : vd === "4k" ? "4K" : vd === "hd" ? "HD" : vd === "sd" ? "SD" : "—", vd === "" ? "" : vd === "4k" || vd === "hd" ? "g" : vd === "sd" ? "o" : "b", vd === "" ? T.fail : T["vid_" + vd]);
    var offline = isNaN(R.lat) && isNaN(R.down);   // nothing answered at all: no verdicts, just "couldn't measure"
    var dnsLv = isNaN(R.dns.ms) || offline ? "" : !R.dns.ok ? "b" : R.dns.ms < 80 ? "g" : R.dns.ms < 250 ? "o" : "b";
    h += card("dns", T.s_dns, isNaN(R.dns.ms) ? "—" : Math.round(R.dns.ms) + " <small>ms</small>", dnsLv, dnsLv === "" ? T.fail : dnsLv === "b" ? T.dns_b : T.dns_g);
    h += card("v6", T.s_v6, R.v6 ? "✓" : "—", R.v6 ? "g" : "", R.v6 ? T.v6_y : offline ? T.fail : T.v6_n);
    $("res").innerHTML = h; $("res").hidden = false; $("honest").hidden = false; next();
  }}
  function buttons(again) {{
    var g = $("go"); g.textContent = "";
    var mk = function (label, size, line) {{ var b = document.createElement("button"); b.className = "btn" + (line ? " line" : ""); b.textContent = label; b.onclick = function () {{ run(size); }}; g.appendChild(b); }};
    if (PHONE || cell) {{ mk(T.light, "light"); mk(T.full, "full", true); $("datanote").hidden = false; }}
    else mk(again ? T.again : T.start, "full");
  }}
  buttons(false);
}})();
</script>
<script src="/ui.js"></script>
</body>
</html>
'''

def main():
    alts = "\n".join('<link rel="alternate" hreflang="{0}" href="https://adeebometer.com{1}/check.html">'.format({"zh": "zh-Hans"}.get(l, l), "" if l == "en" else "/" + l) for l in LANGS) + \
        '\n<link rel="alternate" hreflang="x-default" href="https://adeebometer.com/check.html">'
    for i, l in enumerate(LANGS):
        t = {k: v[i] for k, v in S.items()}
        pre = "" if l == "en" else "/" + l
        html = PAGE.format(lang=l, dir=' dir="rtl"' if l == "ar" else "", title=t["title"], meta=t["meta"].replace('"', "&quot;"), lead=t["lead"], data=t["data"], honest=t["honest"], sim=t["sim"],
                           pre=pre, alts=alts, strings=json.dumps(t, ensure_ascii=False), learn=json.dumps(LEARN))
        out = ROOT / ("check.html" if l == "en" else l + "/check.html")
        out.write_text(html, encoding="utf-8", newline="\n")
        print(out.relative_to(ROOT.parent), len(html))

if __name__ == "__main__":
    main()
