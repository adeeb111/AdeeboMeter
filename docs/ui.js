/* Shared look for Learn, Games, Live and Files: a top bar with the logo, the page's icon, and a Learn / Games / Live / Files bar.
   Nothing is sent anywhere; it only draws. Inside the phone app (an iframe) the app shows its own top bar. */
(function () {
  var I = {
    back: '<path d="M15 18l-6-6 6-6"/>', chev: '<path d="M9 6l6 6-6 6"/>',
    book: '<path d="M2 4h7a3 3 0 013 3v13a2 2 0 00-2-2H2z"/><path d="M22 4h-7a3 3 0 00-3 3v13a2 2 0 012-2h8z"/>',
    game: '<rect x="2" y="7" width="20" height="11" rx="5"/><path d="M7 11v4M5 13h4M15 12h.01M18 14h.01"/>',
    live: '<circle cx="12" cy="12" r="2"/><path d="M8 8a6 6 0 000 8M16 8a6 6 0 010 8M5 5a10 10 0 000 14M19 5a10 10 0 010 14"/>',
    folder: '<path d="M3 6a2 2 0 012-2h4l2 2h8a2 2 0 012 2v10a2 2 0 01-2 2H5a2 2 0 01-2-2z"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="M20 20l-3.5-3.5"/>', play: '<path d="M8 5v14l11-7z" fill="currentColor"/>',
    net: '<rect x="9" y="2" width="6" height="5" rx="1"/><rect x="2" y="17" width="6" height="5" rx="1"/><rect x="16" y="17" width="6" height="5" rx="1"/><path d="M12 7v5M5 17v-3h14v3"/>',
    layers: '<path d="M12 3l9 5-9 5-9-5z"/><path d="M3 13l9 5 9-5"/>', globe: '<circle cx="12" cy="12" r="9.5"/><path d="M2.5 12h19M12 2.5c3 3 3 16 0 19M12 2.5c-3 3-3 16 0 19"/>',
    router: '<rect x="3" y="13" width="18" height="7" rx="2"/><path d="M7 17h.01M11 17h.01M12 13V8M8 6a6 6 0 018 0"/>', signal: '<path d="M4 20v-3M9 20v-7M14 20V9M19 20V4"/>',
    monitor: '<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/>', apps: '<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>',
    lock: '<rect x="4" y="11" width="16" height="10" rx="2"/><path d="M8 11V7a4 4 0 018 0v4"/>', unlock: '<rect x="4" y="11" width="16" height="10" rx="2"/><path d="M8 11V7a4 4 0 017.5-2"/>',
    chart: '<path d="M3 17l6-6 4 4 8-8"/><path d="M15 7h6v6"/>', stream: '<circle cx="12" cy="12" r="10"/><path d="M10 8l6 4-6 4z"/>',
    plug: '<path d="M9 2v6M15 2v6M6 8h12v4a6 6 0 01-12 0zM12 18v4"/>', quiz: '<circle cx="12" cy="12" r="10"/><path d="M9.1 9a3 3 0 015.8 1c0 2-3 3-3 3M12 17h.01"/>',
    order: '<path d="M10 6h11M10 12h11M10 18h11M4 6h1M4 12h1M4 18h1"/>', file: '<path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z"/><path d="M14 2v6h6"/>',
    wifi: '<path d="M2 9a15 15 0 0120 0"/><path d="M5.5 12.5a10 10 0 0113 0"/><path d="M9 16a5 5 0 016 0"/><circle cx="12" cy="19.5" r="1"/>',
    clock: '<circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>', dollar: '<path d="M12 2v20M17 6H9.5a3.5 3.5 0 000 7h5a3.5 3.5 0 010 7H6"/>',
    trophy: '<path d="M8 21h8M12 17v4M7 4h10v4a5 5 0 01-10 0zM17 5h3v2a3 3 0 01-3 3M7 5H4v2a3 3 0 003 3"/>',
    sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M2 12h2M20 12h2M4.9 4.9l1.4 1.4M17.7 17.7l1.4 1.4M4.9 19.1l1.4-1.4M17.7 6.3l1.4-1.4"/>',
    check: '<path d="M5 12.5l4.5 4.5L19 7.5"/>',
    copy: '<rect x="9" y="9" width="12" height="12" rx="2"/><path d="M5 15V5a2 2 0 012-2h10"/>', down: '<path d="M12 3v12M7 10l5 5 5-5M4 20h16"/>',
    share: '<circle cx="18" cy="5" r="2.5"/><circle cx="6" cy="12" r="2.5"/><circle cx="18" cy="19" r="2.5"/><path d="M8.2 10.8l7.6-4.4M8.2 13.2l7.6 4.4"/>',
    key: '<circle cx="7.5" cy="15.5" r="4.5"/><path d="M10.7 12.3L20 3M16 7l3 3M14 9l2 2"/>', info: '<circle cx="12" cy="12" r="10"/><path d="M12 11v6M12 7.5v.5"/>'
  };
  function ic(n, s) { s = s || 24; return '<svg class="ui-i" viewBox="0 0 24 24" width="' + s + '" height="' + s + '" aria-hidden="true">' + (I[n] || '') + '</svg>'; }
  window.UI = { ic: ic };
  var html = document.documentElement, body = document.body, sec = body.getAttribute('data-sec') || '';
  // the app's Look: Dark, Light, Neon, Midnight, Sunset or Mint (the desktop's palettes), or the phone's own light/dark
  try { var th = localStorage.getItem('am-theme') || '', pal = ['neon', 'midnight', 'sunset', 'mint'].indexOf(th) >= 0 ? th : '';
    if (pal) { html.setAttribute('data-pal', pal); html.setAttribute('data-theme', pal === 'mint' ? 'light' : 'dark'); }
    else if (th === 'system' || th === '') { if (th === 'system') html.setAttribute('data-theme', matchMedia('(prefers-color-scheme: light)').matches ? 'light' : 'dark'); } } catch (e) {}
  if (self !== top) html.classList.add('inapp');
  var S = [['learn', '/learn/', 'book', 'Learn'], ['games', '/games.html', 'game', 'Games'], ['live', '/live.html', 'live', 'Live'], ['files', '/files.html', 'folder', 'Files']];
  var lesson = sec === 'learn' && !/\/learn\/(index\.html)?$/.test(location.pathname);
  var bar = document.createElement('header'); bar.className = 'ui-appbar';
  bar.innerHTML = '<a class="ui-back" href="' + (lesson ? '/learn/' : '/app.html') + '" aria-label="' + (lesson ? 'Back to Learn' : 'Back to the app') + '">' + ic('back', 26) + '</a>' +
    '<a class="ui-brand" href="/"><img src="/img/logo.svg" width="30" height="30" alt="">AdeeboMeter</a>' +
    '<nav class="ui-links">' + S.map(function (s) { return '<a href="' + s[1] + '"' + (s[0] === sec ? ' aria-current="page"' : '') + '>' + s[3] + '</a>'; }).join('') + '<a href="/app.html">Phone app</a></nav>';
  body.insertBefore(bar, body.firstChild);
  var nav = document.createElement('nav'); nav.className = 'ui-sbar'; nav.setAttribute('aria-label', 'Sections');
  nav.innerHTML = S.map(function (s) { return '<a href="' + s[1] + '"' + (s[0] === sec ? ' class="on" aria-current="page"' : '') + '>' + ic(s[2], 24) + '<span>' + s[3] + '</span></a>'; }).join('');
  body.appendChild(nav);
  [].forEach.call(document.querySelectorAll('[data-ic]'), function (e) { e.insertAdjacentHTML('afterbegin', ic(e.getAttribute('data-ic'), +e.getAttribute('data-sz') || 24)); });
})();
