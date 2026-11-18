# A rendszer terve

A fejezet a mérési problémát, a belőle levezetett követelményeket, a rendszer elemeit és a köztük lévő felelősségmegosztást mutatja be; az elemek részletes leírása a 4–6. fejezetben következik.

## A kiinduló mérési probléma

A számítógépes reakcióidő-mérésben a program rajzolási parancsa nem azonos a kijelzőn fizikailag megjelenő fénnyel, a PC által fogadott egéresemény pedig nem azonos a mikrokapcsoló tényleges elektromos állapotváltozásával. Két végpontot kell tehát helyesen mérni: az inger és a válasz tényleges kezdetét.

Néhány milliszekundumos pontosság mellett a reakcióidő nem számolható megbízhatóan két PC-s időbélyeg különbségeként, mert a kijelző, az operációs rendszer, a grafikus alrendszer, az inputeszköz és az eseménykezelés késleltetést és szórást visz a mérésbe. [2] [24]

!fig fig_lanc.png | 15.5 | A szoftveres időbélyegekből és a fizikai eseményekből számolt reakcióidő viszonya. A szoftveres érték a megjelenítési és az inputoldali késleltetést is tartalmazza.

A PC a rajzolás befejezésekor és az egérüzenet feldolgozásakor készít időbélyeget. A résztvevő viszont csak a fény tényleges megjelenésekor kezdhet reagálni, a válasza pedig a mikrokapcsoló zárásakor történik meg. A két szoftveres időbélyeg különbsége ezért mindig hosszabb a tényleges reakcióidőnél, és a többlet próbáról próbára változik.

## Követelmények

!table A rendszer funkcionális és nem funkcionális követelményei || 1.4,9.0,5.6
Jel | Követelmény | Megvalósítás helye
K1 | Az inger kezdetét a kijelző tényleges fényváltozása jelölje ki, ne a rajzolási parancs. | fotodióda és jelzőnégyzet (4.2., 6.5. szakasz)
K2 | A válasz időpontját a gomb elektromos jele jelölje ki, ne a PC-s egéresemény. | egérgomb-jel az ESP bemenetén (4.3. szakasz)
K3 | A két eseményt ugyanaz az óra időbélyegezze, és a reakcióidő még az adatátvitel előtt készüljön el. | ESP32-S3 firmware (5. fejezet)
K4 | A mérőlánc saját bizonytalansága legyen 1 ms alatt. | 7. fejezet (igazolandó)
K5 | A képernyőn belüli, pozíciótól függő megjelenési időeltérés legyen korrigálva. | scanout-korrekció (6.6. szakasz)
K6 | A próbák érvényessége előre rögzített szabályok szerint, automatikusan dőljön el. | `TrialEvaluator` (6.9. szakasz)
K7 | A tekintet ellenőrizhető legyen normál és periférikus feladatban is; szemkövető nélkül is lehessen mérni. | Tobii-integráció, tekintetszabályok (6.8. szakasz)
K8 | A mérés paraméterei újrafordítás nélkül állíthatók legyenek. | Testreszabás, beállításfájl (6.10. szakasz)
K9 | Minden próba adata azonnal, visszaellenőrizhető formában mentődjön, a mérés körülményeivel együtt. | CSV-fájlok metaadatokkal (6.11. szakasz)
K10 | Egy eszköz (ESP, szemkövető) kiesése ne vezessen adatvesztéshez. | hibakezelés (6.13. szakasz)
K11 | A fotodióda felhelyezése legyen gyors és reprodukálható, a környezeti fény ne zavarja. | befogó és érzékelőfej (4.5. szakasz)

A K4 a mérőlánc saját (technikai) bizonytalanságára vonatkozik, nem a résztvevő reakcióidejének szórására. Az irodalom szerint az ingerjellemzők hatása a reakcióidőre 10–20 ms nagyságrendű lehet (2.3. szakasz); kimutatásához a mérőeszköz bizonytalanságának legalább egy nagyságrenddel kisebbnek kell lennie.

## A rendszer felépítése

A rendszer öt fő eleme: a PC-n futó vezérlőalkalmazás, a kijelző, az ESP32-S3 alapú időmérő egység a két érzékelő-áramkörrel, a résztvevő egere és a szemmozgáskövető.

!fig fig_rendszer.png | 16 | A mérőrendszer blokkvázlata. A narancssárga elemek a fizikai eseményeket elektromos jellé alakító áramkörök, a kék elemek a programozott egységek.

