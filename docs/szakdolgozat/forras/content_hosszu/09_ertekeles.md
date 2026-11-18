# Értékelés és következtetések

## A megoldás értékelése

A megvalósított rendszer lényege, hogy a reakcióidőmérés két végpontját a lehető legközelebb viszi a fizikai eseményekhez. Az inger oldalán a fotodióda a kijelző tényleges fényváltozását érzékeli, nem a PC rajzolási szándékát. A válasz oldalán az egérgomb elektromos jele közvetlenül az időmérő egységhez jut, így a kattintás időpontja nem az operációs rendszer eseménysorából származik. A két eseményt ugyanaz a mikrokontrolleres időalap rögzíti, és a reakcióidő-számítás az ESP-ben történik meg.

A hardverválasztások nem önmagukért kerültek a rendszerbe. Az ESP32-S3 azért központi elem, mert két processzormagja lehetővé teszi, hogy a mérés és a kommunikáció egymást ne zavarja, és elegendő számítási és I/O tartalékot ad. A BPW34 azért alkalmas fotodiódának, mert gyors, a kijelző fényéhez illeszkedő spektrális tartományban érzékeny, és kis, maszkolt mérőterületen is stabil jelet ad. A transzimpedancia-erősítő a közvetlen bekötéshez képest gyorsabb és terheléstől független jelet szolgáltat. Az egérgomb jelének kivezetése a mérési végpontot a fizikai kattintáshoz közelíti.

A PC-s alkalmazás szerepe vezérlő és értelmező jellegű. A végleges változatban ez a szerep jelentősen bővült a TMDK-dolgozathoz képest: a program nemcsak megjeleníti az ingert és fogadja az eredményt, hanem a kijelző pontos képidőzítéséből számítja a scanout-korrekciót, előre rögzített szabályok szerint minősíti a próbákat, a mérés minden körülményét rögzíti, és a mérést szabadon paraméterezhetővé teszi. A Tobii alapú szemmozgáskövetés nem időmérő elemként működik, hanem azt ellenőrzi, hogy a próba megfelelt-e a feladat szabályainak.

A felépítés erőssége nem egyetlen komponensben, hanem a komponensek felelősségmegosztásában van: az ESP mér és számol, a fotodióda fizikai ingerjelet ad, az egér bekötése fizikai válaszjelet ad, a PC-s alkalmazás vezérel, korrigál és dokumentál, a szemkövetés pedig a próba érvényességét ellenőrzi.

## Következtetések

Az irodalmi áttekintés alapján a számítógépes reakcióidőmérés legfontosabb problémája nem önmagában az, hogy a rendszer késik, hanem az, hogy a késleltetés több, egymástól nehezen elkülöníthető forrásból áll, és egy része próbáról próbára változik. A bemutatott megoldás erre nem egyetlen átlagos korrekcióval válaszol, hanem külön kezeli a kijelzőoldali fényváltozást, az inputoldali kattintásjelet, a képernyőn belüli scanout-eltérést és a tekinteti érvényességet.

A fejlesztés közben rögzített adatok ezt a megközelítést támasztják alá: a szoftveres időbélyegzés többlete a vizsgált mérőhelyen átlagosan 43 ms volt, 8 ms-os szórással. Az átlag levonható lenne, a szórás azonban nem; a szoftveres mérés bizonytalansága így ugyanakkora nagyságrendű, mint az irodalomban leírt ingerhatások. A hardveres mérés ezt a változó részt távolítja el.

A rendszer legfontosabb következtetése, hogy a reakcióidőmérésben az időbélyeg helye meghatározóbb, mint az időmérő óra névleges felbontása. A nagy felbontású PC-s óra önmagában nem biztosít pontos reakcióidőt, ha a rossz eseményt méri. Ezzel szemben a hardverközeli fotodióda- és gombjel-időbélyegzés akkor is értelmezhetőbb mérési alapot ad, ha a teljes rendszer pontosságát továbbra is befolyásolják a kijelző fizikai sajátosságai és a jelkezelési küszöbök.

