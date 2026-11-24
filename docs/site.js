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
  var m = /^\/(fr|es|ar|bn)\//.exec(location.pathname), lang = m ? m[1] : 'en', pre = m ? '/' + m[1] : '';
  var T = {
    en: { site: 'Site', learn: 'Learn', blog: 'Blog', games: 'Games', live: 'Live', more: 'More', files: 'Files', phone: 'Phone app', gaming: 'Gaming', streaming: 'Streaming', trading: 'Trading', isp: 'ISP report', hof: 'Hall of Fame', privacy: 'Privacy', donate: 'Donate', download: 'Download',
      dlTitle: 'Download AdeeboMeter', dlSub: 'Free for Windows 10 and 11. No admin rights needed, nothing about you collected.', store: 'Microsoft Store', storeSub: 'Signed and kept up to date by Windows.', direct: 'Direct download', directSub: 'A zip with the app - unzip and run, it offers to install itself.', directVer: 'Version {v}', all: 'Every version and its notes', phoneLink: 'On a phone? Open the phone app', close: 'Close' },
    fr: { site: 'Site', learn: 'Apprendre', blog: 'Blogue', games: 'Jeux', live: 'En direct', more: 'Plus', files: 'Fichiers', phone: 'App mobile', gaming: 'Jeux vidéo', streaming: 'Streaming', trading: 'Trading', isp: 'Rapport pour le FAI', hof: 'Temple de la renommée', privacy: 'Confidentialité', donate: 'Faire un don', download: 'Télécharger',
      dlTitle: 'Télécharger AdeeboMeter', dlSub: 'Gratuit pour Windows 10 et 11. Aucun droit d’administrateur, rien sur vous n’est collecté.', store: 'Microsoft Store', storeSub: 'Signé et tenu à jour par Windows.', direct: 'Téléchargement direct', directSub: 'Un zip avec l’app : décompressez et lancez, elle propose de s’installer.', directVer: 'Version {v}', all: 'Toutes les versions et leurs notes', phoneLink: 'Sur un téléphone ? Ouvrir l’app mobile', close: 'Fermer' },
    es: { site: 'Sitio', learn: 'Aprender', blog: 'Blog', games: 'Juegos', live: 'En vivo', more: 'Más', files: 'Archivos', phone: 'App móvil', gaming: 'Videojuegos', streaming: 'Streaming', trading: 'Trading', isp: 'Informe al proveedor', hof: 'Salón de la fama', privacy: 'Privacidad', donate: 'Donar', download: 'Descargar',
      dlTitle: 'Descargar AdeeboMeter', dlSub: 'Gratis para Windows 10 y 11. Sin permisos de administrador y sin recopilar nada sobre ti.', store: 'Microsoft Store', storeSub: 'Firmada y actualizada por Windows.', direct: 'Descarga directa', directSub: 'Un zip con la app: descomprímelo y ábrela, te ofrece instalarse.', directVer: 'Versión {v}', all: 'Todas las versiones y sus notas', phoneLink: '¿En un teléfono? Abre la app móvil', close: 'Cerrar' },
    ar: { site: 'الموقع', learn: 'تعلّم', blog: 'المدونة', games: 'الألعاب', live: 'مباشر', more: 'المزيد', files: 'الملفات', phone: 'تطبيق الهاتف', gaming: 'الألعاب الإلكترونية', streaming: 'البث', trading: 'التداول', isp: 'تقرير لمزوّد الخدمة', hof: 'قاعة المشاهير', privacy: 'الخصوصية', donate: 'تبرّع', download: 'تنزيل',
      dlTitle: 'تنزيل AdeeboMeter', dlSub: 'مجاني لنظامي Windows 10 و11. لا يحتاج صلاحيات المسؤول، ولا يجمع أي شيء عنك.', store: 'Microsoft Store', storeSub: 'موقّع ويحدّثه Windows تلقائيًا.', direct: 'تنزيل مباشر', directSub: 'ملف zip فيه التطبيق: فكّ الضغط وشغّله، وسيعرض تثبيت نفسه.', directVer: 'الإصدار {v}', all: 'كل الإصدارات وملاحظاتها', phoneLink: 'على الهاتف؟ افتح تطبيق الهاتف', close: 'إغلاق' },
    bn: { site: 'সাইট', learn: 'শিখুন', blog: 'ব্লগ', games: 'গেম', live: 'লাইভ', more: 'আরও', files: 'ফাইল', phone: 'ফোন অ্যাপ', gaming: 'গেমিং', streaming: 'স্ট্রিমিং', trading: 'ট্রেডিং', isp: 'ISP রিপোর্ট', hof: 'হল অব ফেম', privacy: 'গোপনীয়তা', donate: 'দান করুন', download: 'ডাউনলোড',
      dlTitle: 'AdeeboMeter ডাউনলোড', dlSub: 'Windows 10 ও 11-এর জন্য বিনামূল্যে। অ্যাডমিন অনুমতি লাগে না, আপনার কিছুই সংগ্রহ করা হয় না।', store: 'Microsoft Store', storeSub: 'সাইন করা, আর Windows নিজেই হালনাগাদ রাখে।', direct: 'সরাসরি ডাউনলোড', directSub: 'অ্যাপসহ একটি zip: আনজিপ করে চালান, নিজেই ইনস্টল করার প্রস্তাব দেবে।', directVer: 'সংস্করণ {v}', all: 'সব সংস্করণ ও সেগুলোর নোট', phoneLink: 'ফোনে আছেন? ফোন অ্যাপ খুলুন', close: 'বন্ধ করুন' }
  }[lang];
  var STORE = 'https://apps.microsoft.com/detail/9PFGXJPL9CLJ?mode=direct', RELEASES = 'https://github.com/adeeb111/AdeeboMeter/releases/latest';
  function esc(s) { return String(s).replace(/[&<>"]/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]; }); }
  var path = location.pathname.replace(/index\.html$/, '');
  function cur(href) { var h = href.replace(/index\.html$/, ''); return h === path || (h.length > pre.length + 1 && h.slice(-1) === '/' && path.indexOf(h) === 0) ? ' aria-current="page"' : ''; }
  function a(href, label, cls) { return '<a' + (cls ? ' class="' + cls + '"' : '') + ' href="' + href + '"' + cur(href) + '>' + esc(label) + '</a>'; }
  var gaming = pre + '/gaming.html', main = [[pre + '/learn/', T.learn], [pre + '/blog/', T.blog], ['/games.html', T.games], ['/live.html', T.live]];
  var more = [['/files.html', T.files], ['/app.html', T.phone], [gaming, T.gaming], [pre + '/streaming.html', T.streaming], [pre + '/trading.html', T.trading], [pre + '/isp-report.html', T.isp], ['/hall-of-fame.html', T.hof], [pre + '/privacy.html', T.privacy]];
  var bar = document.getElementById('sitebar');
  if (bar && !bar.getAttribute('data-filled')) {
    bar.setAttribute('data-filled', '1'); bar.setAttribute('aria-label', T.site);
    var moreHere = more.some(function (x) { return cur(x[0]); });
    bar.innerHTML = '<a class="sb-brand" href="' + pre + '/"><img src="/img/logo.svg" width="24" height="24" alt=""><span>Adeebo<b>Meter</b></span></a>' +
      '<div class="sb-links">' + main.map(function (x) { return a(x[0], x[1], 'sb-main'); }).join('') +
      '<details class="sb-more"><summary' + (moreHere ? ' aria-current="true"' : '') + '><span>' + esc(T.more) + '</span><svg viewBox="0 0 24 24" width="16" height="16" aria-hidden="true"><path d="M6 9l6 6 6-6" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg></summary><div class="sb-pop">' +
      main.map(function (x) { return a(x[0], x[1], 'sb-narrow'); }).join('') + more.map(function (x) { return a(x[0], x[1]); }).join('') + '</div></details>' +
      '<a class="sb-heart" href="' + pre + '/#donate" title="' + esc(T.donate) + '" aria-label="' + esc(T.donate) + '"><svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true"><path d="M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z" fill="currentColor"/></svg></a>' +
      '<a class="sb-dl" data-download href="' + RELEASES + '">' + esc(T.download) + '</a></div>';
    // a menu closes on Esc, on a click outside, and when another opens - like the home page's More
    document.addEventListener('click', function (e) { var d = bar.querySelector('details[open]'); if (d && !d.contains(e.target)) d.removeAttribute('open'); });
    document.addEventListener('keydown', function (e) { var d = bar.querySelector('details[open]'); if (e.key === 'Escape' && d) { d.removeAttribute('open'); d.querySelector('summary').focus(); } });
  }

  // ---- the download window
  var zip = null, ver = '';
  function latest() {
    if (latest.asked) return; latest.asked = true;
    try {
      fetch('https://api.github.com/repos/adeeb111/AdeeboMeter/releases/latest', { headers: { Accept: 'application/vnd.github+json' } })
        .then(function (r) { return r.ok ? r.json() : null; })
        .then(function (rel) {
          if (!rel || !rel.assets) return;
          var z = rel.assets.filter(function (x) { return /-win64\.zip$/.test(x.name); })[0]; if (!z) return;
          zip = z.browser_download_url; ver = (rel.tag_name || '').replace(/^v/, '');
          var d = document.getElementById('am-dl'); if (d) fill(d);
        }).catch(function () {});
    } catch (e) {}
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
      '<p class="dl-foot"><a href="/app.html">' + esc(T.phoneLink) + '</a><a href="' + RELEASES + '" rel="noopener">' + esc(T.all) + '</a></p></div>';
    document.body.appendChild(d);
    d.querySelector('.dl-x').addEventListener('click', function () { close(d); });
    d.addEventListener('click', function (e) { if (e.target === d) close(d); });   // the backdrop
    d.addEventListener('cancel', function (e) { e.preventDefault(); close(d); });  // Esc: the same short exit
    fill(d); return d;
  }
  function close(d) { if (!d.open || d.classList.contains('closing')) return; d.classList.add('closing'); setTimeout(function () { d.classList.remove('closing'); d.close(); }, matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 140); }
  function open(from) {
    latest(); var d = dialog();
    if (typeof d.showModal !== 'function') { location.href = (from && from.href) || RELEASES; return; }   // a very old browser: the link itself
    d.showModal(); d.querySelector('.dl-store').focus();
  }
  window.AdeeboMeterDownload = open;
  document.addEventListener('click', function (e) {
    if (e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
    var el = e.target.closest && e.target.closest('a[data-download], a.dl, a.sb-dl, a[href="' + RELEASES + '"]');
    if (!el || el.closest('#am-dl')) return;
    e.preventDefault(); open(el);
  });
})();