- **PC-s alkalmazás:** vezérli a próbákat, kirajzolja az ingert és a fotodióda-jelzőt, kiszámítja a scanout-korrekciót, lekérdezi az ESP által mért reakcióidőt, eldönti a próba érvényességét, és ment. Időt csak tájékoztató céllal mér (helyi reakcióidő).
- **Kijelző:** megjeleníti az ingert és a jelzőt. A késleltetése ismeretlen és változó, ezért a rendszer a fényét méri.
- **Fotodióda-áramkör:** a jelzőnégyzet fényét feszültséggé alakítja.
- **Egér és illesztő áramkör:** a bal gomb mikrokapcsolójának állapotát logikai jelként adja az ESP-nek; az egér a PC felé változatlanul működik.
- **ESP32-S3:** a fényjelet és a gombjelet saját óráján időbélyegzi, kiszámítja a reakcióidőt, és kérésre elküldi.
- **Szemmozgáskövető:** a próba teljes ideje alatt tekintetmintákat ad, amelyekből a PC eldönti, hogy a résztvevő betartotta-e a nézési utasítást. Az időmérésben nem vesz részt.

## A mérési elv

A reakcióidő három mennyiségből számítható: a fényjel időpontjából, a gombjel időpontjából és a scanout-korrekcióból.

!eq RT_{\mathrm{ESP}} = t_{\mathrm{gomb}} - \left(t_{\mathrm{fény}} + \Delta t_{\mathrm{scanout}}\right)

A két időbélyeg az ESP órájából származik. A fotodióda az ingerrel egy képkockában megjelenő, rögzített helyű jelzőnégyzetet figyeli; az inger helye próbánként változik, ezért a kijelző soronkénti kirajzolása miatt a kettő között időeltérés van. Ezt adja meg a scanout-korrekció: negatív, ha az inger a jelző fölött van (korábban rajzolódik ki), és pozitív, ha alatta.

A megoldás három tulajdonsága:

1. A kijelző teljes bemeneti késleltetése (a videojel feldolgozása, a képkocka-várakozás, a panel válasza) kiesik, mert a fotodióda a ténylegesen kivilágító képet érzékeli.
2. Az egér USB-s jelentési ciklusa és az operációs rendszer eseménysora kiesik, mert a gombjel a PC megkerülésével jut az ESP-hez.
3. A soros kommunikáció késleltetése nem része az eredménynek, mert a reakcióidő az ESP-ben a lekérdezés előtt elkészül.

!fig fig_proba.png | 15.5 | Egy próba idődiagramja: a PC és az ESP közötti üzenetek, a jelző, a fotodióda jele, a gomb jele és a firmware állapotai.

## A legfontosabb tervezési döntések

**A reakcióidőt az ESP számítja, nem a PC.** Így a protokoll egyszerű (egyetlen szám megy vissza), és a PC-n nem marad időkritikus művelet.

**A scanout-korrekciót a PC számítja, nem az ESP.** A TMDK-dolgozatban a PC az inger pozícióját és a kijelző adatait küldte el, és az ESP számolt; a végleges változatban a PC a kész korrekciót küldi mikroszekundumban. A pontos számításhoz ugyanis a képjel részletes időzítése kell (tört frissítési frekvencia, a holtidős sorok száma, skálázás), amely csak a PC-n, a Windows megjelenítési API-jából kérdezhető le. A firmware-nek így nem kell ismernie a kijelzőt, és a képlet finomítása nem igényel firmware-frissítést.

**A fotodióda a jelzőt figyeli, nem az ingert.** Az inger helye, színe és mérete változik, a jelző viszont mindig ugyanazt a fekete → fehér átmenetet mutatja ugyanott, így az érzékelő helye és a firmware küszöbértéke rögzített lehet. Ennek ára, hogy az ingert és a jelzőt ugyanannak a rajzolási műveletnek kell kitennie (6.5. szakasz), a helyük közti időeltérést pedig a scanout-korrekció számítja.

**A próba végét a PC dönti el.** A PC a kattintást késve érzékeli; ekkor tünteti el az ingert, és ekkor kérdezi le az ESP-t, amelyben az eredmény már rendelkezésre áll. Ha az ESP nem mért (nem látott fényjelet vagy gomblenyomást), `NA` választ ad, a próba pedig a PC-n mért idővel rögzül.

**A tekintet az érvényességről dönt, nem az időről.** A szemkövető mintavételi ideje (60 Hz-es eszköznél kb. 16,7 ms) egy nagyságrenddel rosszabb az időmérő egységénél, ezért a tekintetadat nem kerül a reakcióidő képletébe. A szemkövető késleltetése legfeljebb azt befolyásolja, hogy a válaszablak szélén egy minta beleszámít-e a döntésbe.

**A méretek képpontban vannak megadva.** A korábbi változat látószögben (fokban) megadott méretekhez a kijelző fizikai méretét és a nézési távolságot is bekérte. A végleges változat minden méretet a mérést futtató képernyő fizikai képpontjaiban kezel; a látószög utólag kiszámítható, mert a metaadatok szemkövetős mérésnél a kijelző fizikai méretét és próbánként a szem távolságát is rögzítik.