A fejlesztés egyik tanulsága, hogy az ellenőrző adat megtartása a mérőrendszer hibáit is láthatóvá teszi. A PC-n mért idő nem része az eredménynek, a kettő próbánkénti összevetése azonban közvetlenül megmutatta azt a hibát, amelyben az ESP az előző próba lecsengő fényjelére indult. Ugyanez az összevetés a későbbi mérésekben is folyamatos működés-ellenőrzésként szolgál.

## Korlátok

A rendszer jelenlegi állapotának korlátai:

- **A pontossági állítások nagy része még igazolásra vár.** A mérőlánc elemeinek hibája részben tervezési adat vagy becslés; a 7. fejezetben felsorolt célzott mérések nélkül az 1 ms alatti bizonytalanság nem tekinthető bizonyítottnak.
- **A megjelenés időpontja nem szinkronizált a képfrissítéshez.** A program nem tudja, melyik képkockában jelenik meg az inger. Ez a mérés eredményét nem érinti (a fotodióda méri), de az előperiódus tényleges hossza egy-két képkockányit ingadozik, és a tekintet-ellenőrzés válaszablaka ennyivel korábban kezdődik.
- **A scanout-modell feltevésekre épül.** Változó frissítési frekvencia mellett a modell érvénytelen, és a program ezt nem ismeri fel.
- **Színfüggő panelválasz.** A fotodióda a jelző fekete → fehér átmenetét méri; az alapértelmezettől eltérő inger- és háttérszínnél az inger megjelenése ettől eltérhet.
- **Rögzített küszöb a firmware-ben.** Más kijelzőn vagy kis fényerőn a detektálás megbízhatósága nem garantált.
- **A méretek képpontban vannak megadva.** A fizikai méret és a látószög a monitortól és az ülési távolságtól függ; összehasonlítható mérésekhez azonos mérőhely kell.
- **A kalibráció pontosságát a program nem méri.** A tekintet-ellenőrzés megbízhatósága a gyártói kalibráció minőségétől függ.
- **A válasz egyetlen gomb.** A rendszer egyszerű reakcióidőt mér; választásos feladat (több inger, több válasz) nem valósítható meg vele.
- **A firmware 1.1.0-s változata még nem futott a mérőeszközön** (5.8. szakasz); a közölt mérések a korábbi változattal készültek.

## Továbbfejlesztési lehetőségek

- **Önkalibráló küszöb:** a firmware a mérés elején mérje meg a jelző sötét és világos szintjét, és ebből állítsa be a küszöböt; a PC ehhez külön parancsot adhat.
- **Próbaazonosító a protokollban:** a reakcióidő-válasz tartalmazza, melyik próbához tartozik.
- **Nyers időbélyegek visszaadása:** ha az ESP a fényjel és a gombjel időbélyegét is elküldi, a korrekció utólag újraszámítható, és az előperiódus tényleges hossza is hardveresen mérhető.
- **Képfrissítéshez szinkronizált megjelenítés:** a rajzolás áthelyezése olyan grafikus felületre, amely a képfrissítéshez igazítja a megjelenítést; ezzel az előperiódus pontosabb lesz, és az egy képkockában megjelenés garantálható.
- **A változó frissítési frekvencia felismerése** a mérés indításakor.
- **Beépített validációs mód:** a firmware diagnosztikai parancsai (ciklusidő, jelszintek) jelenleg soros terminálból kérhetők le; a PC-s programba épített diagnosztikai nézet ezeket automatikusan rögzíthetné a mérési fájlba.
- **Több válaszgomb és más ingertípusok:** választásos reakcióidő, illetve hanginger mikrofonos érzékeléssel.
- **A kalibráció pontosságának mérése** a programban, ismert pontok megjelenítésével.
