# A rendszer terve

Ez a fejezet a mérőrendszer egészét mutatja be: a kiinduló mérési problémát, az ebből levezetett követelményeket, a rendszer elemeit és a köztük lévő felelősségmegosztást. Az egyes elemek részletes leírása a 4–6. fejezetben következik.

## A kiinduló mérési probléma

A reakcióidőmérés látszólag egyszerű feladat: megjelenik egy vizuális jel, a vizsgálati személy reagál, majd a rendszer kiszámítja a két esemény közötti időt. A számítógépes mérésben azonban a két esemény nem ott történik, ahol elsőre gondolnánk. A program által kiadott rajzolási parancs nem azonos a kijelzőn fizikailag megjelenő fénnyel, és a PC által fogadott egéresemény sem azonos a mikrokapcsoló tényleges elektromos állapotváltozásával. A mérési láncban ezért két külön problémát kell kezelni: az inger tényleges kezdetét és a válasz tényleges kezdetét.

A projekt alapfeltevése az, hogy a reakcióidő nem számolható megbízhatóan pusztán két PC-s időbélyeg különbségeként, ha a cél néhány milliszekundumos nagyságrendű pontosság. A kijelző, az operációs rendszer, a grafikus alrendszer, az inputeszköz és az eseménykezelés mind késleltetést vagy szórást vihet a mérésbe. Ha a két végpont rossz helyen van mérve, akkor a kapott reakcióidő nem a felhasználó észlelési-válasz idejét, hanem a teljes PC-s mérőláncot is tartalmazza. [2] [24]

!fig fig_lanc.png | 15.5 | A szoftveres időbélyegekből és a fizikai eseményekből számolt reakcióidő viszonya. A szoftveres érték a megjelenítési és az inputoldali késleltetést is tartalmazza.

Az ábra a két megközelítés különbségét mutatja. A PC a rajzolás befejezésekor és az egérüzenet feldolgozásakor tud időbélyeget készíteni. A résztvevő ezzel szemben csak akkor kezdhet reagálni, amikor az inger fénye ténylegesen megjelent, a válasza pedig akkor történik meg, amikor a mikrokapcsoló zár. A két szoftveres időbélyeg különbsége ezért mindig hosszabb a tényleges reakcióidőnél, és a többlet próbáról próbára változik.

## Követelmények

A mérési problémából és a célkitűzésekből az alábbi követelmények következnek.

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

A K4 követelmény a mérőlánc saját (technikai) bizonytalanságára vonatkozik, nem a résztvevő reakcióidejének szórására. Az 1 ms-os határ azért indokolt, mert az irodalom szerint az ingerjellemzők hatása a reakcióidőre 10–20 ms nagyságrendű lehet (2.3. szakasz), ennek megbízható kimutatásához a mérőeszköz bizonytalanságának legalább egy nagyságrenddel kisebbnek kell lennie.

## A rendszer felépítése

A rendszer öt fő elemből áll: a PC-n futó vezérlőalkalmazásból, a kijelzőből, az ESP32-S3 alapú időmérő egységből a két érzékelő-áramkörrel, a résztvevő által használt egérből és a szemmozgáskövetőből.

!fig fig_rendszer.png | 16 | A mérőrendszer blokkvázlata. A narancssárga elemek a fizikai eseményeket elektromos jellé alakító áramkörök, a kék elemek a programozott egységek.

A felelősségek megosztása a következő:

- **PC-s alkalmazás:** előkészíti és vezérli a próbákat, kirajzolja az ingert és a fotodióda-jelzőt, kiszámítja a scanout-korrekciót, a mérés végén lekérdezi az ESP által mért reakcióidőt, eldönti a próba érvényességét, és ment. Időt csak tájékoztató céllal mér (helyi reakcióidő).
- **Kijelző:** megjeleníti az ingert és a jelzőt. A késleltetése ismeretlen és változó, ezért a rendszer nem feltételez róla semmit, hanem a fényét méri.
- **Fotodióda-áramkör:** a jelzőnégyzet fényét feszültséggé alakítja.
- **Egér és illesztő áramkör:** a bal gomb mikrokapcsolójának állapotát logikai jelként adja az ESP-nek. Az egér közben változatlanul működik a PC felé.
- **ESP32-S3:** a fényjelet és a gombjelet saját óráján időbélyegzi, kiszámítja a reakcióidőt, és kérésre elküldi.
- **Szemmozgáskövető:** a próba teljes ideje alatt tekintetmintákat ad; ezekből a PC dönti el, hogy a résztvevő betartotta-e a nézési utasítást. Az időmérésben nem vesz részt.

## A mérési elv

A reakcióidő számítása három mennyiségre épül: a fényjel időpontjára, a gombjel időpontjára és a scanout-korrekcióra.

