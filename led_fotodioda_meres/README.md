# LED → fotodióda késleltetés

Önálló ESP-IDF program, amely a LED bekapcsolásától a fotodióda-jel küszöbátlépéséig eltelt időt
méri. A reakcióidő-mérőtől független.

## Működés

`python mentes.py COM5` → az ESP `MERESEK_SZAMA`-szor:
LED be → ADC-olvasás, amíg a jel el nem éri a `KUSZOB`-ot → a LED még `VILAGIT_MS`-ig ég → LED ki →
`SZUNET_MS` szünet. Minden időt (µs, `-1` = 1 s alatt nem jött fény) azonnal elküld; a Python
soronként a `meres_<dátum>.txt` fájlba menti.

| Beállítás (`firmware/main/main.c`) | Érték |
|---|---|
| `MERESEK_SZAMA` | 500 |
| `KUSZOB` | 150 (nyers ADC) |
| `VILAGIT_MS` / `SZUNET_MS` | 1000 / 500 (egy futás kb. 12,5 perc) |
| LED / fotodióda | GPIO15 / GPIO4 (ADC1 CH3) |

Az ADC-t a mérő ciklus közvetlenül a regiszterből olvassa (`gyors_olvas()`): egy olvasás kb. 6,4 µs,
az `adc_oneshot_read()`-é kb. 44 µs. A beállítást és a kalibrálást induláskor egy
`adc_oneshot_read()` végzi.

Feltöltés (ESP-IDF v5.5): `cd firmware; idf.py set-target esp32s3; idf.py -p COM5 flash`

## Eredmény (`meresek/`)

| Fájl | Mérés | Értékek |
|---|---|---|
| `meres_20261125_194719.txt` | 500 | 497 × 15 µs, 2 × 13 µs (az első kettő), 1 × 8 µs |
| `meres_20261125_200449.txt` | 500 | 499 × 15 µs, 1 × 16 µs |

A LED–fotodióda lánc késleltetése legfeljebb 15 µs, a felbontás egy ADC-olvasás (6,4 µs). Mivel
minden mérés ugyanabban az ütemben indul, a valódi küszöbátlépés kb. 8,6 és 15 µs közé esik; ezt
több mérés nem szűkíti. A kilógó értékek: bemelegedés (első futások) és egy-egy szobafény-csúcs.
Ez a saját LED és fotodióda-áramkör késése, a kijelzőé nem.
