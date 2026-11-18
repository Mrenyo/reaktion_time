# A firmware

Az időmérő egység programja az Espressif hivatalos fejlesztői keretrendszerében, az ESP-IDF-ben készült, C nyelven. A TMDK-dolgozat a firmware megvalósítását nem tárgyalta; ez a fejezet a végleges programot írja le. A teljes forráskód az A mellékletben található.

## A keretrendszer választása: ESP-IDF

Az ESP32-családra két elterjedt fejlesztői környezet létezik: az Arduino-keretrendszer és az ESP-IDF. A firmware korábbi változata Arduino-környezetben készült (a forráskód megjegyzései a jelenlegi megoldást több helyen ahhoz hasonlítják), a végleges változat ESP-IDF-re épül. Az áttérést a mérési feladat igényei indokolják:

- **Közvetlen hozzáférés a FreeRTOS-hoz.** Az ESP-IDF a FreeRTOS valós idejű operációs rendszer többmagos változatára épül [48]. A feladatok (taskok) processzormaghoz rögzíthetők, így a mérés kizárólagosan használhatja az egyik magot, miközben a soros kommunikáció a másikon fut.
- **Teljes körű konfiguráció.** A rendszer beállításai (az ütemező órajele, a felügyeleti időzítő viselkedése, a memória és a flash paraméterei) a projekt részeként, verziózható fájlban rögzíthetők.
- **A perifériák natív meghajtói.** Az A/D átalakító, a GPIO és az UART a keretrendszer saját, dokumentált meghajtóin keresztül érhető el [40] [47], köztes réteg nélkül.
- **Reprodukálható fordítás.** A projekt CMake-alapú, a függőségei és a célhardver a forrásfában rögzítettek.

A projekt ESP-IDF 5.5.6 verzióval készült. Felépítése a keretrendszer szokásos mintáját követi:

!table A firmware-projekt fájljai || 4.6,11.4
Fájl | Tartalom
`CMakeLists.txt` | a projekt gyökere: betölti az ESP-IDF fordítási rendszerét, a projekt neve `Reakcioido_firmware`
`main/CMakeLists.txt` | a `main` komponens: forrásfájl (`main.c`) és a felhasznált komponensek (`esp_driver_gpio`, `esp_driver_uart`, `esp_timer`, `esp_adc`)
`main/main.c` | a teljes program (kb. 220 sor)
`sdkconfig.defaults` | a projekt saját beállításai (lásd lent); ebből és az alapértékekből készül a teljes `sdkconfig`

A fordítás és a feltöltés telepített ESP-IDF környezetben egyetlen paranccsal történik (`idf.py build flash`), a feltöltés a fejlesztőlap soros (UART) portján keresztül megy.

### A projekt beállításai

!table A `sdkconfig.defaults` beállításai és indoklásuk || 6.6,9.4
Beállítás | Indoklás
`CONFIG_IDF_TARGET="esp32s3"` | a célhardver az ESP32-S3
`CONFIG_ESPTOOLPY_FLASHSIZE_16MB`, `FLASHMODE_QIO`, `FLASHFREQ_80M` | az N16R8 modul 16 MB-os, négyvezetékes (quad) flash memóriája
`CONFIG_SPIRAM`, `SPIRAM_MODE_OCT`, `SPIRAM_SPEED_80M` | a modul 8 MB-os, oktális PSRAM-ja (a program nem használja, de a hardvernek megfelelő beállítás)
`CONFIG_ESP_DEFAULT_CPU_FREQ_MHZ_240` | 240 MHz-es processzor-órajel az alapértelmezett 160 MHz helyett: rövidebb mérő ciklus
`CONFIG_FREERTOS_HZ=1000` | 1 ms-os ütemezési órajel az alapértelmezett 10 ms helyett
`CONFIG_ESP_TASK_WDT_CHECK_IDLE_TASK_CPU1` kikapcsolva | a mérő feladat folyamatosan foglalja az 1-es magot; e beállítás nélkül a felügyeleti időzítő hibát jelezne, mert az üresjárati feladat ott nem jut futáshoz

A konzol (a szabványos be- és kimenet) az UART0-ra van irányítva 115200 baud sebességgel; ez a keretrendszer alapértelmezése, és a fejlesztőlap USB–soros átalakítóján keresztül éri el a PC-t.

## A program felépítése

A program két feladatból áll, amelyek a két processzormagon futnak:

- A **mérő feladat** (`mero_task`) az 1-es maghoz rögzítve, megszakítás nélküli végtelen ciklusban figyeli a gombot és a fotodiódát, és vezeti a próba állapotgépét. Várakozást vagy blokkoló hívást nem tartalmaz, így a ciklus a lehető legsűrűbben fut.
- A **fő feladat** (`app_main`) az inicializálás után a soros portról érkező sorokat olvassa blokkoló módon, és végrehajtja a parancsokat. Ez a 0-s magon fut, így a soros kommunikáció nem veszi el az időt a méréstől.

