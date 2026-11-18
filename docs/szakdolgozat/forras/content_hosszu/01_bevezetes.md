# Bevezetés és célkitűzések

A reakcióidő mérése fontos szerepet tölt be a pszichológiai, informatikai és humán-gép interakciós vizsgálatokban, mivel alkalmas az észlelési és válaszadási folyamatok időbeli jellemzésére. A számítógépes környezetben végzett reakcióidőmérések esetében azonban a kapott eredmények pontosságát számos technikai tényező befolyásolja. A kijelző megjelenítési sajátosságai, az operációs rendszer időzítési bizonytalanságai, a grafikus feldolgozás késleltetése, valamint a beviteli eszközök működéséből származó eltérések mind hozzájárulhatnak ahhoz, hogy a rögzített időadatok ne a felhasználó tényleges reakcióját tükrözzék. Emiatt a hagyományos, kizárólag szoftveres időbélyegekre épülő megoldások pontossága sok esetben korlátozott. [1]

A projekt célja egy olyan reakcióidőmérő rendszer megvalósítása, amely a technikai eredetű mérési hibák csökkentésével pontosabb képet ad a vizuális ingerekre, más néven stimulusokra adott manuális reakcióidőről. A rendszer egyik alapvető eleme az inger tényleges fizikai megjelenésének érzékelése. Ennek megoldására fotodióda kerül alkalmazásra, amely a kijelző fényváltozását közvetlenül érzékeli, így a mérés nem csupán a szoftver által kezdeményezett megjelenítés időpontjára, hanem a valódi vizuális megjelenés pillanatára épülhet. Ez különösen lényeges olyan esetekben, ahol néhány ezredmásodperc eltérés is érdemben befolyásolhatja az eredmények értelmezését.

A reakcióidő meghatározásának másik fontos összetevője a felhasználói válasz lehető legpontosabb rögzítése. A projekt ennek érdekében mikrokontroller-alapú időbélyegzést alkalmaz, amely a kattintás fizikai időpontjának meghatározását a számítógép hagyományos inputfeldolgozásától függetlenül teszi lehetővé. Ez azért bír kiemelt jelentőséggel, mert a szoftveresen regisztrált kattintási idő nem esik egybe a felhasználó tényleges válaszának pillanatával. A hardveres mérés így pontosabb alapot biztosít a reakcióidő kiszámításához.

A projekt további lényeges része a szemmozgáskövetés bevonása a mérési folyamatba. A Tobii szemkövető használata lehetőséget teremt arra, hogy a rendszer ne csupán a válasz időpontját rögzítse, hanem annak érvényességét is ellenőrizze. A tekinteti adatok alapján megállapítható, hogy a felhasználó az inger megjelenésekor valóban a megfelelő területre figyelt-e, így kiszűrhetők azok a próbák, amelyekben ugyan történt válaszadás, de a mérési feltételek nem teljesültek megfelelően. A reakcióidő helyes értelmezéséhez nem elegendő önmagában a kattintás időpontjának ismerete, a figyelmi állapot ellenőrzése is szükséges lehet.

A projekt jelentősége abban áll, hogy egyetlen rendszerben kapcsolja össze a vizuális inger tényleges megjelenésének mérését, a válasz hardveres időbélyegzését és a szemmozgás-alapú validációt. Ez a megközelítés lehetőséget ad arra, hogy a reakcióidőmérés a hagyományos szoftveres megoldásoknál pontosabb és megbízhatóbb legyen.

## A témaválasztás indoklása

A témaválasztást az indokolja, hogy a reakcióidő mérése egyszerre tűnik egyszerűnek és mérnöki szempontból összetettnek. A vizsgálati személy szempontjából a feladat egy látható inger észlelése és egy kattintás végrehajtása, a mérőrendszer szempontjából azonban több egymásra épülő technikai folyamatot kell kezelni. Az inger létrehozása, a kijelző fizikai fényváltozása, az inputeszköz elektromos állapotváltozása és az adatok feldolgozása nem egyetlen időpillanatban történik. Emiatt a dolgozat központi kérdése az, hogyan lehet a mérési láncban megjelenő technikai késleltetéseket úgy kezelni, hogy a kapott reakcióidő a lehető legközelebb kerüljön a tényleges emberi válaszidőhöz.

