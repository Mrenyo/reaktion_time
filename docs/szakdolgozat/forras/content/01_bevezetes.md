# Bevezetés és célkitűzések

A reakcióidő az észlelési és válaszadási folyamatok időbeli jellemzésére szolgál. Számítógépes mérésnél a kijelző megjelenítési sajátosságai, az operációs rendszer időzítési bizonytalansága, a grafikus feldolgozás és a beviteli eszköz késése miatt a kizárólag szoftveres időbélyegekre épülő megoldások pontossága korlátozott. [1]

A résztvevő a kijelzőn fizikailag megjelenő fényváltozásra reagál, nem a program rajzolási parancsára, és a program által fogadott egéresemény sem azonos a kattintás fizikai kezdetével. A mérés pontossága ezért azon múlik, hogy a rendszer hol jelöli ki az inger és a válasz időpontját. [1] [2] [3]

A projekt célja olyan reakcióidőmérő rendszer, amely a vizuális inger (stimulus) tényleges megjelenését fotodiódával érzékeli, a kattintás fizikai időpontját mikrokontrollerrel időbélyegzi, a próbák érvényességét pedig Tobii szemmozgáskövetővel ellenőrzi, vagyis azt, hogy a résztvevő az inger megjelenésekor a megfelelő területre nézett-e.

## Célkitűzések

Az elméleti cél a reakcióidőmérés és a rendszerkésleltetés kapcsolatának bemutatása: a megjelenítési és az inputkésleltetés, a szoftveres időbélyegzés korlátai és a false-pozitív reakcióidőmérés áttekintése. A gyakorlati célkitűzések:

- olyan időmérő egység, amely az inger fizikai megjelenését és a válasz elektromos jelét ugyanazon, a PC-től független időalapon rögzíti, és a reakcióidőt helyben számítja ki;
- a kijelző soronkénti kirajzolásából adódó, képernyőpozíciótól függő időeltérés (scanout) számítása és korrekciója;
- olyan PC-s alkalmazás, amely a mérést vezérli, az ingereket megjeleníti, a próbák érvényességét előre rögzített szabályok szerint eldönti, és az eredményt visszaellenőrizhető formában tárolja;
- a mérés paramétereinek testreszabhatósága többféle kísérleti elrendezéshez;
- a tekintet ellenőrzése szemmozgáskövetővel, normál (ingerre néző) és periférikus (fixációt tartó) feladatban;
- tartós, reprodukálhatóan felszerelhető mérőhardver: nyomtatott áramkör, ház és a fotodiódát a kijelzőn tartó befogó.

A dolgozat nem a pszichológiai teljesítményt minősíti, hanem a mérési feltételeket tisztázza: az inger fizikai kezdetének, a válasz fizikai kezdetének és a próba érvényességének elkülöníthetően kezelhetőnek kell lennie.

## Előzmények: a TMDK-dolgozat és az azóta történt változások

A dolgozat a „Reakcióidőmérés Tobii szemmozgáskövetővel megtámogatva” című TMDK-dolgozatra épül. Az irodalmi áttekintés (2. fejezet) annak szövegét követi. A megvalósítás azonban a TMDK-dolgozat leadása óta több ponton megváltozott; a jelen dolgozat a végleges állapotot írja le. A legfontosabb eltéréseket az alábbi táblázat foglalja össze.

!table A TMDK-dolgozatban leírt és a végleges megvalósítás eltérései || 3.3,5.6,7.1
Terület | TMDK-dolgozat | Végleges megvalósítás
Mikrokontroller | ESP32-S3 fejlesztőpanel (DevKitC jellegű) | ESP32-S3 N16R8 (16 MB flash, 8 MB PSRAM) fejlesztőlap, saját hordozó NYÁK-on
Jelek bekötése | a fotodióda és az egér jele közvetlenül az ESP lábaira kötve | illesztő áramkörök: a fotodióda transzimpedancia-erősítőn, az egérgomb jele illesztő fokozaton keresztül, nyomtatott áramkörön
Firmware | nem részletezett | ESP-IDF 5.5 keretrendszer, C nyelv; a mérés külön, processzormaghoz rögzített feladatban fut; verziólekérdezés és diagnosztikai parancsok
Scanout-korrekció | az ESP számítja az inger pozíciójából | a PC számítja a kijelző pontos képidőzítéséből, és kész értékként (µs) küldi el
Soros protokoll | próbaazonosító, pozíció, kijelzőadatok; az ESP magától küld eredményt | `CHECK` → `OK`, `VERSION`, próbánként egy korrekciós érték, a reakcióidőt a PC `RTIME` kéréssel kérdezi le
Mérési módok | két rögzített mód (direkt, perifériás) | egyetlen, testreszabható mérés: három tekintet-ellenőrzési mód, kétféle válaszmód, állítható időzítés és megjelenés
Érvénytelen próbák | – | előre rögzített okok szerint megjelölve, pótlás nélkül
Mechanika | – | 3D nyomtatott ház az elektronikának, állítható befogó a fotodiódának
Adattárolás | statisztika a programban | próbánkénti CSV metaadatokkal, külön fájl a tekintetmintáknak

## A dolgozat felépítése

A 2. fejezet az irodalmi hátteret tekinti át. A 3. fejezet a rendszer követelményeit és felépítését, a 4. fejezet a hardvert (mikrokontroller, érzékelő-áramkörök, nyomtatott áramkör, mechanika), az 5. fejezet a firmware-t, a 6. fejezet a PC-s alkalmazást írja le. A 7. fejezet a mérési pontosságot befolyásoló tényezőket és az igazolásukhoz szükséges méréseket veszi sorra, a 8. fejezet az eddigi eredményeket közli, a 9. fejezet az értékelést és az összefoglalást tartalmazza.

A dolgozat két sorszámozott jelölést használ a még kiegészítendő helyeken. A **[BIZONYITAS]** jelölés olyan állítást követ, amelyet célzott méréssel vagy adatlapi adattal igazolni kell; a **[HIANYZO ADAT]** jelölés olyan adatra utal, amely a rendelkezésre álló forrásokból (forráskód, NYÁK-rajz, 3D modellek, TMDK-dolgozat) nem volt megállapítható.
