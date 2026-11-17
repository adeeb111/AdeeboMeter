# AdeeboMeter 1.1.6 - plan

Status (17 Nov 2026): planning, on branch dev-1.1.6 (from dev-1.1.5 after the 1.1.5 release). 1.1.5 went live on
17 Nov 2026, code-signed. One release, everything together (Adeeb's rule). Nothing is built, tested, committed or
published until he says go; designs are shown first.

## Rules every item keeps
No admin · never listens · offline by default · nothing stored beyond what each feature documents · C# 5 / .NET 4.8 ·
phone and website save nothing (passthrough) · no alerts or pop-ups · motto: "Know what's wrong. Show anyone."

## 1. Fixes found after 1.1.5
- [done, 306b9a6] Every tool blinked: the question rail and the tool list each set the page height on every paint, so
  the scrollbar flipped and asked for another repaint. Now one height per paint.
- [done, 03703a5, shipped in 1.1.5] Self-test "no leaks": a one-time handle bump fails only if 30 more opens grow it again.

## 2. Website refresh (adeebometer.com)
- New top of the page (mockup shown 17 Nov): navy look matching the phone app, two-colour AdeeboMeter name.
  - Headline (decided 17 Nov): "Find out why your internet or PC is slow." + "And much more." (gradient).
  - Slogan pill above it: "Know what's wrong. Show anyone."
  - Example result with the full path (This PC > Router > DNS > Internet) and Save picture / Send to my ISP.
  - Phone + PC linked (QR and matching words), "Signed: Open Source Developer MdAhasanul Adeeb Khan" badge.
  - Trust line: Free · No admin rights · No cloud, no telemetry · Never listens · Signed by its author.
- The rest of the page in the same navy cards and spacing - same content, not rewritten. Light/dark switch stays.
- Open question: replace "Is it me or the network?" with the new motto in the page title, social preview and the
  release notes header.
- Users who had the old page: the offline copy (sw.js) refreshes on the second visit - consider a one-line
  "updated - reload" note when a new site version arrives.

## 3. Release flow with code signing
- 1.1.5 showed the steps: GitHub builds and attaches files on publish -> Adeeb signs the exe with tools/sign.ps1
  (SimplySign on his PC, phone code) -> win64 zip, SHA256SUMS and notes rebuilt around the signed exe -> files swapped
  on the release by hand (this assistant can't edit releases).
- Make it one step for him: a `release.ps1 -Sign` (or tools/finish-release.ps1) that downloads the release files, signs,
  rebuilds the zip/sums/notes and uploads with gh - so nothing is done by hand in the browser. Needs gh on his PC.
- The release tag rule ("Protect v* tags") blocks deleting a tag: a broken release means a new version, not a re-tag.
- SimplySign logs out after a while - sign.ps1 should say "log in to SimplySign again" for error 0x80090020 instead of
  retrying three times.

## 4. Carried over (ideas waiting for a decision)
- Phone remote: "allow when away" setting + session log, a second-device waker, "invite another device from your
  phone" (office PC without a QR trip).
- Small motion details on the PC.
- Microsoft Store: 1.1.5.0 package from the release run's artifacts (AdeeboMeter-store).