A két feladat közös változókon keresztül érintkezik. Az állapotot (`allapot`) csak a mérő feladat írja; a fő feladat egy jelzőbittel (`uj_proba`) kér új próbát, amelyet a mérő feladat a ciklusa elején vesz észre. Ez az egyirányú írási szabály zárolás nélkül is elkerüli, hogy a két feladat egyszerre módosítsa az állapotot. Az A/D átalakítót is kizárólag a mérő feladat olvassa; a diagnosztikai `ADC` parancsnál a fő feladat csak kérést jelez, az olvasást a mérő feladat végzi el.

Az inicializálás lépései:

1. a GPIO5 bemenetként, belső felhúzó ellenállással, lefutó élre érzékeny megszakítással;
2. az ADC1 egység egyenkénti (oneshot) módban, a 3-as csatornán (GPIO4), 12 bites felbontással és 0 dB csillapítással;
3. az UART0 meghajtójának telepítése és a szabványos bemenet ráirányítása, hogy a sorok blokkoló olvasással (`fgets`) érkezzenek;
4. a mérő feladat létrehozása az 1-es maghoz rögzítve (`xTaskCreatePinnedToCore`).

## Az állapotgép

A firmware próbánként négy állapotot jár be.

!fig fig_allapotgep.png | 16 | A firmware állapotgépe. Az új próbát jelző korrekciós érték bármely állapotból a fényjelre várakozásba viszi a gépet.

!table A firmware állapotai || 2.6,6.0,7.4
Állapot | Jelentés | Továbblépés feltétele
`TETLEN` | bekapcsolás után, még nem volt próba | korrekciós érték érkezik a soros porton
`VAR_FENY` | a fotodióda fekete → fehér átmenetére vár | az ADC-érték előbb a küszöb alatt volt, majd eléri a küszöböt; ekkor rögzül a fényjel időbélyege
`VAR_GOMB` | a fényjel utáni első gomblenyomásra vár | új lenyomás érkezik; a gombjel időbélyege az első alacsony szint ideje
`KESZ` | a reakcióidő lekérdezhető | új korrekciós érték (új próba)

Az állapotgép két szabálya közvetlenül a mérés érvényességét védi. Egyrészt a fényjelet csak **átmenetként** fogadja el: az új próba kezdete után előbb sötét (küszöb alatti) értéket kell látnia, és csak az ezt követő küszöb feletti érték számít. Így ha a jelző az előző próbából még világos, vagy az erősítő kimenete még nem csengett le, az nem indít hamis mérést. Másrészt a gombot csak **új lenyomásként** fogadja el: ha a gomb a fényjel pillanatában már le van nyomva, akkor az nem válasz, csak a felengedés utáni következő lenyomás az. A fényjel előtti lenyomások az ESP szempontjából nem számítanak (az elsietett választ a PC kezeli, 6.9. szakasz).

## A fényjel detektálása

A mérő ciklus `VAR_FENY` állapotban minden körben beolvassa az A/D átalakító értékét, és összeveti a küszöbbel:

```
if (adc_oneshot_read(adc, FOTODIODA, &ertek) == ESP_OK) {
    if (ertek < KUSZOB) {
        sotet_volt = true;
    } else if (sotet_volt) {
        t_pd = esp_timer_get_time();
        allapot = VAR_GOMB;
    }
}
```

A küszöb rögzített érték (475), a megfigyelt sötét (kb. 0) és világos (kb. 950) szint fele. A fényjel időbélyege közvetlenül az olvasás után, a küszöb első elérésekor készül. Ha az átalakító olvasása hibát jelez, a minta kimarad, így hibás olvasás nem adhat hamis fényjelet. Az átalakítót a program csak ebben az állapotban olvassa, a többi állapotban a ciklus csak a gombot figyeli, ezért ott a ciklus gyorsabb.

A fényjel időbélyegének felbontását a ciklus periódusideje adja, amelyet döntően az A/D átalakítás ideje határoz meg. A küszöb elérése és az észlelése között legfeljebb egy ciklusnyi idő telhet el; ez a mérés egyik bizonytalansági forrása (7.2. szakasz).

## A gombjel detektálása

A gombot a program két úton figyeli. A lefutó élre megszakítás fut le, amely rögzíti az él idejét; emellett a mérő ciklus minden körben beolvassa a láb szintjét. A megszakítás azért kell, hogy egy nagyon rövid alacsony impulzus se vesszen el akkor sem, ha a ciklus éppen az A/D átalakítással van elfoglalva, és hogy a lenyomás időbélyege az él tényleges ideje legyen, ne a ciklus következő körének ideje.