!eq RT_{\mathrm{ESP}} = t_{\mathrm{gomb}} - \left(t_{\mathrm{fény}} + \Delta t_{\mathrm{scanout}}\right)

A képletben a két időbélyeg az ESP órájából származik. A fotodióda nem magát az ingert, hanem az ingerrel egy képkockában megjelenő jelzőnégyzetet figyeli. A jelző helye rögzített, az ingeré próbánként változik, ezért a kettő között a kijelző soronkénti kirajzolása miatt időeltérés van. Ezt az eltérést adja meg a scanout-korrekció: negatív, ha az inger a jelző fölött van (korábban rajzolódik ki), és pozitív, ha alatta.

A megoldás három tulajdonsága miatt alkalmas a kitűzött célra:

1. A kijelző teljes bemeneti késleltetése (a videojel feldolgozása, a képkocka-várakozás, a panel válasza) kiesik a mérésből, mert a fotodióda a ténylegesen kivilágító képet érzékeli.
2. Az egér USB-s jelentési ciklusa és az operációs rendszer eseménysora kiesik a mérésből, mert a gombjel az ESP-hez közvetlenül, a PC megkerülésével jut el.
3. A soros kommunikáció késleltetése nem része az eredménynek, mert a reakcióidő az ESP-ben már elkészült, mielőtt a PC lekérdezi.

!fig fig_proba.png | 15.5 | Egy próba idődiagramja: a PC és az ESP közötti üzenetek, a jelző, a fotodióda jele, a gomb jele és a firmware állapotai.

## A legfontosabb tervezési döntések

**A reakcióidőt az ESP számítja, nem a PC.** Ha az ESP csak időbélyegeket küldene, és a különbséget a PC képezné, az eredmény ugyanaz lenne; a helyi számítás előnye az, hogy a protokoll egyszerű marad (egyetlen szám megy vissza), és a PC-n semmilyen időkritikus művelet nem marad.

**A scanout-korrekciót a PC számítja, nem az ESP.** A TMDK-dolgozatban leírt változatban a PC az inger pozícióját és a kijelző adatait küldte el, és az ESP számolt. A végleges változatban a PC a kész korrekciót küldi mikroszekundumban. Ennek oka, hogy a pontos számításhoz a kijelző képjelének részletes időzítése kell (tört frissítési frekvencia, a holtidős sorok száma, skálázás), amely csak a PC-n, a Windows megjelenítési API-jából kérdezhető le. Így a firmware-nek nem kell ismernie a kijelzőt, és a képlet finomítása nem igényel firmware-frissítést.

**A fotodióda a jelzőt figyeli, nem az ingert.** Az inger helye, színe és mérete próbánként, illetve beállításonként változik, a jelző viszont mindig ugyanazt a fekete → fehér átmenetet mutatja ugyanott. Az érzékelő így rögzített helyre kerülhet, és a firmware küszöbértéke minden beállítás mellett ugyanúgy működik. Ennek ára, hogy az inger és a jelző megjelenése között kapcsolatot kell teremteni: egyrészt a kettőt ugyanaz a rajzolási művelet teszi ki (6.5. szakasz), másrészt a helyük közti időeltérést a scanout-korrekció számítja.

**A próba végét a PC dönti el.** A résztvevő kattintását a PC is érzékeli (késve), ekkor tünteti el az ingert, és ekkor kérdezi le az ESP-t. Az ESP a gombot a PC-nél korábban érzékeli, így a lekérdezéskor az eredmény már rendelkezésre áll. Ha az ESP nem mért (nem látott fényjelet vagy gomblenyomást), ezt `NA` válasszal jelzi, a próba pedig a PC-n mért idővel rögzül.

**A tekintet az érvényességről dönt, nem az időről.** A szemkövető mintavételi ideje (60 Hz-es eszköznél kb. 16,7 ms) egy nagyságrenddel rosszabb, mint az időmérő egységé, ezért a tekintetadat nem kerül be a reakcióidő képletébe. A szemkövető késleltetése legfeljebb azt befolyásolja, hogy egy minta a válaszablak szélén beleszámít-e a döntésbe.

**A méretek képpontban vannak megadva.** A korábbi változat látószögben (fokban) megadott méretekkel dolgozott, amihez a kijelző fizikai méretét és a nézési távolságot is meg kellett adni. A végleges változat minden méretet a mérést futtató képernyő fizikai képpontjaiban kezel. Ez egyszerűbb és egyértelműbb beállítást ad, a látószög pedig utólag kiszámítható, mert a metaadatok szemkövetős mérésnél a kijelző fizikai méretét és próbánként a szem távolságát is rögzítik.
