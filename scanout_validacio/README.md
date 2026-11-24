# Scanout-korrekció validálása két fotodiódával

Önálló validációs eszköz a repó részeként: saját PC-programmal és saját firmware-rel, a
reakcióidő-mérőtől függetlenül. A mérőprogram fordításából ki van zárva
(`DefaultItemExcludes` a `ReactionTimeMeasurementApp.csproj`-ban). Azt ellenőrzi, hogy a
reakcióidő-mérő scanout-korrekciója ([docs/SCANOUT_PROTOCOL.md](../docs/SCANOUT_PROTOCOL.md))
megegyezik-e a kijelzőn ténylegesen mért időkülönbséggel.

> **Firmware-csere:** a validációhoz a `firmware/` mappa validációs firmware-ét kell az ESP-re
> tölteni (verzió: `SV-…`). A reakcióidő-mérő ezt a verzió alapján elutasítja, ezért mérés előtt
> vissza kell tölteni a mérőprogram firmware-ét (`../firmware`, `idf.py -p COM5 flash`).

**Elv.** A referencia-fotodióda a szokásos jelzőn van (jobb alsó sarok), a mérő fotodióda egy
vizsgált képernyőponton. A két négyzet **ugyanabban a rajzolásban** vált feketéről fehérre. Az ESP
mindkét fényjelet időbélyegzi, a mért különbséget (Δt = t_mérő − t_ref) a program összeveti a
scanout-képletből várt értékkel: `(y_pont − y_ref) · linesPerRow · soridő`.

| Mappa | Tartalom |
|---|---|
| `firmware/` | ESP-IDF projekt (ESP32-S3, a reakcióidő-mérő `sdkconfig.defaults`-ával) |
| `pc/` | .NET 10 WinForms mérőprogram és elemző |

## Ugyanaz a módszer, mint a reakcióidő-mérőben

| | Reakcióidő-mérő | Validáció |
|---|---|---|
| Fényjel | oneshot ADC, első küszöbátlépés sötét minta után | ugyanaz, mindkét csatornán |
| Időbélyeg | `esp_timer`, közvetlenül a küszöböt elérő olvasás után | ugyanaz, csatornánként a saját olvasása után |
| Mérő ciklus | külön task az 1-es magon, 240 MHz | ugyanaz; a két csatorna felváltva olvasva |
| Küszöb | 475 | alapból 475, csatornánként állítható |
| Képidőzítés | `QueryDisplayConfig` (tört Hz, összes sor, skálázás) | ugyanaz a kód |
| Rajzolás | WinForms, kettős puffer, `Invalidate` + `Update()` | ugyanaz |
| Jelző | 56 × 56 px, széltől 20 px, 16 px fekete védősáv | ugyanaz |

A PC-oldal ezért .NET: a validáció azt a rajzolási utat és képletet méri, amelyet a mérőprogram
használ. Mivel a két csatornát felváltva olvassa, a ciklus kb. kétszer olyan hosszú, mint a
reakcióidő-mérőben. Ez a felbontás (`CiklusAtlagUs`) próbánként véletlenszerű, nulla várható értékű
hibát okoz, amely az átlagokban kiátlagolódik.

## Bekötés

| Jel | ESP32-S3 láb |
|---|---|
| Referencia-fotodióda erősítőjének kimenete | GPIO4 (ADC1 CH3), mint eddig |
| Mérő fotodióda erősítőjének kimenete | **GPIO6 (ADC1 CH5)** |

Más láb a `firmware/main/main.c` `FD_MERO` állandójában adható meg (csak ADC1: GPIO1–10). A gomb
nem kell.

## Fordítás, feltöltés, futtatás

ESP-IDF környezetben (v5.5):

```powershell
cd firmware; idf.py set-target esp32s3; idf.py -p COM5 flash
```

PC:

```powershell
cd pc; dotnet run -c Release
```

Az első indítás létrehozza a `pc/beallitasok.json` fájlt (a munkakönyvtárban). Fontosabb mezők:

| Mező | Alapérték | Jelentés |
|---|---|---|
| `Port` | `""` | üres: automatikus keresés (a `VERSION` válasza `SV-`-vel kezdődik) |
| `Display` | `""` | üres: elsődleges kijelző; különben pl. `\\.\DISPLAY2` |
| `TrialsPerPoint` | 50 | próbák pontonként |
| `Points` | P1–P5 | a mérő négyzet középpontja a képernyő arányában; `"Y": null` = a referencia sorában |
| `ThresholdReference`, `ThresholdMeasurement` | 475 | küszöbök (a mérés közben `[K]`-val is állítható) |
| `PointColor`, `BackgroundColor` | `#FFFFFF`, `#000000` | más színpár vizsgálatához |
| `DarkMinMs`–`DarkMaxMs`, `LitDurationMs` | 300–700, 150 | sötét szakasz (véletlen), világos szakasz |

