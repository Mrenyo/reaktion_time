// Reakcióidő-mérő: soros protokoll a PC-programmal (docs/SCANOUT_PROTOCOL.md)
//   PC: "CHECK"   -> ESP: "OK"
//   PC: "VERSION" -> ESP: a firmware verziója (FW_VERSION)
//   PC: <szám>    -> új próba, a szám a scanout-korrekció µs-ban (nincs válasz)
//   PC: "RTIME"   -> ESP: reakcióidő µs-ban, vagy "NA"
// Diagnosztika (a PC-program nem használja, soros monitorból kérhető):
//   "DIAG"        -> ESP: "DIAG ..." a legutóbbi fényjelre várás ciklusideje és ADC-szintjei
//   "ADC"         -> ESP: "ADC <nyers érték>", vagy "NA", ha éppen próba fut
// Kérés nélkül az ESP semmit nem küld.

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
#include <stdbool.h>
#include <stdint.h>
#include <limits.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/uart.h"
#include "driver/uart_vfs.h"
#include "esp_attr.h"
#include "esp_timer.h"
#include "esp_adc/adc_oneshot.h"

#define FW_VERSION  "1.1.0"

#define GOMB        GPIO_NUM_5      // egér kattintás, aktív alacsony
#define FOTODIODA   ADC_CHANNEL_3   // GPIO4 = ADC1 3-as csatorna
#define KUSZOB      475             // sötét ~0, világos ~950: a fele
#define HOLD_US     30000           // ennyi ideig "lenyomott" egy alacsony jel után
#define SOHA        (INT64_MIN / 2) // "még nem volt alacsony jel" (a különbségképzés nem csordul túl)

typedef enum { TETLEN, VAR_FENY, VAR_GOMB, KESZ } allapot_t;

// Az állapotot csak a mérő task írja; a soros task az uj_proba jelzővel kér nullázást.
// Az időbélyegek 64 bitesek (esp_timer, µs): a számláló a gyakorlatban nem csordul túl.
static volatile allapot_t allapot = TETLEN;
static volatile bool uj_proba = false;
static volatile int64_t t_pd, t_btn;    // a mérő task írja, még a KESZ állapot beállítása előtt
static adc_oneshot_unit_handle_t adc;

// A megszakítás és a mérő task más-más magon futhat, egy 64 bites változó írása pedig a 32 bites
// processzoron nem atomi. A megszakítás ezért csak 32 bites értékeket ír (az idő alsó 32 bitjét és
// egy számlálót); a teljes időbélyeget a mérő task állítja vissza a saját 64 bites órájából.
static volatile uint32_t isr_ido_also;
static volatile uint32_t isr_szamlalo;

// Diagnosztika a legutóbbi fényjelre várásról (csak 32 bites mezők, a soros task olvassa)
static volatile uint32_t diag_ciklusok, diag_ossz_us, diag_max_us;
static volatile int diag_adc_min, diag_adc_max;

// "ADC" parancs: a soros task kéri, a mérő task olvassa be (az ADC-t csak ő használja)
static volatile bool adc_keres = false;
static volatile int adc_valasz;

// Rövid alacsony impulzus se vesszen el, akkor sem, ha a ciklus épp az ADC-t olvassa
static void IRAM_ATTR gomb_isr(void *arg)
{
    isr_ido_also = (uint32_t)esp_timer_get_time();
    isr_szamlalo = isr_szamlalo + 1;
}

// Folyamatosan fut az 1-es magon: gomb és fotodióda figyelése
static void mero_task(void *arg)
{
    bool pressed = false;
    bool sotet_volt = false;
    uint32_t latott_szamlalo = isr_szamlalo;
    int64_t utolso_alacsony = SOHA;     // az utolsó alacsony jel ideje
    int64_t lenyomas_kezdete = 0;       // a folyamatban lévő lenyomás első alacsony jelének ideje
    int64_t elozo = 0, feny_kezdet = 0;
    bool diag_elso = true;

    while (1) {
        // A megszakítás adatait az óra ELŐTT olvassuk ki, így az ottani idő biztosan nem későbbi
        uint32_t szamlalo = isr_szamlalo;
        uint32_t also = isr_ido_also;
        int64_t most = esp_timer_get_time();

        if (uj_proba) {
            uj_proba = false;
            sotet_volt = false;
            diag_ciklusok = 0;
            diag_ossz_us = 0;
            diag_max_us = 0;
            diag_adc_min = INT_MAX;
            diag_adc_max = INT_MIN;
            diag_elso = true;
            feny_kezdet = most;
            allapot = VAR_FENY;
        }

        // Gomb: az első alacsony jel a megszakításból (pontos él), vagy a szint olvasásából
        int64_t elso = SOHA;
        if (szamlalo != latott_szamlalo) {
            latott_szamlalo = szamlalo;
            elso = most - (int64_t)(uint32_t)((uint32_t)most - also);   // 64 bitre visszaállítva
        }
        bool alacsony = gpio_get_level(GOMB) == 0;
        if (alacsony && elso == SOHA) elso = most;
        if (elso != SOHA) {
            if (!pressed) lenyomas_kezdete = elso;
            utolso_alacsony = alacsony ? most : elso;
        }

        // Lenyomott, amíg HOLD_US-on belül volt alacsony jel (pergésmentesítés)
        bool now = (most - utolso_alacsony) < HOLD_US;
        if (now && !pressed && allapot == VAR_GOMB) {
            t_btn = lenyomas_kezdete;   // az első alacsony jel ideje
            allapot = KESZ;
        }
        pressed = now;

        // Fotodióda: az első fekete -> fehér átmenet; hibás ADC-olvasásnál a minta kimarad
        int ertek;
        if (allapot == VAR_FENY) {
            if (!diag_elso) {
                uint32_t dt = (uint32_t)(most - elozo);
                if (dt > diag_max_us) diag_max_us = dt;
                diag_ciklusok = diag_ciklusok + 1;
                diag_ossz_us = (uint32_t)(most - feny_kezdet);
            }
            diag_elso = false;

            if (adc_oneshot_read(adc, FOTODIODA, &ertek) == ESP_OK) {
                if (ertek < diag_adc_min) diag_adc_min = ertek;
                if (ertek > diag_adc_max) diag_adc_max = ertek;
                if (ertek < KUSZOB) {
                    sotet_volt = true;
                } else if (sotet_volt) {
                    t_pd = esp_timer_get_time();
                    allapot = VAR_GOMB;
                }
            }
        } else if (adc_keres && allapot != VAR_GOMB) {
            // Próbán kívül: egyetlen ADC-olvasás az "ADC" parancsnak
            adc_valasz = adc_oneshot_read(adc, FOTODIODA, &ertek) == ESP_OK ? ertek : -1;
            adc_keres = false;
        }
        elozo = most;
    }
}