```
int64_t elso = SOHA;
if (szamlalo != latott_szamlalo) {                  // új él a megszakításból
    latott_szamlalo = szamlalo;
    elso = most - (int64_t)(uint32_t)((uint32_t)most - also);
}
bool alacsony = gpio_get_level(GOMB) == 0;
if (alacsony && elso == SOHA) elso = most;
if (elso != SOHA) {
    if (!pressed) lenyomas_kezdete = elso;
    utolso_alacsony = alacsony ? most : elso;
}
bool now = (most - utolso_alacsony) < HOLD_US;
if (now && !pressed && allapot == VAR_GOMB) {
    t_btn = lenyomas_kezdete;   // az első alacsony jel ideje
    allapot = KESZ;
}
pressed = now;
```

A pergésmentesítés tartási idővel történik: a gomb az utolsó alacsony szint után még 30 ms-ig (`HOLD_US`) lenyomottnak számít. A mechanikus kapcsoló záráskor és nyitáskor néhány milliszekundumig pereg; a tartási idő miatt a pergés nem ad több lenyomást, és a felengedéskori pergés sem számít új lenyomásnak. A megoldás lényeges tulajdonsága, hogy a **válasz időbélyege az első alacsony szint ideje**, tehát a pergésmentesítés nem késlelteti a mért időpontot: a 30 ms csak azt határozza meg, mennyi idő múlva fogadható el a következő lenyomás.

A megszakítás és a mérő feladat más-más processzormagon futhat, a 32 bites processzoron pedig egy 64 bites változó írása nem egyetlen lépés. Ha a megszakítás a teljes 64 bites időbélyeget írná, a mérő feladat félig frissített értéket olvashatna. A megszakítási rutin ezért csak 32 bites értékeket ír: az idő alsó 32 bitjét és egy számlálót. A mérő feladat a számláló változásából veszi észre az új élt, és a teljes időbélyeget a saját 64 bites órájából állítja vissza (a fenti kódrészlet negyedik sora). A visszaállítás egyértelmű, mert az él legfeljebb egy ciklusnyi idővel korábbi az aktuális időnél; a megszakítás adatait a ciklus még az óra kiolvasása előtt olvassa be, így az él ideje biztosan nem későbbi.

## Az időmérés

Az időbélyegek az `esp_timer` komponens órájából származnak, amely a bekapcsolás óta eltelt időt mikroszekundumban, 64 bites értékként adja meg [42]. A program az időbélyegeket 64 biten tárolja és vonja ki egymásból; a 64 bites mikroszekundum-számláló a gyakorlatban nem csordul túl (a túlcsordulásig több százezer év telne el).

A firmware korábbi (1.0-s) változata az időt 32 bitre csonkolva tárolta. A 32 bites számláló körülbelül 71,6 percenként körbefordul, és a tartási idő vizsgálata előjeles 32 bites különbséggel dolgozott: ha a gombot 35,8 percnél hosszabb ideig nem nyomták le, a különbség negatívba fordult, a gomb tévesen lenyomottnak látszott, és a következő tényleges lenyomás elveszhetett. Egy hosszabb szünet utáni mérés első próbájában ez hiányzó reakcióidőt okozhatott. Az 1.1.0-s változat 64 bites időbélyegei ezt a hibát megszüntetik.

Az óra pontosságát a mikrokontroller kvarcoszcillátora határozza meg. Egy tipikusan ±10–20 ppm pontosságú kvarc 300 ms-os reakcióidőn néhány nanoszekundumnyi hibát okoz, ami a többi hibaforráshoz képest elhanyagolható. [BIZONYITAS: A fejlesztőlap kvarcoszcillátorának adatlapi pontossága (ppm), vagy az ESP órájának összevetése ismert pontosságú referenciával (pl. jelgenerátor ismert periódusú jelének mérése).]

## A reakcióidő számítása és a soros parancsok

A fő feladat a soros portról érkező sorokat dolgozza fel. A sorvégi kocsivissza, soremelés és szóköz nem számít. A mérés üzenetei mellett a firmware a verzióját is megadja, és két diagnosztikai parancsot ismer:

