# Felhasználói útmutató

Ez az útmutató a mérést vezető kutatónak szól: az első beállítástól egy mérés
lefolytatásán át az eredmények értelmezéséig.

## 1. Első beállítás (egyszer)

1. **Tobii Pro Eye Tracker Manager:** telepítsd, csatlakoztasd a szemkövetőt,
   és állítsd be a **display area**-t arra a monitorra, amelyen a mérés fut
   (a monitor mérete és a szemkövető helyzete). A program ebből számolja a
   kijelző fizikai méretét. Ha nincs beállítva, a mérés nem indul el.
2. **ESP:** csatlakoztasd USB-n. Ha a gépen több soros eszköz is van, és a
   program nem találja meg egyértelműen az ESP-t, add meg a portját a
   beállításfájlban (`Hardware.EspPortName`, pl. `"COM5"`).
3. **Beállításfájl:** az első indításkor létrejön a
   `Dokumentumok\ReakcioidoMeres\beallitasok.json` fájl. Itt állíthatod a
   próbaszámot, a gyakorló próbákat, az előperiódust, az inger méretét és
   helyét stb. (lásd [MERESI_BEALLITASOK.md](MERESI_BEALLITASOK.md)).
4. **Fotodióda:** nyisd meg a kívánt mérési nézetet (a menüben a Mérés kártya,
   „Normál” vagy „Periférikus reakcióidő”). A nézet teljes képernyőre vált, a
   mérőfelület jobb alsó sarkában megjelenik egy szürke keretes fekete négyzet.
   A fotodiódát erre, középre kell felhelyezni.

## 2. Egy mérés menete

1. **A résztvevő elhelyezése:** üljön kényelmesen a monitor és a szemkövető
   elé, a tervezett nézési távolságban (az alapérték 60 cm).
2. **Kalibráció:** a menüben a Kalibráció kártya.
   - *Meglévő kalibráció használata:* a program ellenőrzi, hogy van-e
     kalibráció a szemkövetőn, és hogy 1 másodperc alatt érkezik-e érvényes
     tekintetadat. Ehhez a résztvevőnek a képernyőt kell néznie.
   - *Újrakalibrálás:* a Tobii Pro Eye Tracker Manager kalibrációja indul el.
   A mérés csak kalibráció után indítható.
3. **Mérés indítása:** a mérési nézetben a résztvevő elolvassa a képernyő
   tetején lévő instrukciót, majd a kutató megnyomja a „Mérés indítása”
   gombot. A program:
   - betölti a beállításokat (hibás fájl esetén hibaüzenetet ad, és nem indul);
   - 1 másodpercig méri a nézési távolságot (ha nem sikerül, a beállított
     értékkel számol);
   - megnyitja az ESP-kapcsolatot és létrehozza a mérési fájlt;
   - lefuttatja a gyakorló próbákat, majd a tesztpróbákat.
4. **Mérés közben:** a képernyőn csak a fixációs kereszt és az inger látszik,
   az egérmutató rejtve van. A haladást az alsó sáv mutatja (pl.
   „Érvényes 5/10”). Az érvénytelen próbák helyett a program újat ad, amíg
   össze nem gyűlik a kívánt számú érvényes próba (legfeljebb
   `MaxTestAttempts` próbálkozásig).
5. **Megszakítás:** az **Esc** billentyű leállítja a mérést, a „Vissza a
   menühöz” gomb pedig le is állítja, és ki is lép. Az addig rögzített próbák
   mindkét esetben mentve maradnak.
6. **A mérés vége:** a képernyő tetején megjelenik az eredmény (befejezett,
   próbálkozási korlát miatt véget ért, vagy megszakadt), és új mérés
   indítható.

## 3. Az eredmények

A **Statisztika** nézetben kiválasztható bármelyik korábbi mérés.

- A táblázat minden próbát mutat. A gyakorló próbák „Gy” előtagot kapnak, és
  nem számítanak bele az összesítésbe.
- A **Státusz** oszlop az érvénytelenség okát is megadja:

  | Felirat | Jelentés |
  |---|---|
  | Érvényes | a próba érvényes |
  | Időtúllépés | nem érkezett válasz a `ReactionTimeoutMs` időn belül |
  | Tekintethiba | a résztvevő nem tartotta be a tekintetre vonatkozó utasítást |
  | Kevés tekintetadat | a szemkövető a próba alatt túl sokszor elvesztette a szemet |
  | Nincs szemkövető | a szemkövető a próba alatt nem volt elérhető |

- Az **Összesítés** az érvényes tesztpróbák átlagát, mediánját és szórását
  mutatja: „ESP” a hardveres, „Helyi” a PC-n mért időre. A reakcióidők
  eloszlása jobbra ferde, ezért elemzéshez a medián a megbízhatóbb.
- A lista jelzi, ha egy mérés megszakadt, próbálkozási korlát miatt ért véget,
  vagy nem zárult le (pl. a program a mérés közben leállt).

A teljes adat (metaadatok, próbánkénti ingerhely, excentricitás, előperiódus,
tekintetminták) a `Dokumentumok\ReakcioidoMeres\measurements` mappában van,
CSV-formátumban. Ez Excelben, R-ben vagy Pythonban (pandas) közvetlenül
megnyitható.

## 4. Hibaelhárítás

| Üzenet | Teendő |
|---|---|
| A Tobii szemkövető nem érhető el | ellenőrizd az USB-kapcsolatot és a Tobii Pro Eye Tracker Managert |
| Nincs beállítva a kijelző (display area) | állítsd be a display area-t a mérést futtató monitorra |
| A display area méretaránya nem egyezik | a display area másik monitorra van beállítva, vagy a program nem azon a monitoron fut |
| COM-port foglalt | zárd be a soros monitort (Arduino, PlatformIO) vagy a program másik példányát |
| Az ESP nem érhető el / nem sikerült egyértelműen azonosítani | csatlakoztasd újra, vagy add meg a portot: `Hardware.EspPortName` |
| Hibás mérési beállítások | javítsd a `beallitasok.json`-t, az üzenet megmondja, melyik mezőt |
| A beállított excentricitás nem fér el | csökkentsd a `MinEccentricityDeg` értéket, vagy növeld a nézési távolságot / a monitort |
| Mentési hiba | ellenőrizd, hogy az adatmappa írható-e (és van-e szabad hely) |

Minden hiba a `Dokumentumok\ReakcioidoMeres\logs` mappa naplójában is
megtalálható, a részletekkel együtt.