Az alap pontok: P1 (y = 8%), P2 (30%), P3 (50%), P4 (72%), P5 (a referencia sorában, bal oldalon).
A P5 **nullpont**: a várt Δt = 0, így a mért értéke a két érzékelőlánc állandó eltolása.
Az X-ek eltérnek, így az is látszik, hogy a késés nem függ a vízszintes helyzettől.

## A mérés menete

1. A kijelzőn kapcsold ki a G-Sync/FreeSync/VRR funkciót, és használd a natív felbontást (mint a mérésnél).
2. Indítsd el a programot. Először az **érzékelő-teszt** jelenik meg: mindkét fotodióda nyers ADC-értéke
   élőben (referencia és mérő). `[L]` világosra/sötétre kapcsolja a négyzeteket; a program kiszámolja
   mindkét csatorna sötét és világos szintjét, és küszöbjavaslatot ad (a két szint fele). A küszöb a
   világos szint alatt kell legyen (az alap 475 csak ~950-es világos szintnél jó). `[Space]` tovább.
3. Az első pont felhelyező nézete: a két négyzetnek szürke kerete van, a mérő négyzeten célkereszt
   fut át. Tedd a mérő fotodiódát a keretezett négyzetre, és az `[L]`-lel mérd meg újra a szinteket
   (a mérő fotodióda helyzete változott). A `[Space]` csak akkor indít, ha mindkét szint ismert, és
   a küszöböket a mért szintek felére állítja (`[K]` ugyanezt előre is megteszi).
4. `[Space]` elindítja a próbákat. Egy próba: sötét szakasz → `ARM` → mindkét négyzet egy
   rajzolásban fehér → sötét → `RESULT`. A haladás halvány szövegként látszik, az érzékelőktől
   legtávolabbi negyedben.
5. Utána jön a következő pont. `[S]` kihagyja a pontot, `[Esc]` befejezi a mérést; amit addig
   mértél, az elemzésbe kerül.

## Kimenet

`pc/meresek/scanout_validacio_ÉÉÉÉHHNN_ÓÓPPMM/`:

| Fájl | Tartalom |
|---|---|
| `nyers.csv` | minden próba: várt Δt, mért Δt, a két nyers időbélyeg és az előző minta ideje/értéke, küszöbök, ciklusidő, ADC-tartományok |
| `meta.json`, `beallitasok.json` | a kijelző időzítése, a geometria, a firmware verziója, a beállítások |
| `osszesites.csv` | pontonként: n, várt, átlag, szórás, min, max, medián, eltérés és 95% KI |
| `tablazat.md` | a körülmények és az eredmények táblázata, regresszió, nullpont-korrekció, valamint a **szakdolgozat `!table` formátumában** bemásolható blokk |
| `abra.png` | felül a mért Δt a sor függvényében a várt egyenessel, alul az eltérés (µs) pontonként, 95% KI-vel |

Újraelemzés (pl. az elemzés módosítása után), mérés nélkül:

```powershell
cd pc; dotnet run -c Release -- --elemzes "meresek\scanout_validacio_20261120_140000"
```

### Az elemzés szabályai

- A fő eredmény a reakcióidő-mérővel azonos módszerű Δt: a két olvasás utáni időbélyeg különbsége.
  Kiegészítésként a küszöbátlépés lineáris interpolációval becsült idejéből számolt eltérés is szerepel.
- **Más képkocka:** ha |mért − várt| > fél képidő, a két négyzet nem ugyanabban a képkockában jelent
  meg. Ezeket a program külön számolja (a táblázatban `n (+k)`), a statisztikából kihagyja.
  Ha ilyen nincs, az igazolja a modell „egy képkocka” feltevését.
- Regresszió a mért Δt-re a várt függvényében, minden érvényes próbára: a meredekség 1-től való
  eltérése a soridő (a modell) hibája, a tengelymetszet az érzékelőláncok állandó eltolása.

## Mérések a repóban

A [`meresek/`](meresek) mappában a szakdolgozathoz használt három teljes mérés van (MP59G,
1920 × 1080, 74,97 Hz, 5 pont × 50 próba). A program új méréseit a `pc/meresek` mappába írja,
azt a git kihagyja.

| Mappa | Dátum |
|---|---|
| `scanout_validacio_20261121_172833` | 2026-11-21 17:28 |
| `scanout_validacio_20261124_140134` | 2026-11-24 14:01 |
| `scanout_validacio_20261124_155449` | 2026-11-24 15:54 |

## Soros protokoll (UART0, 115200, soronként)

| Parancs | Válasz |
|---|---|
| `CHECK` | `OK` |
| `VERSION` | `SV-1.0.0` |
| `THR <ref> <mérő>` | `THR <ref> <mérő>` |
| `ADC` | `ADC <ref> <mérő>`, vagy `NA` próba közben |
| `ARM` | `ARMED` (új próba, mindkét csatorna a fekete → fehér átmenetre vár) |
| `RESULT` | `RES dt=… t_ref=… tp_ref=… v_ref=… vp_ref=… t_mero=… … ciklus=… atlag_us=… max_us=…`, vagy `RES NA ref=0/1 mero=0/1 …`; utána az ESP tétlen |
| `STOP` | `STOPPED` |
