# Scanout-korrekció validálása

## Mérési körülmények

| | |
|---|---|
| Dátum | 2026-11-21 17:28:33 |
| Kijelző | MP59G (\\.\DISPLAY2), 1920 × 1080 px |
| Képfrissítés | 74,9725 Hz, képidő 13,3382 ms |
| Sorok (aktív / összes) | 1080 / 1119 (QueryDisplayConfig) |
| Soridő | 11,920 µs; egy képpontsor: 11,920 µs |
| Referencia-jelző | középpont (1872; 1032), 56 × 56 px |
| Mérő négyzet | 56 × 56 px, #FFFFFF a #000000 háttéren |
| Próbák pontonként | 50 (világos 150 ms) |
| Firmware | SV-1.0.0 (COM5); mérő ciklus átlag 93,4 µs, max 158 µs |

## Eredmények

Δt = t(mérő fotodióda) − t(referencia-jelző). A várt érték a reakcióidő-mérő scanout-képlete: Δy · linesPerRow · soridő. Az eltérés = mért átlag − várt; a 95%-os konfidenciaintervallum (KI) az átlag bizonytalansága. Az n melletti zárójeles szám a más képkockába esett (|eltérés| > fél képidő), a statisztikából kihagyott próbák száma.

| Pont | Középpont x; y (px) | Δy (px) | Várt Δt (ms) | Mért Δt átlag ± szórás (ms) | Eltérés átlaga ± 95% KI (ms) | n |
|---|---|---|---|---|---|---|
| P1 | 960; 86 | -946 | -11,276 | -10,681 ± 0,165 | 0,595 ± 0,047 | 50 |
| P2 | 480; 324 | -708 | -8,439 | -7,977 ± 0,192 | 0,462 ± 0,055 | 50 |
| P3 | 960; 540 | -492 | -5,865 | -5,497 ± 0,159 | 0,367 ± 0,045 | 50 |
| P4 | 1440; 778 | -254 | -3,028 | -2,657 ± 0,132 | 0,371 ± 0,037 | 50 |
| P5 | 192; 1032 | 0 | 0,000 | 0,281 ± 0,107 | 0,281 ± 0,030 | 50 |

- Érvényes próbák: 250 / 250; más képkockába esett: 0.
- Regresszió (mért Δt a várt függvényében, minden érvényes próba): meredekség 0,9744, tengelymetszet 0,269 ms, R² = 0,99837.
- Az eltérés négyzetes középértéke (RMS) próbánként: 0,455 ms; a pontátlagok legnagyobb abszolút eltérése: 0,595 ms.
- Nullpont (P5, Δy = 0): eltérés 0,281 ms; ez a két érzékelőlánc állandó eltolása. Ezzel csökkentve az eltérés: P1: 0,313 ms, P2: 0,180 ms, P3: 0,086 ms, P4: 0,090 ms.
- Kiegészítés (a küszöbátlépés lineáris interpolációja a két szomszédos minta között): eltérés átlaga pontonként P1: 0,599 ms, P2: 0,472 ms, P3: 0,370 ms, P4: 0,377 ms, P5: 0,261 ms.

## A szakdolgozat formátumában

```text
!table A scanout-korrekció validálása két fotodiódával (2026. november 21.; MP59G, 1920 × 1080, 74,973 Hz) || 1.4,2.6,1.6,2.2,3.2,3.6,1.4
Pont | Középpont x; y (px) | Δy (px) | Várt Δt (ms) | Mért Δt átlag ± szórás (ms) | Eltérés átlaga ± 95% KI (ms) | n
P1 | 960; 86 | -946 | -11,276 | -10,681 ± 0,165 | 0,595 ± 0,047 | 50
P2 | 480; 324 | -708 | -8,439 | -7,977 ± 0,192 | 0,462 ± 0,055 | 50
P3 | 960; 540 | -492 | -5,865 | -5,497 ± 0,159 | 0,367 ± 0,045 | 50
P4 | 1440; 778 | -254 | -3,028 | -2,657 ± 0,132 | 0,371 ± 0,037 | 50
P5 | 192; 1032 | 0 | 0,000 | 0,281 ± 0,107 | 0,281 ± 0,030 | 50
```

Ábra: `abra.png` (felül a mért Δt a képernyő-pozíció függvényében a várt egyenessel, alul az eltérés).
