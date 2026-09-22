# Numerikus ESP protokoll

A kommunikáció mindkét irányban sortöréssel lezárt, invariáns kultúrájú
mikroszekundumos szám. Nincsenek parancsnevek, mezőnevek, ACK üzenetek vagy
verziófüggő formátumok.

## .NET → ESP

A .NET minden próba előtt kiszámítja és elküldi az előjeles
scanout-korrekciót mikroszekundumban:

```text
8333.333
```

Negatív korrekció esetén például:

```text
-9259.259
```

A számítás:

```text
frameTimeUs   = 1 000 000 / refreshRateHz
lineTimeUs    = frameTimeUs / verticalTotalLines
linesPerRow   = verticalActiveLines / displayHeightPx

scanoutCorrectionUs =
    (stimulusCenterY - photodiodeCenterY) * linesPerRow * lineTimeUs
```

A .NET a stimulus és a képernyőn megjelenő fotodióda-jelző tényleges
vertikális középpontját használja (képpontsorban, a kijelző tetejétől).

A képidőzítést a mérés elején egyszer, a Windows `QueryDisplayConfig`
API-jával kérdezi le a mérést futtató kijelzőre:

- `refreshRateHz`: a pontos, tört frekvencia (pl. 59,94 vagy 60,0031 Hz),
  nem egész számra kerekítve;
- `verticalTotalLines`: a képjel összes sora, a képkockák közti holtidővel
  (vertical blanking) együtt, pl. 1080 látható sornál 1125 vagy 1140;
- `verticalActiveLines`: a látható sorok száma; ha eltér az asztal
  magasságától (`displayHeightPx`), a videokártya skáláz, és ezt a
  `linesPerRow` szorzó kezeli.

A korábbi képlet `(Δy / displayHeight) * frameTime` volt: ez a holtidőt
figyelmen kívül hagyta, így a korrekciót a `verticalTotalLines / displayHeight`
arányban (tipikusan 4–6%-kal) túlbecsülte.

Ha a pontos időzítés nem kérdezhető le, a .NET az `EnumDisplaySettings`
egész Hz-es frekvenciájával és holtidő nélkül számol. Ezt a mérési fájl
metaadataiban a `ScanoutTimingExact=False` jelzi.

### A modell feltevései

A korrekció akkor érvényes, ha:

- a kijelző a képet soronként, fentről lefelé, állandó sorfrekvenciával
  rajzolja ki, progresszív (nem váltottsoros) képjellel és elforgatás nélkül.
  Elforgatott vagy váltottsoros kijelzőnél a .NET figyelmeztetést ír a
  naplóba és a metaadatokba (`ScanoutTimingWarning`);
- nincs bekapcsolva változó frissítési frekvencia (G-Sync / FreeSync / VRR);
- a monitor belső képfeldolgozása (skálázás, képpuffer) nem változtat a
  sorok egymáshoz képesti időzítésén. A monitor teljes bemeneti késleltetése
  nem számít, mert a fotodióda a ténylegesen kivilágító képet méri;
- az inger és a jelző ugyanabban a képkockában jelenik meg (lásd alább).

Ezek a feltevések egy fotodiódás validációs méréssel ellenőrizhetők, például
úgy, hogy az inger a kijelző tetején és alján is megjelenik.

## Fotodióda-jelző a képernyőn

- A mérési nézet keret nélküli teljes képernyőn fut. A jelző egy
  56 × 56 képpontos négyzet a mérőfelület (a sötét mérési terület, az alsó
  gombsor fölött) jobb alsó sarkában, a jobb és az alsó széltől 20-20
  képpontra. A fizikai fotodiódát erre a négyzetre, középre kell felhelyezni,
  a teljes képernyős mérési nézet megnyitása után. A négyzet körüli vékony
  szürke keret csak a felhelyezést segíti. A pontos helyet a mérési fájl
  `PhotodiodeMarkerStagePx` metaadata rögzíti.
- A jelző fekete, amíg nincs inger, és pontosan addig fehér, amíg az inger
  látszik.
- Az ingert és a jelzőt ugyanaz a rajzolás teszi ki, ugyanarra a dupla
  pufferelt felületre, egyetlen másolással. Így a kettő mindig ugyanabban a
  képkockában jelenik meg, és a korrekció képlete (amely ezt feltételezi)
  érvényes.
- Az inger soha nem kerül a jelzőre, és a biztonsági margón belül sem
  közelíti meg.

Az ESP a beérkező sort egyetlen előjeles lebegőpontos számként értelmezze.
Más szöveg nem érkezik. A korrekció előjelét meg kell őrizni.

## ESP → .NET

Az ESP a már korrigált, nemnegatív reakcióidőt küldi vissza
mikroszekundumban:

```text
250000
```

Ez 250 ms reakcióidőt jelent. A .NET kizárólag egy nemnegatív, véges,
invariáns formátumú számot fogad el. Mértékegység, előtag, mezőnév vagy
egyéb karakter hibás válasznak számít.

Az ESP számítása:

```text
correctedStimulusTimestampUs =
    rawPhotodiodeTimestampUs + scanoutCorrectionUs

reactionTimeUs =
    mouseTimestampUs - correctedStimulusTimestampUs
```

A soros kommunikáció késleltetése nem része a reakcióidőnek.