void app_main(void)
{
    // Gomb: GPIO5, belső felhúzó, megszakítás a lefutó élre
    gpio_config_t gomb_cfg = {
        .pin_bit_mask = 1ULL << GOMB,
        .mode = GPIO_MODE_INPUT,
        .pull_up_en = GPIO_PULLUP_ENABLE,
        .intr_type = GPIO_INTR_NEGEDGE,
    };
    ESP_ERROR_CHECK(gpio_config(&gomb_cfg));
    ESP_ERROR_CHECK(gpio_install_isr_service(0));
    ESP_ERROR_CHECK(gpio_isr_handler_add(GOMB, gomb_isr, NULL));

    // Fotodióda: GPIO4, 12 bit, 0 dB (kb. 0–0,95 V)
    adc_oneshot_unit_init_cfg_t adc_cfg = { .unit_id = ADC_UNIT_1 };
    ESP_ERROR_CHECK(adc_oneshot_new_unit(&adc_cfg, &adc));
    adc_oneshot_chan_cfg_t ch_cfg = { .atten = ADC_ATTEN_DB_0, .bitwidth = ADC_BITWIDTH_12 };
    ESP_ERROR_CHECK(adc_oneshot_config_channel(adc, FOTODIODA, &ch_cfg));

    // Soros port (UART0, 115200): blokkoló soronkénti olvasás a stdin-ről
    ESP_ERROR_CHECK(uart_driver_install(UART_NUM_0, 256, 0, 0, NULL, 0));
    uart_vfs_dev_use_driver(UART_NUM_0);

    // A mérés teljesen leköti az 1-es magot (a watchdog ott nem figyel, lásd sdkconfig.defaults)
    xTaskCreatePinnedToCore(mero_task, "mero", 4096, NULL, 1, NULL, 1);

    double korr = 0;
    char sor[64];
    while (1) {
        if (fgets(sor, sizeof sor, stdin) == NULL) {
            clearerr(stdin);
            continue;
        }
        int n = strlen(sor);
        while (n > 0 && isspace((unsigned char)sor[n - 1])) sor[--n] = '\0';   // \r, \n, szóköz

        if (strcmp(sor, "CHECK") == 0) {
            printf("OK\n");
        } else if (strcmp(sor, "VERSION") == 0) {
            printf(FW_VERSION "\n");
        } else if (strcmp(sor, "RTIME") == 0) {
            double rt = -1;
            if (allapot == KESZ && !uj_proba) {
                rt = (double)(t_btn - t_pd) - korr;     // t_btn - (t_pd + korr)
            }
            if (rt >= 0) printf("%.3f\n", rt);
            else         printf("NA\n");
        } else if (strcmp(sor, "DIAG") == 0) {
            // A legutóbbi fényjelre várás: ciklusok száma, átlagos és legnagyobb ciklusidő (µs),
            // a közben látott legkisebb és legnagyobb ADC-érték
            uint32_t c = diag_ciklusok;
            if (c == 0) {
                printf("DIAG NA\n");
            } else {
                printf("DIAG ciklus=%lu atlag_us=%.2f max_us=%lu adc_min=%d adc_max=%d kuszob=%d\n",
                       (unsigned long)c, (double)diag_ossz_us / c, (unsigned long)diag_max_us,
                       diag_adc_min, diag_adc_max, KUSZOB);
            }
        } else if (strcmp(sor, "ADC") == 0) {
            adc_keres = true;
            for (int i = 0; i < 100 && adc_keres; i++) vTaskDelay(pdMS_TO_TICKS(1));
            if (adc_keres || adc_valasz < 0) {          // próba fut, vagy hibás olvasás
                adc_keres = false;
                printf("NA\n");
            } else {
                printf("ADC %d\n", adc_valasz);
            }
        } else {
            char *vege;
            double x = strtod(sor, &vege);
            if (vege != sor && *vege == '\0') {         // érvényes szám: új próba
                korr = x;
                uj_proba = true;
            }
        }
        fflush(stdout);
    }
}
