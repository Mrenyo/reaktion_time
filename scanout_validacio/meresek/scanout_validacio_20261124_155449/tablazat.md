# Scanout-korrekció validálása

## Mérési körülmények

| | |
|---|---|
| Dátum | 2026-11-24 15:54:49 |
| Kijelző | MP59G (\\.\DISPLAY2), 1920 × 1080 px |
| Képfrissítés | 74,9725 Hz, képidő 13,3382 ms |
| Sorok (aktív / összes) | 1080 / 1119 (QueryDisplayConfig) |
| Soridő | 11,920 µs; egy képpontsor: 11,920 µs |
| Referencia-jelző | középpont (1872; 1032), 56 × 56 px |
| Mérő négyzet | 56 × 56 px, #FFFFFF a #000000 háttéren |
| Próbák pontonként | 50 (világos 150 ms) |
| Firmware | SV-1.0.0 (COM5); mérő ciklus átlag 93,5 µs, max 148 µs |

## Eredmények

Δt = t(mérő fotodióda) − t(referencia-jelző). A várt érték a reakcióidő-mérő scanout-képlete: Δy · linesPerRow · soridő. Az eltérés = mért átlag − várt; a 95%-os konfidenciaintervallum (KI) az átlag bizonytalansága. Az n melletti zárójeles szám a más képkockába esett (|eltérés| > fél képidő), a statisztikából kihagyott próbák száma.

| Pont | Középpont x; y (px) | Δy (px) | Várt Δt (ms) | Mért Δt átlag ± szórás (ms) | Eltérés átlaga ± 95% KI (ms) | n |
|---|---|---|---|---|---|---|
| P1 | 960; 86 | -946 | -11,276 | -11,095 ± 0,144 | 0,181 ± 0,041 | 49 |
| P2 | 480; 324 | -708 | -8,439 | -8,282 ± 0,141 | 0,157 ± 0,040 | 49 |
| P3 | 960; 540 | -492 | -5,865 | -6,005 ± 0,134 | -0,141 ± 0,038 | 50 |
| P4 | 1440; 778 | -254 | -3,028 | -3,232 ± 0,123 | -0,205 ± 0,035 | 49 |
| P5 | 192; 1032 | 0 | 0,000 | -0,232 ± 0,152 | -0,232 ± 0,044 | 49 |

- Érvényes próbák: 246 / 250; más képkockába esett: 0.
- Regresszió (mért Δt a várt függvényében, minden érvényes próba): meredekség 0,9579, tengelymetszet -0,289 ms, R² = 0,99836.
- Az eltérés négyzetes középértéke (RMS) próbánként: 0,231 ms; a pontátlagok legnagyobb abszolút eltérése: 0,232 ms.
- Nullpont (P5, Δy = 0): eltérés -0,232 ms; ez a két érzékelőlánc állandó eltolása. Ezzel csökkentve az eltérés: P1: 0,413 ms, P2: 0,389 ms, P3: 0,091 ms, P4: 0,027 ms.
- Kiegészítés (a küszöbátlépés lineáris interpolációja a két szomszédos minta között): eltérés átlaga pontonként P1: 0,191 ms, P2: 0,158 ms, P3: -0,124 ms, P4: -0,192 ms, P5: -0,217 ms.

## A szakdolgozat formátumában

```text
!table A scanout-korrekció validálása két fotodiódával (2026. november 24.; MP59G, 1920 × 1080, 74,973 Hz) || 1.4,2.6,1.6,2.2,3.2,3.6,1.4
Pont | Középpont x; y (px) | Δy (px) | Várt Δt (ms) | Mért Δt átlag ± szórás (ms) | Eltérés átlaga ± 95% KI (ms) | n
P1 | 960; 86 | -946 | -11,276 | -11,095 ± 0,144 | 0,181 ± 0,041 | 49
P2 | 480; 324 | -708 | -8,439 | -8,282 ± 0,141 | 0,157 ± 0,040 | 49
P3 | 960; 540 | -492 | -5,865 | -6,005 ± 0,134 | -0,141 ± 0,038 | 50
P4 | 1440; 778 | -254 | -3,028 | -3,232 ± 0,123 | -0,205 ± 0,035 | 49
P5 | 192; 1032 | 0 | 0,000 | -0,232 ± 0,152 | -0,232 ± 0,044 | 49
```

Ábra: `abra.png` (felül a mért Δt a képernyő-pozíció függvényében a várt egyenessel, alul az eltérés).
