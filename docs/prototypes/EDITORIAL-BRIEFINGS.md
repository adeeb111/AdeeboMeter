# Editorial Briefings — Implementation Handoff

Branch: `design/editorial-briefings-2026`

Prototype: [Open the interactive HTML](./editorial-briefings.html). Open locally in a browser to inspect three pages and their interactions.

## Status

This is a functional **design prototype**, not integrated into the Windows application. It intentionally makes no network requests and does not claim live financial, sports, weather, PC or network data. All unsupported data has explicit empty states.

## Three pages

- **Markets:** financial newspaper masthead, energy and metals desks, market briefing, earnings, economic calendar, exchange sessions, local watchlist and trading readiness.
- **Sports:** existing newspaper identity, fixture selection, stadium/weather placeholders, PC/network readiness, one-link IPTV classification and preparation checklist.
- **Weather:** weather overview, hourly outlook, network trends, PC observations and daily preparation.

## Integration map (verify against current HEAD)

- Sports: `src/Ui/FullView.Sports.cs` and `src/Watch/Sports.cs`
- Weather settings: `src/Ui/FullView.Weather.cs` and `src/Weather.cs`
- Markets data: `src/Watch/Markets.cs` and current Markets UI implementation (locate before modifying)
- Streaming: `src/Ui/FullView.Stream.cs` and `src/Watch/Streams.cs`
- Reuse existing theme, GDI+ drawing, hit testing and navigation; do not transplant HTML/CSS into WinForms.
- Preserve C# 5 / .NET Framework 4.8, no admin, no listening ports, no telemetry, no new heavy dependencies.

## Follow-up tasks

1. Capture current running WinForms pages and map the prototype sections to actual canvas bounds.
2. Implement Markets briefing with verifiable sourced snapshots and explicit as-of dates; never present historical data as live quotes.
3. Implement Sports briefing with real fixture IDs, venue/timezone verification and on-demand IPTV readiness (with explicit consent, bounded requests and credential redaction).
4. Implement Weather overview using the existing Open-Meteo model and local PC/network observations.
5. Add screenshot comparisons at 100/150/200% DPI, compile and regression test.
6. Verify source terms, caching and attribution before connecting financial data providers.

## Security notes

The HTML prototype runs without external scripts or requests. The IPTV input is cleared after local URL inspection. A production IPTV engine must also prevent sensitive URLs in logs/history, control redirects, validate TLS, and limit response sizes. The prototype does not implement authenticated stream testing.

## Acceptance

All three pages render, navigation switches pages, sample controls respond, no fake live values appear, and the current desktop UI remains unchanged until explicit integration.