A dolgozat gyakorlati jelentősége abból következik, hogy a vizuális ingerekre adott manuális reakcióidő több területen is értelmezhető mérőszám. Használható figyelmi állapot, szenzomotoros válasz, ember-gép interakció és perifériás észlelés vizsgálatánál is. Ezekben a helyzetekben nem elegendő azt tudni, hogy egy program mikor adott ki rajzolási parancsot, mert a résztvevő csak a kijelzőn fizikailag megjelenő fényváltozásra tud reagálni. Ugyanígy a program által fogadott egéresemény sem azonos a kattintás fizikai kezdetével. A mérés pontossága ezért elsősorban azon múlik, hogy a rendszer hol jelöli ki az inger és a válasz időpontját. [1] [2] [3]

## Célkitűzések

A vizsgálat elméleti célja a reakcióidőmérés és a rendszerkésleltetés kapcsolatának bemutatása. Ennek része a megjelenítési késleltetés, az inputkésleltetés, a szoftveres időbélyegzés korlátainak, valamint a false-pozitív reakcióidőmérés problémájának áttekintése. A gyakorlati cél egy olyan mérési architektúra létrehozása, amely az inger kezdetét fotodiódás fénydetektálással, a válasz kezdetét hardveres jelrögzítéssel, a mérési érvényességet pedig szemmozgáskövetéssel támogatja.

A gyakorlati célkitűzések részletesen:

- olyan időmérő egység készítése, amely az inger fizikai megjelenését és a válasz elektromos jelét ugyanazon, a PC-től független időalapon rögzíti, és a reakcióidőt helyben számítja ki;
- a kijelző soronkénti kirajzolásából adódó, képernyőpozíciótól függő időeltérés (scanout) számítása és korrekciója;
- olyan PC-s alkalmazás készítése, amely a mérést vezérli, az ingereket megjeleníti, a próbák érvényességét előre rögzített szabályok szerint eldönti, és az eredményt visszaellenőrizhető formában tárolja;
- a mérés paramétereinek testreszabhatósága, hogy ugyanaz a rendszer többféle kísérleti elrendezést kiszolgáljon;
- a tekintet ellenőrzése szemmozgáskövetővel, normál (ingerre néző) és periférikus (fixációt tartó) feladatban;
- a mérőhardver tartós, reprodukálhatóan felszerelhető kivitele: nyomtatott áramkör, ház és a fotodiódát a kijelzőn tartó befogó.

A dolgozat nem általános reakcióidőtesztet kíván bemutatni, hanem egy olyan mérési rendszert, amelynek központi kérdése az időpontok helyes kijelölése. Emiatt a hangsúly nem a pszichológiai teljesítmény minősítésén, hanem a mérési feltételek tisztázásán van. A rendszer akkor tekinthető jól felépítettnek, ha az inger fizikai kezdete, a válasz fizikai kezdete és a próba érvényessége elkülöníthetően kezelhető.

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

A 2. fejezet az irodalmi hátteret tekinti át. A 3. fejezet a rendszer követelményeit és felépítését, a 4. fejezet a hardvert (mikrokontroller, érzékelő-áramkörök, nyomtatott áramkör, mechanika), az 5. fejezet a firmware-t, a 6. fejezet a PC-s alkalmazást írja le. A 7. fejezet a mérési pontosságot befolyásoló tényezőket és az igazolásukhoz szükséges méréseket veszi sorra, a 8. fejezet az eddigi eredményeket közli, a 9. és a 10. fejezet az értékelést és az összefoglalást tartalmazza.

A dolgozat két jelölést használ azokra a helyekre, ahol a szöveg még kiegészítésre szorul. A **[BIZONYITAS]** jelölés olyan állítást követ, amelyet célzott méréssel vagy adatlapi adattal igazolni kell; a **[HIANYZO ADAT]** jelölés olyan adatra utal, amely a rendelkezésre álló forrásokból (forráskód, NYÁK-rajz, 3D modellek, TMDK-dolgozat) nem volt megállapítható. A jelölések sorszámozottak, összesített listájuk a D mellékletben található.
