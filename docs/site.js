/* The one site bar and the download window, for every page except the home page's own header (24 Nov, Adeeb: "synchronize
   everything" - before this the blog, the guides, the feature pages, Learn / Games / Live, privacy and the Hall of Fame each had
   their own header, and some had no way back but the logo).
   - The bar: the page has <nav class="sitebar" id="sitebar"> right after <body>, then this script (not deferred), so the bar is
     filled before the page paints - no jump. Same brand, same links, same Download on every page, in the page's language.
     Inside the phone app (the page in a frame) the bar hides: the app has its own.
   - Download: every Download button opens one small window - Microsoft Store (updates itself) or the direct download -
     instead of sending people to GitHub. Ctrl-click / middle-click still follow the link. */
(function () {
  var html = document.documentElement;
  if (self !== top) html.classList.add('inapp');
  var m = /^\/(fr|es|ar|bn|zh)\//.exec(location.pathname), lang = m ? m[1] : 'en', pre = m ? '/' + m[1] : '';
  var T = {
    zh: {"theme":"深色或浅色","home":"主页","site":"网站","learn":"学习","blog":"博客","games":"游戏","live":"实时信息","more":"更多","files":"文件","phone":"手机应用","gaming":"游戏","streaming":"流媒体","trading":"交易","isp":"运营商报告","hof":"名人堂","privacy":"隐私","donate":"捐赠","download":"下载","dlTitle":"下载 AdeeboMeter","dlSub":"免费支持 Windows 10 和 11，无需管理员权限，不收集个人信息。","store":"Microsoft Store","storeSub":"已签名，由 Windows 保持更新。","direct":"直接下载","directSub":"下载 ZIP，解压运行，即可选择自行安装。","directVer":"版本 {v}","all":"全部版本与更新说明","phoneLink":"使用手机？打开手机应用","close":"关闭"},
    en: { theme: 'Dark or light', home: 'Home', site: 'Site', learn: 'Learn', blog: 'Blog', games: 'Games', live: 'Live', more: 'More', files: 'Files', phone: 'Phone app', gaming: 'Gaming', streaming: 'Streaming', trading: 'Trading', isp: 'ISP report', hof: 'Hall of Fame', privacy: 'Privacy', donate: 'Donate', download: 'Download',
      dlTitle: 'Download AdeeboMeter', dlSub: 'Free for Windows 10 and 11. No admin rights needed, nothing about you collected.', store: 'Microsoft Store', storeSub: 'Signed and kept up to date by Windows.', direct: 'Direct download', directSub: 'A zip with the app - unzip and run, it offers to install itself.', directVer: 'Version {v}', all: 'Every version and its notes', phoneLink: 'On a phone? Open the phone app', close: 'Close' },
    fr: { theme: 'Sombre ou clair', home: 'Accueil', site: 'Site', learn: 'Apprendre', blog: 'Blogue', games: 'Jeux', live: 'En direct', more: 'Plus', files: 'Fichiers', phone: 'App mobile', gaming: 'Jeux vidéo', streaming: 'Streaming', trading: 'Trading', isp: 'Rapport pour le FAI', hof: 'Temple de la renommée', privacy: 'Confidentialité', donate: 'Faire un don', download: 'Télécharger',
      dlTitle: 'Télécharger AdeeboMeter', dlSub: 'Gratuit pour Windows 10 et 11. Aucun droit d’administrateur, rien sur vous n’est collecté.', store: 'Microsoft Store', storeSub: 'Signé et tenu à jour par Windows.', direct: 'Téléchargement direct', directSub: 'Un zip avec l’app : décompressez et lancez, elle propose de s’installer.', directVer: 'Version {v}', all: 'Toutes les versions et leurs notes', phoneLink: 'Sur un téléphone ? Ouvrir l’app mobile', close: 'Fermer' },
    es: { theme: 'Oscuro o claro', home: 'Inicio', site: 'Sitio', learn: 'Aprender', blog: 'Blog', games: 'Juegos', live: 'En vivo', more: 'Más', files: 'Archivos', phone: 'App móvil', gaming: 'Videojuegos', streaming: 'Streaming', trading: 'Trading', isp: 'Informe al proveedor', hof: 'Salón de la fama', privacy: 'Privacidad', donate: 'Donar', download: 'Descargar',
      dlTitle: 'Descargar AdeeboMeter', dlSub: 'Gratis para Windows 10 y 11. Sin permisos de administrador y sin recopilar nada sobre ti.', store: 'Microsoft Store', storeSub: 'Firmada y actualizada por Windows.', direct: 'Descarga directa', directSub: 'Un zip con la app: descomprímelo y ábrela, te ofrece instalarse.', directVer: 'Versión {v}', all: 'Todas las versiones y sus notas', phoneLink: '¿En un teléfono? Abre la app móvil', close: 'Cerrar' },
    ar: { theme: 'داكن أو فاتح', home: 'الرئيسية', site: 'الموقع', learn: 'تعلّم', blog: 'المدونة', games: 'الألعاب', live: 'مباشر', more: 'المزيد', files: 'الملفات', phone: 'تطبيق الهاتف', gaming: 'الألعاب الإلكترونية', streaming: 'البث', trading: 'التداول', isp: 'تقرير لمزوّد الخدمة', hof: 'قاعة المشاهير', privacy: 'الخصوصية', donate: 'تبرّع', download: 'تنزيل',
      dlTitle: 'تنزيل AdeeboMeter', dlSub: 'مجاني لنظامي Windows 10 و11. لا يحتاج صلاحيات المسؤول، ولا يجمع أي شيء عنك.', store: 'Microsoft Store', storeSub: 'موقّع ويحدّثه Windows تلقائيًا.', direct: 'تنزيل مباشر', directSub: 'ملف zip فيه التطبيق: فكّ الضغط وشغّله، وسيعرض تثبيت نفسه.', directVer: 'الإصدار {v}', all: 'كل الإصدارات وملاحظاتها', phoneLink: 'على الهاتف؟ افتح تطبيق الهاتف', close: 'إغلاق' },
    bn: { theme: 'ডার্ক বা লাইট', home: 'হোম', site: 'সাইট', learn: 'শিখুন', blog: 'ব্লগ', games: 'গেম', live: 'লাইভ', more: 'আরও', files: 'ফাইল', phone: 'ফোন অ্যাপ', gaming: 'গেমিং', streaming: 'স্ট্রিমিং', trading: 'ট্রেডিং', isp: 'ISP রিপোর্ট', hof: 'হল অব ফেম', privacy: 'গোপনীয়তা', donate: 'দান করুন', download: 'ডাউনলোড',
      dlTitle: 'AdeeboMeter ডাউনলোড', dlSub: 'Windows 10 ও 11-এর জন্য বিনামূল্যে। অ্যাডমিন অনুমতি লাগে না, আপনার কিছুই সংগ্রহ করা হয় না।', store: 'Microsoft Store', storeSub: 'সাইন করা, আর Windows নিজেই হালনাগাদ রাখে।', direct: 'সরাসরি ডাউনলোড', directSub: 'অ্যাপসহ একটি zip: আনজিপ করে চালান, নিজেই ইনস্টল করার প্রস্তাব দেবে।', directVer: 'সংস্করণ {v}', all: 'সব সংস্করণ ও সেগুলোর নোট', phoneLink: 'ফোনে আছেন? ফোন অ্যাপ খুলুন', close: 'বন্ধ করুন' }
  }[lang];
  var STORE = 'https://apps.microsoft.com/detail/9PFGXJPL9CLJ?mode=direct', RELEASES = 'https://github.com/adeeb111/AdeeboMeter/releases/latest';
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  var path = location.pathname.replace(/index\.html$/, '');
  function cur(href) { var h = href.replace(/index\.html$/, ''); return h === path || (h.length > pre.length + 1 && h.slice(-1) === '/' && path.indexOf(h) === 0) ? ' aria-current="page"' : ''; }
  function a(href, label, cls) { return '<a' + (cls ? ' class="' + cls + '"' : '') + ' href="' + href + '"' + cur(href) + '>' + esc(label) + '</a>'; }
  var gaming = pre + '/gaming.html', main = [[pre + '/', T.home], [pre + '/learn/', T.learn], [pre + '/blog/', T.blog], [pre + '/games.html', T.games], [pre + '/live.html', T.live]];
  var more = [['/lab.html', 'AdeeboLab Playground'], [pre + '/files.html', T.files], [pre + '/app.html', T.phone], [gaming, T.gaming], [pre + '/streaming.html', T.streaming], [pre + '/trading.html', T.trading], [pre + '/isp-report.html', T.isp], [pre + '/hall-of-fame.html', T.hof], [pre + '/privacy.html', T.privacy], ['/img/poster.png', {"en":{"poster":"Poster","line":"Everything AdeeboMeter does, on one page.","see":"See the poster","get":"Download the poster","alt":"The AdeeboMeter poster: what it does - the corner widget, What's wrong?, the ISP report, 232 tools, AdeeboShell, AdeeboLab and more"},"fr":{"poster":"Affiche","line":"Tout ce que fait AdeeboMeter, sur une page.","see":"Voir l’affiche","get":"Télécharger l’affiche","alt":"L’affiche d’AdeeboMeter : ce qu’il fait - le widget, Qu’est-ce qui ne va pas ?, le rapport pour le fournisseur, 232 outils, AdeeboShell, AdeeboLab et plus"},"es":{"poster":"Póster","line":"Todo lo que hace AdeeboMeter, en una página.","see":"Ver el póster","get":"Descargar el póster","alt":"El póster de AdeeboMeter: lo que hace - el widget, ¿Qué pasa?, el informe para el proveedor, 232 herramientas, AdeeboShell, AdeeboLab y más"},"ar":{"poster":"الملصق","line":"كل ما يفعله AdeeboMeter في صفحة واحدة.","see":"شاهد الملصق","get":"نزّل الملصق","alt":"ملصق AdeeboMeter: ما يفعله - أداة الزاوية، ما الخلل؟، تقرير مزوّد الخدمة، 232 أداة، AdeeboShell، AdeeboLab والمزيد"},"bn":{"poster":"পোস্টার","line":"AdeeboMeter যা যা করে, এক পাতায়।","see":"পোস্টার দেখুন","get":"পোস্টার ডাউনলোড করুন","alt":"AdeeboMeter-এর পোস্টার: এটি কী করে - কোণের উইজেট, What's wrong?, ISP রিপোর্ট, ২৩২টি টুল, AdeeboShell, AdeeboLab ও আরও"},"zh":{"poster":"海报","line":"一张图了解 AdeeboMeter 的全部功能。","see":"查看海报","get":"下载海报","alt":"AdeeboMeter 功能海报：角落小组件、问题诊断、运营商报告、232 个工具、AdeeboShell、AdeeboLab 等"}}[lang].poster]];
  var bar = document.getElementById('sitebar');
  if (bar && !bar.getAttribute('data-filled')) {
    bar.setAttribute('data-filled', '1'); bar.setAttribute('aria-label', T.site);
    var moreHere = more.some(function (x) { return cur(x[0]); });
    bar.innerHTML = '<a class="sb-brand" href="' + pre + '/"><img src="/img/logo.svg" width="24" height="24" alt=""><span>Adeebo<b>Meter</b></span></a>' +
      '<div class="sb-links">' + main.map(function (x) { return a(x[0], x[1], 'sb-main'); }).join('') +
      '<details class="sb-more"><summary' + (moreHere ? ' aria-current="true"' : '') + '><span>' + esc(T.more) + '</span><svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true"><path d="M6 9l6 6 6-6" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg></summary><div class="sb-pop">' +
      main.map(function (x) { return a(x[0], x[1], 'sb-narrow'); }).join('') + more.map(function (x) { return a(x[0], x[1]); }).join('') + '</div></details>' +
      '<a class="sb-heart" href="' + pre + '/#donate" title="' + esc(T.donate) + '" aria-label="' + esc(T.donate) + '"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z" fill="currentColor"/></svg></a>' +
      '<button class="sb-theme" type="button" aria-label="' + esc(T.theme) + '" title="' + esc(T.theme) + '"><svg class="sb-sun" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><circle cx="12" cy="12" r="4"/><path d="M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4"/></svg><svg class="sb-moon" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" fill="currentColor"><path d="M20.5 14.2A8.5 8.5 0 0 1 9.8 3.5a8.5 8.5 0 1 0 10.7 10.7z"/></svg></button>' +
      '<a class="sb-dl" data-download href="' + RELEASES + '">' + esc(T.download) + '</a></div>';
    // day / night, the same switch as the home page's: the choice is kept (am-theme) and every page reads it
    var html = document.documentElement, tb = bar.querySelector('.sb-theme');
    function isDark() { var t = html.getAttribute('data-theme'); return t ? t === 'dark' : matchMedia('(prefers-color-scheme: dark)').matches; }
    tb.setAttribute('aria-pressed', isDark() ? 'true' : 'false');
    tb.addEventListener('click', function () { var d = isDark() ? 'light' : 'dark'; html.setAttribute('data-theme', d); try { localStorage.setItem('am-theme', d); } catch (e) {} tb.setAttribute('aria-pressed', d === 'dark' ? 'true' : 'false'); });
    // a menu closes on Esc, on a click outside, and when another opens - like the home page's More
    document.addEventListener('click', function (e) { var d = bar.querySelector('details[open]'); if (d && !d.contains(e.target)) d.removeAttribute('open'); });
    document.addEventListener('keydown', function (e) { var d = bar.querySelector('details[open]'); if (e.key === 'Escape' && d) { d.removeAttribute('open'); d.querySelector('summary').focus(); } });
  }

  // ---- the download window
  var ver = '1.1.6', zip = 'https://github.com/adeeb111/AdeeboMeter/releases/download/v1.1.6/AdeeboMeter-1.1.6-win64.zip';   // 24 Nov: always a direct file, even before GitHub answers (it used to fall back to the release page); the newest release replaces it when it answers
  // hunters14 SITE-L3: 10 s at most, and a failed look is tried again on the next open (it used to give up for good)
  function latest() {
    if (latest.asked) return; latest.asked = true;
    try {
      var ctl = typeof AbortController === 'function' ? new AbortController() : null; if (ctl) setTimeout(function () { ctl.abort(); }, 10000);
      fetch('https://api.github.com/repos/adeeb111/AdeeboMeter/releases/latest', { headers: { Accept: 'application/vnd.github+json' }, signal: ctl ? ctl.signal : undefined })
        .then(function (r) { return r.ok ? r.json() : null; })
        .then(function (rel) {
          if (!rel || !rel.assets) return;
          var z = rel.assets.filter(function (x) { return /-win64\.zip$/.test(x.name); })[0]; if (!z) return;
          zip = z.browser_download_url; ver = (rel.tag_name || '').replace(/^v/, '');
          var d = document.getElementById('am-dl'); if (d) fill(d);
        }).catch(function () { latest.asked = false; });
    } catch (e) { latest.asked = false; }
  }
  function fill(d) {
    var link = d.querySelector('.dl-direct'); link.href = zip || RELEASES;
    var v = d.querySelector('.dl-ver'); v.textContent = ver ? T.directVer.replace('{v}', ver) : ''; v.hidden = !ver;
  }
  function dialog() {
    var d = document.getElementById('am-dl'); if (d) return d;
    d = document.createElement('dialog'); d.id = 'am-dl'; d.className = 'am-dl'; d.setAttribute('aria-labelledby', 'am-dl-t');
    if (lang === 'ar') d.dir = 'rtl';
    d.innerHTML = '<div class="dl-card"><button class="dl-x" type="button" aria-label="' + esc(T.close) + '"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg></button>' +
      '<img src="/img/logo.svg" width="40" height="40" alt=""><h2 id="am-dl-t">' + esc(T.dlTitle) + '</h2><p class="dl-sub">' + esc(T.dlSub) + '</p>' +
      '<a class="dl-opt dl-store" href="' + STORE + '" rel="noopener"><svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true"><path d="M4 8h16l-1.2 11.2a1 1 0 0 1-1 .8H6.2a1 1 0 0 1-1-.8z" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linejoin="round"/><path d="M9 8V6.5a3 3 0 0 1 6 0V8" fill="none" stroke="currentColor" stroke-width="1.8"/></svg><span><b>' + esc(T.store) + '</b><small>' + esc(T.storeSub) + '</small></span></a>' +
      '<a class="dl-opt dl-direct" href="' + RELEASES + '"><svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true"><path d="M12 4v11M7 10.5l5 5 5-5M5 20h14" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg><span><b>' + esc(T.direct) + '</b><small>' + esc(T.directSub) + '</small><small class="dl-ver" hidden></small></span></a>' +
      '<p class="dl-foot"><a href="' + pre + '/app.html">' + esc(T.phoneLink) + '</a><a href="#new" class="dl-new">' + esc(NEW.t) + '</a></p></div>';
    document.body.appendChild(d);
    d.querySelector('.dl-x').addEventListener('click', function () { close(d); });
    d.querySelector('.dl-new').addEventListener('click', function (e) { e.preventDefault(); d.close(); openNew(); });
    d.addEventListener('click', function (e) { if (e.target === d) close(d); });   // the backdrop
    d.addEventListener('cancel', function (e) { e.preventDefault(); close(d); });  // Esc: the same short exit
    fill(d); return d;
  }
  var closing = 0;
  function close(d) { if (!d.open || d.classList.contains('closing')) return; d.classList.add('closing'); closing = setTimeout(function () { closing = 0; d.classList.remove('closing'); d.close(); }, matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 140); }
  function open(from) {
    latest(); var d = dialog();
    if (typeof d.showModal !== 'function') { location.href = (from && from.href) || RELEASES; return; }   // a very old browser: the link itself
    if (closing) { clearTimeout(closing); closing = 0; d.classList.remove('closing'); }   // hunters14 SITE-L2: reopened mid-close - it simply stays open
    if (!d.open) d.showModal(); d.querySelector('.dl-store').focus();
  }
  window.AdeeboMeterDownload = open;

  // ---- what's new, in plain words (24 Nov: "Everything that changed" went to GitHub's CHANGELOG - that list is for the nerds)
  var NEW = {"en": {"t": "What's new in 1.1.6", "s": "In plain words.", "i": [["Speaks your language", "English, French, Spanish, Arabic and Bengali - switch with the globe button."], ["A calmer corner", "It brightens only when you rest the mouse on it, and never gets in your way."], ["Follow companies", "Pick from the big names and keep up to four on the widget."], ["A clearer report for your provider", "One page: what's wrong, where it starts, and a message ready to send."], ["For gamers, streamers and traders", "Game ping, dips in your streams, and the line to your broker."], ["Draw your network", "Break one piece and see what stops working."], ["Steadier everywhere", "Hundreds of fixes from testing on real PCs."]], "f": "Still free. No account, nothing about you collected."}, "fr": {"t": "Nouveautés de la 1.1.6", "s": "En mots simples.", "i": [["Parle votre langue", "Anglais, français, espagnol, arabe et bengali : changez avec le bouton globe."], ["Un coin plus calme", "Il ne s'éclaire que quand la souris s'y pose, et ne vous gêne jamais."], ["Suivez des entreprises", "Choisissez parmi les grands noms et gardez-en jusqu'à quatre sur le widget."], ["Un rapport plus clair pour votre fournisseur", "Une page : ce qui ne va pas, où ça commence, et un message prêt à envoyer."], ["Pour les joueurs, le streaming et le trading", "Le ping des jeux, les baisses de vos diffusions et la ligne vers votre courtier."], ["Dessinez votre réseau", "Cassez un élément et voyez ce qui cesse de fonctionner."], ["Plus stable partout", "Des centaines de corrections après des tests sur de vrais PC."]], "f": "Toujours gratuit. Pas de compte, rien sur vous n'est collecté."}, "es": {"t": "Novedades de la 1.1.6", "s": "En palabras sencillas.", "i": [["Habla tu idioma", "Inglés, francés, español, árabe y bengalí: cambia con el botón del globo."], ["Una esquina más tranquila", "Solo se ilumina cuando apoyas el ratón encima y nunca estorba."], ["Sigue empresas", "Elige entre los grandes nombres y deja hasta cuatro en el widget."], ["Un informe más claro para tu proveedor", "Una página: qué falla, dónde empieza y un mensaje listo para enviar."], ["Para jugadores, streaming y trading", "El ping de los juegos, las caídas de tus emisiones y la línea a tu bróker."], ["Dibuja tu red", "Rompe una pieza y mira qué deja de funcionar."], ["Más estable en todo", "Cientos de correcciones tras probarlo en PC reales."]], "f": "Sigue siendo gratis. Sin cuenta, sin recopilar nada sobre ti."}, "ar": {"t": "الجديد في 1.1.6", "s": "بكلمات بسيطة.", "i": [["يتحدث لغتك", "الإنجليزية والفرنسية والإسبانية والعربية والبنغالية - بدّل من زر الكرة الأرضية."], ["زاوية أهدأ", "لا تضيء إلا عندما تريح الفأرة عليها، ولا تعترض طريقك أبدًا."], ["تابع الشركات", "اختر من الأسماء الكبيرة واحتفظ بأربع منها على الأداة."], ["تقرير أوضح لمزوّد الخدمة", "صفحة واحدة: ما الخلل، وأين يبدأ، ورسالة جاهزة للإرسال."], ["للاعبين والبث والتداول", "زمن استجابة الألعاب، وانقطاعات البث، والخط إلى وسيطك."], ["ارسم شبكتك", "عطّل جزءًا واحدًا وشاهد ما الذي يتوقف."], ["أكثر ثباتًا في كل مكان", "مئات الإصلاحات بعد الاختبار على أجهزة حقيقية."]], "f": "ما زال مجانيًا. بلا حساب، ولا يجمع أي شيء عنك."}, "bn": {"t": "1.1.6-এ নতুন কী", "s": "সহজ ভাষায়।", "i": [["আপনার ভাষায় কথা বলে", "ইংরেজি, ফরাসি, স্প্যানিশ, আরবি ও বাংলা - গ্লোব বোতাম দিয়ে বদলান।"], ["আরও শান্ত কোণ", "মাউস একটু থামালে তবেই উজ্জ্বল হয়, কখনো পথ আটকায় না।"], ["কোম্পানি অনুসরণ করুন", "বড় নামগুলো থেকে বেছে নিন, উইজেটে চারটি পর্যন্ত রাখুন।"], ["ISP-র জন্য আরও পরিষ্কার রিপোর্ট", "এক পাতায়: কী সমস্যা, কোথা থেকে শুরু, আর পাঠানোর মতো তৈরি বার্তা।"], ["গেমার, স্ট্রিমার ও ট্রেডারদের জন্য", "গেমের পিং, স্ট্রিমের ড্রপ, আর ব্রোকার পর্যন্ত লাইন।"], ["আপনার নেটওয়ার্ক আঁকুন", "একটি অংশ ভাঙুন, দেখুন কী বন্ধ হয়ে যায়।"], ["সবখানে আরও স্থির", "আসল পিসিতে পরীক্ষার পর শত শত সংশোধন।"]], "f": "এখনও বিনামূল্যে। অ্যাকাউন্ট নেই, আপনার কিছুই সংগ্রহ করা হয় না।"}}[lang];
  function newDialog() {
    var d = document.getElementById('am-new'); if (d) return d;
    d = document.createElement('dialog'); d.id = 'am-new'; d.className = 'am-dl am-new'; d.setAttribute('aria-labelledby', 'am-new-t');
    if (lang === 'ar') d.dir = 'rtl';
    d.innerHTML = '<div class="dl-card"><button class="dl-x" type="button" aria-label="' + esc(T.close) + '"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg></button>' +
      '<img src="/img/logo.svg" width="40" height="40" alt=""><h2 id="am-new-t">' + esc(NEW.t) + '</h2><p class="dl-sub">' + esc(NEW.s) + '</p><ul class="nw-list">' +
      NEW.i.map(function (x) { return '<li><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M5 12.5l4.2 4.2L19 7" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/></svg><span><b>' + esc(x[0]) + '</b><small>' + esc(x[1]) + '</small></span></li>'; }).join('') +
      '</ul><p class="nw-foot">' + esc(NEW.f) + '</p><button class="dl-opt dl-store nw-get" type="button"><svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true"><path d="M12 4v11M7 10.5l5 5 5-5M5 20h14" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg><span><b>' + esc(T.download) + '</b></span></button></div>';
    document.body.appendChild(d);
    d.querySelector('.dl-x').addEventListener('click', function () { close(d); });
    d.querySelector('.nw-get').addEventListener('click', function () { d.close(); open(null); });
    d.addEventListener('click', function (e) { if (e.target === d) close(d); });
    d.addEventListener('cancel', function (e) { e.preventDefault(); close(d); });
    return d;
  }
  // ---- the MIT License, in plain words (24 Nov: the footer link went to GitHub)
  var LIC = {"en": {"t": "MIT License", "s": "AdeeboMeter is free and open source. In plain words:", "i": [["Free to use", "At home, at work, anywhere - for as long as you like."], ["Free to share", "Give copies to anyone."], ["Free to change", "The code is open. Adapt it as you like, as long as the copyright line and this license go with it."], ["No warranty", "It comes as it is. Adeeb Khan isn't liable for problems that come from using it."]], "full": "The full license text"}, "fr": {"t": "Licence MIT", "s": "AdeeboMeter est gratuit et open source. En mots simples :", "i": [["Libre d'utilisation", "À la maison, au travail, partout, aussi longtemps que vous voulez."], ["Libre de partage", "Donnez-en des copies à qui vous voulez."], ["Libre de modification", "Le code est ouvert. Adaptez-le à votre guise, tant que la mention de droit d'auteur et cette licence l'accompagnent."], ["Sans garantie", "Il est fourni tel quel. Adeeb Khan n'est pas responsable des problèmes liés à son utilisation."]], "full": "Le texte complet de la licence (en anglais)"}, "es": {"t": "Licencia MIT", "s": "AdeeboMeter es gratis y de código abierto. En palabras sencillas:", "i": [["Libre para usar", "En casa, en el trabajo, donde sea, todo el tiempo que quieras."], ["Libre para compartir", "Dale copias a quien quieras."], ["Libre para cambiar", "El código es abierto. Adáptalo como quieras, siempre que lo acompañen la línea de derechos de autor y esta licencia."], ["Sin garantía", "Se ofrece tal cual. Adeeb Khan no se hace responsable de los problemas que surjan por usarlo."]], "full": "El texto completo de la licencia (en inglés)"}, "ar": {"t": "رخصة MIT", "s": "AdeeboMeter مجاني ومفتوح المصدر. بكلمات بسيطة:", "i": [["حر في الاستخدام", "في البيت، في العمل، في أي مكان، ولأي مدة تريدها."], ["حر في المشاركة", "أعطِ نسخًا لمن تشاء."], ["حر في التعديل", "الشيفرة مفتوحة. عدّلها كما تشاء، بشرط أن يبقى معها سطر حقوق النشر وهذه الرخصة."], ["بلا ضمان", "يُقدَّم كما هو. لا يتحمل Adeeb Khan مسؤولية المشكلات الناتجة عن استخدامه."]], "full": "النص الكامل للرخصة (بالإنجليزية)"}, "bn": {"t": "MIT লাইসেন্স", "s": "AdeeboMeter বিনামূল্যে ও ওপেন সোর্স। সহজ ভাষায়:", "i": [["ব্যবহারে স্বাধীন", "বাড়িতে, অফিসে, যেকোনো জায়গায় - যতদিন খুশি।"], ["শেয়ারে স্বাধীন", "যাকে খুশি কপি দিন।"], ["বদলাতে স্বাধীন", "কোড খোলা। ইচ্ছেমতো বদলান, শুধু কপিরাইট লাইন আর এই লাইসেন্স সঙ্গে রাখুন।"], ["কোনো ওয়ারেন্টি নেই", "যেমন আছে তেমনই দেওয়া। ব্যবহার থেকে কোনো সমস্যা হলে Adeeb Khan দায়ী নন।"]], "full": "লাইসেন্সের পুরো লেখা (ইংরেজিতে)"}}[lang], MIT = "MIT License\n\nCopyright (c) 2026 Adeeb Khan\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\nThe above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\nTHE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";
  function openLicense() {
    var d = document.getElementById('am-lic');
    if (!d) {
      d = document.createElement('dialog'); d.id = 'am-lic'; d.className = 'am-dl am-new'; d.setAttribute('aria-labelledby', 'am-lic-t');
      if (lang === 'ar') d.dir = 'rtl';
      d.innerHTML = '<div class="dl-card"><button class="dl-x" type="button" aria-label="' + esc(T.close) + '"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg></button>' +
        '<img src="/img/logo.svg" width="40" height="40" alt=""><h2 id="am-lic-t">' + esc(LIC.t) + '</h2><p class="dl-sub">' + esc(LIC.s) + '</p><ul class="nw-list">' +
        LIC.i.map(function (x) { return '<li><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M5 12.5l4.2 4.2L19 7" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"/></svg><span><b>' + esc(x[0]) + '</b><small>' + esc(x[1]) + '</small></span></li>'; }).join('') +
        '</ul><details class="lic-full"><summary>' + esc(LIC.full) + '</summary><pre dir="ltr" lang="en">' + esc(MIT) + '</pre></details><p class="nw-foot">© 2026 Adeeb Khan</p></div>';
      document.body.appendChild(d);
      d.querySelector('.dl-x').addEventListener('click', function () { close(d); });
      d.addEventListener('click', function (e) { if (e.target === d) close(d); });
      d.addEventListener('cancel', function (e) { e.preventDefault(); close(d); });
    }
    if (typeof d.showModal !== 'function') return;
    if (!d.open) d.showModal(); d.querySelector('.dl-x').focus();
  }
  // ---- the poster: once, for a first visit - a small corner card, never over the page (24 Nov)
  var PO = {"en":{"poster":"Poster","line":"Everything AdeeboMeter does, on one page.","see":"See the poster","get":"Download the poster","alt":"The AdeeboMeter poster: what it does - the corner widget, What's wrong?, the ISP report, 232 tools, AdeeboShell, AdeeboLab and more"},"fr":{"poster":"Affiche","line":"Tout ce que fait AdeeboMeter, sur une page.","see":"Voir l’affiche","get":"Télécharger l’affiche","alt":"L’affiche d’AdeeboMeter : ce qu’il fait - le widget, Qu’est-ce qui ne va pas ?, le rapport pour le fournisseur, 232 outils, AdeeboShell, AdeeboLab et plus"},"es":{"poster":"Póster","line":"Todo lo que hace AdeeboMeter, en una página.","see":"Ver el póster","get":"Descargar el póster","alt":"El póster de AdeeboMeter: lo que hace - el widget, ¿Qué pasa?, el informe para el proveedor, 232 herramientas, AdeeboShell, AdeeboLab y más"},"ar":{"poster":"الملصق","line":"كل ما يفعله AdeeboMeter في صفحة واحدة.","see":"شاهد الملصق","get":"نزّل الملصق","alt":"ملصق AdeeboMeter: ما يفعله - أداة الزاوية، ما الخلل؟، تقرير مزوّد الخدمة، 232 أداة، AdeeboShell، AdeeboLab والمزيد"},"bn":{"poster":"পোস্টার","line":"AdeeboMeter যা যা করে, এক পাতায়।","see":"পোস্টার দেখুন","get":"পোস্টার ডাউনলোড করুন","alt":"AdeeboMeter-এর পোস্টার: এটি কী করে - কোণের উইজেট, What's wrong?, ISP রিপোর্ট, ২৩২টি টুল, AdeeboShell, AdeeboLab ও আরও"},"zh":{"poster":"海报","line":"一张图了解 AdeeboMeter 的全部功能。","see":"查看海报","get":"下载海报","alt":"AdeeboMeter 功能海报：角落小组件、问题诊断、运营商报告、232 个工具、AdeeboShell、AdeeboLab 等"}}[lang];
  function openPoster() {
    var d = document.getElementById('am-poster');
    if (!d) {
      d = document.createElement('dialog'); d.id = 'am-poster'; d.className = 'am-dl am-pv'; d.setAttribute('aria-label', PO.poster);
      if (lang === 'ar') d.dir = 'rtl';
      d.innerHTML = '<div class="dl-card"><button class="dl-x" type="button" aria-label="' + esc(T.close) + '"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg></button>' +
        '<img class="pv-img" src="/img/poster.png" width="1080" height="1350" alt="' + esc(PO.alt) + '"><p class="dl-foot"><a href="/img/poster.png" download="AdeeboMeter-poster.png">' + esc(PO.get) + '</a></p></div>';
      document.body.appendChild(d);
      d.querySelector('.dl-x').addEventListener('click', function () { close(d); });
      d.addEventListener('click', function (e) { if (e.target === d) close(d); });
      d.addEventListener('cancel', function (e) { e.preventDefault(); close(d); });
    }
    if (typeof d.showModal !== 'function') { location.href = '/img/poster.png'; return; }
    if (!d.open) d.showModal(); d.querySelector('.dl-x').focus();
  }
  document.addEventListener('click', function (e) {
    if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    var el = e.target.closest && e.target.closest('a[href="/img/poster.png"]'); if (!el || el.hasAttribute('download')) return;
    e.preventDefault(); [].forEach.call(document.querySelectorAll('details.sb-more[open], details.navmore[open]'), function (dd) { dd.removeAttribute('open'); }); openPoster();
  });
  (function () {
    var seen = true; try { seen = !!localStorage.getItem('am-poster'); } catch (e) {}
    if (seen || html.classList.contains('inapp') || /^\/(app|r|remote)\.html$/.test(location.pathname)) return;
    setTimeout(function () {
      if (document.querySelector('dialog[open]')) return;   // never over a window someone opened
      try { localStorage.setItem('am-poster', '1'); } catch (e) {}
      var c = document.createElement('aside'); c.className = 'am-pc'; c.setAttribute('aria-label', PO.poster); if (lang === 'ar') c.dir = 'rtl';
      c.innerHTML = '<button class="pc-x" type="button" aria-label="' + esc(T.close) + '"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg></button><img src="/img/poster-sm.webp" width="96" height="120" alt="">' +
        '<div><p>' + esc(PO.line) + '</p><button class="pc-see" type="button">' + esc(PO.see) + '</button></div><i class="pc-bar" aria-hidden="true"></i>';
      document.body.appendChild(c);
      var left = 8000, t0 = 0, timer = 0;
      function hide() { clearTimeout(timer); c.classList.add('out'); setTimeout(function () { c.remove(); }, 180); }
      function run() { t0 = Date.now(); c.classList.remove('hold'); timer = setTimeout(hide, left); }
      function hold() { if (!t0) return; clearTimeout(timer); left = Math.max(1500, left - (Date.now() - t0)); t0 = 0; c.classList.add('hold'); }
      c.addEventListener('mouseenter', hold); c.addEventListener('focusin', hold);
      c.addEventListener('mouseleave', function () { if (!c.contains(document.activeElement)) run(); });
      c.querySelector('.pc-x').addEventListener('click', hide);
      c.querySelector('.pc-see').addEventListener('click', function () { hide(); openPoster(); });
      requestAnimationFrame(function () { c.classList.add('in'); run(); });
    }, 3000);
  })();
  function openNew() {
    var d = newDialog();
    if (typeof d.showModal !== 'function') { location.hash = 'new'; return; }
    if (!d.open) d.showModal(); d.querySelector('.dl-x').focus();
  }
  document.addEventListener('click', function (e) {
    if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    var el = e.target.closest && e.target.closest('a[data-whatsnew], a[href$="/LICENSE"]'); if (!el) return;
    e.preventDefault(); if (el.hasAttribute('data-whatsnew')) openNew(); else openLicense();
  });
  document.addEventListener('click', function (e) {
    if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    var el = e.target.closest && e.target.closest('a[data-download], a.dl, a.sb-dl, a[href="' + RELEASES + '"]');
    if (!el || el.closest('#am-dl')) return;
    e.preventDefault(); open(el);
  });
})();
