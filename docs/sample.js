// The example in the homepage meter (23 Nov): "Save picture" and "Send to my ISP" show what the app makes, in place,
// then close on their own after 10 seconds (the bar shows the time left). Hovering, focusing or a hidden tab pauses
// the count; Esc and the close button close it at once; focus goes back to the button that opened it.
// Opened from the keyboard, focus moves to its close button, and the count waits while focus is inside.
(function () {
  var box = document.getElementById("am-sample"); if (!box) return;
  var bar = box.querySelector(".sbar i"), shut = box.querySelector(".sx"), from = null, timer = 0;
  function show(k, btn, keys) {
    from = btn;
    [].forEach.call(box.querySelectorAll("[data-k]"), function (e) { e.hidden = e.getAttribute("data-k") !== k; });
    [].forEach.call(document.querySelectorAll(".meter .acts button"), function (b) { b.setAttribute("aria-expanded", b === btn ? "true" : "false"); });
    clearTimeout(timer); box.hidden = false;
    bar.style.animation = "none"; void bar.offsetWidth; bar.style.animation = "";   // the count starts again for each example
    requestAnimationFrame(function () { box.setAttribute("data-open", ""); });
    // from the keyboard, focus moves into the example (and the count waits while it is there); a mouse click leaves focus alone
    if (keys) shut.focus({ preventScroll: true });
  }
  function hide() {
    if (box.hidden) return;
    var had = box.contains(document.activeElement);
    box.removeAttribute("data-open");
    [].forEach.call(document.querySelectorAll(".meter .acts button"), function (b) { b.setAttribute("aria-expanded", "false"); });
    timer = setTimeout(function () { box.hidden = true; }, 160);   // after the fade (140 ms)
    if (had && from) from.focus({ preventScroll: true });
  }
  [].forEach.call(document.querySelectorAll(".meter .acts button[data-sample]"), function (b) {
    b.addEventListener("click", function (e) { show(b.getAttribute("data-sample"), b, e.detail === 0); });
  });
  shut.addEventListener("click", hide);
  bar.addEventListener("animationend", hide);
  box.addEventListener("keydown", function (e) { if (e.key === "Escape") { e.preventDefault(); hide(); } });
  document.addEventListener("visibilitychange", function () { box.toggleAttribute("data-paused", document.hidden); });
})();
