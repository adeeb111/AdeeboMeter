(function () {
  "use strict";

  // Public website token from Cloudflare Web Analytics, not an API credential.
  var siteToken = "d012c320b71641e0bfeafe23b701d164";
  var excludedPages = /\/(?:app|files|live|remote|r|games)(?:\.html)?\/?$/;
  var productionHost = location.hostname === "adeebometer.com" ||
    location.hostname === "www.adeebometer.com";

  if (!productionHost || !/^[a-f0-9]{32}$/i.test(siteToken) ||
      excludedPages.test(location.pathname) ||
      navigator.doNotTrack === "1" || navigator.globalPrivacyControl === true) {
    return;
  }

  var beacon = document.createElement("script");
  beacon.type = "module";
  beacon.src = "https://static.cloudflareinsights.com/beacon.min.js";
  beacon.setAttribute("data-cf-beacon", JSON.stringify({ token: siteToken }));
  document.head.appendChild(beacon);
}());
