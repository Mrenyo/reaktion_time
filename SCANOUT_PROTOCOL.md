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
frameTimeUs = 1 000 000 / refreshRateHz

scanoutCorrectionUs =
    ((stimulusCenterY - photodiodeCenterY) / displayHeight)
    * frameTimeUs
```

A .NET a stimulus és a képernyőn megjelenő fotodióda-jelző tényleges
vertikális középpontját használja. A képfrissítési frekvenciát az aktuális
monitortól kérdezi le.

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