!table A firmware által kezelt soros üzenetek || 3.4,5.0,7.6
Beérkező sor | Válasz | Hatás
`CHECK` | `OK` | kapcsolatellenőrzés; a mérési állapotot nem változtatja
`VERSION` | a firmware verziója, pl. `1.1.0` | a PC a mérés indításakor kérdezi le, és a mérési fájlba írja
előjeles szám (pl. `-9259.259`) | nincs | új próba: a szám a scanout-korrekció µs-ban; az előző próba adatai elvesznek, az állapot `VAR_FENY`
`RTIME` | a reakcióidő µs-ban három tizedesjeggyel (pl. `250000.000`), vagy `NA` | az eredmény lekérdezése; az állapotot nem változtatja
`DIAG` | `DIAG ciklus=… atlag_us=… max_us=… adc_min=… adc_max=… kuszob=475` | diagnosztika: a legutóbbi fényjelre várás alatt a mérő ciklus lefutásainak száma, átlagos és legnagyobb periódusideje, a közben olvasott legkisebb és legnagyobb ADC-érték
`ADC` | `ADC <érték>` vagy `NA` | diagnosztika: egyetlen nyers ADC-olvasás; csak próbán kívül ad értéket
bármi más | nincs | figyelmen kívül marad

Az `RTIME` kérésre a program akkor ad számot, ha az állapot `KESZ`, és nincs függőben új próba:

```
rt = (double)(t_btn - t_pd) - korr;     // t_btn - (t_pd + korr)
if (rt >= 0) printf("%.3f\n", rt);
else         printf("NA\n");
```

Ez megfelel a 3.4. szakasz képletének. `NA` a válasz, ha a korrekció megérkezése óta nem volt fényjel, a fényjel után nem volt gomblenyomás, vagy a korrigált érték negatív lenne. Az ESP kérés nélkül soha nem küld adatot; ez leegyszerűsíti a PC oldalát, mert minden beérkező sor egy függőben lévő kérés válasza. A válaszok alakja kérésenként más (`OK`; két pontot tartalmazó verziószám; szám vagy `NA`; `DIAG`, illetve `ADC` kezdetű sor), így egy késve érkező válasz nem téveszthető össze egy másik kérés válaszával.

A két diagnosztikai parancsot a PC-s program nem küldi; soros terminálból kérhetők le, a mérőlánc ellenőrzéséhez. A `DIAG` a mérő ciklus tényleges periódusidejét adja meg, vagyis a fényjel időbélyegének felbontását; az `ADC` paranccsal a jelző sötét és világos állapotához tartozó jelszint olvasható ki, amiből a küszöb tartaléka megítélhető. A ciklusidő mérése a mérést nem lassítja érdemben: a ciklus az időt egyébként is kiolvassa, a diagnosztika ehhez egy kivonást és egy összehasonlítást ad.

A soros átvitel ideje nem része az eredménynek. 115200 baud sebességnél egy bájt átvitele körülbelül 87 µs, egy tíz-tizenkét karakteres üzeneté körülbelül 1 ms; ehhez járul az USB–soros átalakító és a PC illesztőprogramjának pufferelése. Mindez csak azt befolyásolja, hogy a PC mikor kapja meg az eredményt, magát az eredményt nem.

## A firmware ismert korlátai

- **Rögzített küszöb.** A 475-ös küszöb a fejlesztéshez használt kijelzőn és fényerőn megfigyelt szintekhez igazodik. Más kijelzőn vagy jelentősen kisebb fényerőn a világos szint a küszöb közelébe kerülhet. Továbbfejlesztési lehetőség a küszöb automatikus beállítása a mérés elején mért sötét és világos szintből.
- **A ciklus periódusa nem állandó.** A fényjelre várva a ciklus tartalmazza az A/D átalakítást, a többi állapotban nem. A gombjel időbélyegét a megszakítás és a sűrűbb ciklus pontosabbá teszi, a fényjelét az átalakítás ideje korlátozza.
- **A megszakítások a mérő magon is futhatnak.** A mérő feladatot a saját magján más feladat nem szakítja meg, a rendszer megszakításai (pl. az ütemező órajele) azonban igen. Ezek rövidek, de a ciklusidő szórásához hozzájárulnak. [BIZONYITAS: A mérő ciklus legnagyobb periódusideje (a `DIAG` parancs `max_us` értéke) sok próba után; ez adja a fényjel időbélyegének legrosszabb esetét.]
- **Nincs próbaazonosító.** Az `RTIME` válasza nem tartalmazza, melyik próbához tartozik; a hozzárendelést a PC végzi a kérés–válasz sorrend alapján (6.7. szakasz).
- **Az 1.1.0-s változat hardveres kipróbálása hátravan.** A 64 bites időbélyegeket, a verziólekérdezést és a diagnosztikai parancsokat tartalmazó forráskód hibátlanul lefordul, a mérőeszközön azonban még nem futott. [BIZONYITAS: Az 1.1.0-s firmware kipróbálása a mérőeszközön: a `CHECK`, `VERSION`, `DIAG` és `ADC` parancs válasza, valamint néhány teljes mérés annak ellenőrzésére, hogy a reakcióidők és a PC − ESP különbség a korábbi változattal egyező tartományba esnek.]
