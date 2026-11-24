# Scanout-korrekció validálása

## Mérési körülmények

| | |
|---|---|
| Dátum | 2026-11-24 14:01:34 |
| Kijelző | MP59G (\\.\DISPLAY2), 1920 × 1080 px |
| Képfrissítés | 74,9725 Hz, képidő 13,3382 ms |
| Sorok (aktív / összes) | 1080 / 1119 (QueryDisplayConfig) |
| Soridő | 11,920 µs; egy képpontsor: 11,920 µs |
| Referencia-jelző | középpont (1872; 1032), 56 × 56 px |
| Mérő négyzet | 56 × 56 px, #FFFFFF a #000000 háttéren |
| Próbák pontonként | 50 (világos 150 ms) |
| Firmware | SV-1.0.0 (COM5); mérő ciklus átlag 93,4 µs, max 151 µs |

## Eredmények

Δt = t(mérő fotodióda) − t(referencia-jelző). A várt érték a reakcióidő-mérő scanout-képlete: Δy · linesPerRow · soridő. Az eltérés = mért átlag − várt; a 95%-os konfidenciaintervallum (KI) az átlag bizonytalansága. Az n melletti zárójeles szám a más képkockába esett (|eltérés| > fél képidő), a statisztikából kihagyott próbák száma.

| Pont | Középpont x; y (px) | Δy (px) | Várt Δt (ms) | Mért Δt átlag ± szórás (ms) | Eltérés átlaga ± 95% KI (ms) | n |
|---|---|---|---|---|---|---|
| P1 | 960; 86 | -946 | -11,276 | -11,081 ± 0,363 | 0,195 ± 0,103 | 50 |
| P2 | 480; 324 | -708 | -8,439 | -8,085 ± 0,224 | 0,354 ± 0,064 | 50 |
| P3 | 960; 540 | -492 | -5,865 | -5,572 ± 0,201 | 0,292 ± 0,057 | 50 |
| P4 | 1440; 778 | -254 | -3,028 | -2,930 ± 0,167 | 0,097 ± 0,048 | 49 |
| P5 | 192; 1032 | 0 | 0,000 | -0,011 ± 0,137 | -0,011 ± 0,039 | 49 |

- Érvényes próbák: 248 / 250; más képkockába esett: 0.
- Regresszió (mért Δt a várt függvényében, minden érvényes próba): meredekség 0,9760, tengelymetszet 0,048 ms, R² = 0,99590.
- Az eltérés négyzetes középértéke (RMS) próbánként: 0,324 ms; a pontátlagok legnagyobb abszolút eltérése: 0,354 ms.
- Nullpont (P5, Δy = 0): eltérés -0,011 ms; ez a két érzékelőlánc állandó eltolása. Ezzel csökkentve az eltérés: P1: 0,206 ms, P2: 0,365 ms, P3: 0,303 ms, P4: 0,108 ms.
- Kiegészítés (a küszöbátlépés lineáris interpolációja a két szomszédos minta között): eltérés átlaga pontonként P1: 0,215 ms, P2: 0,367 ms, P3: 0,300 ms, P4: 0,107 ms, P5: -0,001 ms.

## A szakdolgozat formátumában

```text
!table A scanout-korrekció validálása két fotodiódával (2026. november 24.; MP59G, 1920 × 1080, 74,973 Hz) || 1.4,2.6,1.6,2.2,3.2,3.6,1.4
Pont | Középpont x; y (px) | Δy (px) | Várt Δt (ms) | Mért Δt átlag ± szórás (ms) | Eltérés átlaga ± 95% KI (ms) | n
P1 | 960; 86 | -946 | -11,276 | -11,081 ± 0,363 | 0,195 ± 0,103 | 50
P2 | 480; 324 | -708 | -8,439 | -8,085 ± 0,224 | 0,354 ± 0,064 | 50
P3 | 960; 540 | -492 | -5,865 | -5,572 ± 0,201 | 0,292 ± 0,057 | 50
P4 | 1440; 778 | -254 | -3,028 | -2,930 ± 0,167 | 0,097 ± 0,048 | 49
P5 | 192; 1032 | 0 | 0,000 | -0,011 ± 0,137 | -0,011 ± 0,039 | 49
```

Ábra: `abra.png` (felül a mért Δt a képernyő-pozíció függvényében a várt egyenessel, alul az eltérés).
