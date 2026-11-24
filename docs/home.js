// Homepage: the parts that open on request (the feedback form, the folded slides).
(function () {
  "use strict";
  // Feedback: three buttons open the form with the kind already picked; the form's own switch stays in sync.
  var form = document.getElementById("fb-form");
  var opens = document.querySelectorAll("[data-fb]");
  opens.forEach(function (b) {
    b.addEventListener("click", function () {
      var k = b.getAttribute("data-fb");
      var first = form.hidden;
      form.hidden = false;
      var seg = form.querySelector('.seg button[data-k="' + k + '"]');
      if (seg) seg.click();
      opens.forEach(function (x) {
        var on = x === b;
        x.setAttribute("aria-expanded", on ? "true" : "false");
        x.classList.toggle("line", !on);
      });
      if (first) { var t = document.getElementById("fb-title"); if (t) t.focus({ preventScroll: false }); }
    });
  });
  // Slides: the deck measures itself; tell it once it is visible.
  var fold = document.querySelector("details.tfold");
  if (fold) fold.addEventListener("toggle", function () {
    if (fold.open) requestAnimationFrame(function () { window.dispatchEvent(new Event("resize")); });
  });
})();
