// Scanout-korrekció validálása két fotodiódával.
//
// A mérő ciklus a reakcióidő-mérő firmware-ével (a repó firmware/main/main.c fájlja) azonos módszerrel
// dolgozik: egyenkénti (oneshot) ADC-olvasás, a fényjel az első fekete -> fehér küszöbátlépés
// (előbb a küszöb alatti, sötét jelet kell látnia), az időbélyeg az esp_timer órájából közvetlenül a
// küszöböt elérő olvasás után. A különbség: két fotodióda-csatornát olvas felváltva, és mindkettőnek
// saját időbélyeget ad, közvetlenül a saját olvasása után.
//
//   REF  - a referencia-fotodióda (a szokásos jelzőhely, jobb alsó sarok): GPIO4 = ADC1 3-as csatorna
//   MERO - a mérő fotodióda (a vizsgált képernyőpont):                    GPIO6 = ADC1 5-ös csatorna
//
// Soros protokoll (UART0, 115200 baud, soronként, "\n" zárja):
//   "CHECK"         -> "OK"
//   "VERSION"       -> FW_VERSION (pl. "SV-1.0.0")
//   "THR <ref> <mero>" -> "THR <ref> <mero>"     a két küszöb (nyers ADC, 0-4095); alapértelmezés 475
//   "ADC"           -> "ADC <ref> <mero>"      egy-egy nyers olvasás; "NA", ha próba fut
//   "ARM"           -> "ARMED"                 új próba: mindkét csatorna a fekete -> fehér átmenetre vár
//   "RESULT"        -> "RES dt=<µs> ..."       a két időbélyeg és különbségük, vagy "RES NA ...";
//                                              utána az ESP tétlen
//   "STOP"          -> "STOPPED"               a próba megszakítása
// Kérés nélkül az ESP semmit nem küld.

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <ctype.h>
#include <stdbool.h>
#include <stdint.h>
#include <limits.h>
#include <inttypes.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/uart.h"
#include "driver/uart_vfs.h"
#include "esp_timer.h"
#include "esp_adc/adc_oneshot.h"

#define FW_VERSION    "SV-1.0.0"

#define FD_REF        ADC_CHANNEL_3   // GPIO4: ugyanaz, mint a reakcióidő-mérőben
#define FD_MERO       ADC_CHANNEL_5   // GPIO6: a második fotodióda-erősítő kimenete
#define ALAP_KUSZOB   475             // a reakcióidő-mérő küszöbe (sötét ~0, világos ~950)

#define REF  0
#define MERO 1

typedef enum { TETLEN, VAR_FENY, KESZ } allapot_t;

// Egy csatorna eredménye. A mérő task írja, a "kesz" jelzőt utolsóként állítja be; a soros task
// csak a "kesz" után olvassa a többi mezőt.
typedef struct {
    adc_channel_t csatorna;
    volatile int kuszob;
    volatile bool kesz;
    volatile int64_t t;          // a küszöböt elérő olvasás utáni időbélyeg (µs)
    volatile int64_t t_elozo;    // az előző (még küszöb alatti) olvasás utáni időbélyeg (µs)
    volatile int v, v_elozo;     // a két olvasás nyers értéke
    volatile int adc_min, adc_max;
    bool sotet_volt;             // csak a mérő task használja
    int64_t utolso_t;            // csak a mérő task használja
    int utolso_v;
} csatorna_t;

static csatorna_t cs[2] = {
    [REF]  = { .csatorna = FD_REF,  .kuszob = ALAP_KUSZOB },
    [MERO] = { .csatorna = FD_MERO, .kuszob = ALAP_KUSZOB },
};

static volatile allapot_t allapot = TETLEN;
static volatile bool uj_proba = false;      // a soros task kéri, a mérő task hajtja végre
static volatile bool leallitas = false;
static adc_oneshot_unit_handle_t adc;

// Diagnosztika a próbáról: a mérő ciklus lefutásai (egy lefutás = mindkét csatorna egy olvasása)
static volatile uint32_t diag_ciklusok, diag_ossz_us, diag_max_us;

static volatile bool adc_keres = false;
static volatile int adc_valasz[2];

static void csatorna_nullaz(csatorna_t *c)
{
    c->kesz = false;
    c->sotet_volt = false;
    c->adc_min = INT_MAX;
    c->adc_max = INT_MIN;
    c->utolso_t = 0;
    c->utolso_v = -1;
}

// Egy csatorna egy mintája, a reakcióidő-mérő firmware-ével azonos szabállyal. Az olvasás akkor is
// megtörténik, ha a csatorna már kész, így a ciklusidő a próba alatt nem változik.
static void csatorna_olvas(csatorna_t *c)
{
    int ertek;
    if (adc_oneshot_read(adc, c->csatorna, &ertek) != ESP_OK) {
        return;     // hibás olvasásnál a minta kimarad
    }
    int64_t t = esp_timer_get_time();
    if (c->kesz) {
        return;
    }

    if (ertek < c->adc_min) c->adc_min = ertek;
    if (ertek > c->adc_max) c->adc_max = ertek;
    if (ertek < c->kuszob) {
        c->sotet_volt = true;
    } else if (c->sotet_volt) {
        c->t = t;
        c->t_elozo = c->utolso_t;
        c->v = ertek;
        c->v_elozo = c->utolso_v;
        __sync_synchronize();
        c->kesz = true;
    }
    c->utolso_t = t;
    c->utolso_v = ertek;
}

// Folyamatosan fut az 1-es magon
static void mero_task(void *arg)
{
    int64_t elozo = 0, kezdet = 0;
    bool elso = true;

    while (1) {
        int64_t most = esp_timer_get_time();

        if (uj_proba) {
            csatorna_nullaz(&cs[REF]);
            csatorna_nullaz(&cs[MERO]);
            diag_ciklusok = 0;
            diag_ossz_us = 0;
            diag_max_us = 0;
            elso = true;
            kezdet = most;
            allapot = VAR_FENY;
            uj_proba = false;
        }
        if (leallitas) {
            allapot = TETLEN;
            leallitas = false;
        }

        if (allapot == VAR_FENY) {
            if (!elso) {
                uint32_t dt = (uint32_t)(most - elozo);
                if (dt > diag_max_us) diag_max_us = dt;
                diag_ciklusok = diag_ciklusok + 1;
                diag_ossz_us = (uint32_t)(most - kezdet);
            }
            elso = false;

            // A két csatorna felváltva; mindkettő a saját olvasása utáni időbélyeget kapja
            csatorna_olvas(&cs[REF]);
            csatorna_olvas(&cs[MERO]);
            if (cs[REF].kesz && cs[MERO].kesz) {
                allapot = KESZ;
            }
        } else if (adc_keres) {
            int a, b;
            adc_valasz[REF] = adc_oneshot_read(adc, FD_REF, &a) == ESP_OK ? a : -1;
            adc_valasz[MERO] = adc_oneshot_read(adc, FD_MERO, &b) == ESP_OK ? b : -1;
            adc_keres = false;
        }
        elozo = most;
    }
}

static void eredmeny_kuld(void)
{
    uint32_t c = diag_ciklusok;
    double atlag = c > 0 ? (double)diag_ossz_us / c : 0;

    if (!cs[REF].kesz || !cs[MERO].kesz) {
        printf("RES NA ref=%d mero=%d ciklus=%" PRIu32 " atlag_us=%.2f max_us=%" PRIu32
               " min_ref=%d max_ref=%d min_mero=%d max_mero=%d\n",
               cs[REF].kesz ? 1 : 0, cs[MERO].kesz ? 1 : 0, c, atlag, diag_max_us,
               cs[REF].adc_min, cs[REF].adc_max, cs[MERO].adc_min, cs[MERO].adc_max);
        return;
    }

    int64_t dt = cs[MERO].t - cs[REF].t;
    printf("RES dt=%" PRId64
           " t_ref=%" PRId64 " tp_ref=%" PRId64 " v_ref=%d vp_ref=%d"
           " t_mero=%" PRId64 " tp_mero=%" PRId64 " v_mero=%d vp_mero=%d"
           " thr_ref=%d thr_mero=%d"
           " ciklus=%" PRIu32 " atlag_us=%.2f max_us=%" PRIu32
           " min_ref=%d max_ref=%d min_mero=%d max_mero=%d\n",
           dt,
           cs[REF].t, cs[REF].t_elozo, cs[REF].v, cs[REF].v_elozo,
           cs[MERO].t, cs[MERO].t_elozo, cs[MERO].v, cs[MERO].v_elozo,
           cs[REF].kuszob, cs[MERO].kuszob,
           c, atlag, diag_max_us,
           cs[REF].adc_min, cs[REF].adc_max, cs[MERO].adc_min, cs[MERO].adc_max);
}

void app_main(void)
{
    // Fotodiódák: 12 bit, 0 dB (kb. 0-0,95 V), mint a reakcióidő-mérőben
    adc_oneshot_unit_init_cfg_t adc_cfg = { .unit_id = ADC_UNIT_1 };
    ESP_ERROR_CHECK(adc_oneshot_new_unit(&adc_cfg, &adc));
    adc_oneshot_chan_cfg_t ch_cfg = { .atten = ADC_ATTEN_DB_0, .bitwidth = ADC_BITWIDTH_12 };
    ESP_ERROR_CHECK(adc_oneshot_config_channel(adc, FD_REF, &ch_cfg));
    ESP_ERROR_CHECK(adc_oneshot_config_channel(adc, FD_MERO, &ch_cfg));

    ESP_ERROR_CHECK(uart_driver_install(UART_NUM_0, 256, 0, 0, NULL, 0));
    uart_vfs_dev_use_driver(UART_NUM_0);

    xTaskCreatePinnedToCore(mero_task, "mero", 4096, NULL, 1, NULL, 1);

    char sor[96];
    while (1) {
        if (fgets(sor, sizeof sor, stdin) == NULL) {
            clearerr(stdin);
            continue;
        }
        int n = strlen(sor);
        while (n > 0 && isspace((unsigned char)sor[n - 1])) sor[--n] = '\0';

        if (strcmp(sor, "CHECK") == 0) {
            printf("OK\n");
        } else if (strcmp(sor, "VERSION") == 0) {
            printf(FW_VERSION "\n");
        } else if (strcmp(sor, "ARM") == 0) {
            uj_proba = true;
            while (uj_proba) vTaskDelay(1);     // a mérő task a következő lefutásban nulláz
            printf("ARMED\n");
        } else if (strcmp(sor, "RESULT") == 0) {
            eredmeny_kuld();
            leallitas = true;
        } else if (strcmp(sor, "STOP") == 0) {
            leallitas = true;
            printf("STOPPED\n");
        } else if (strncmp(sor, "THR ", 4) == 0) {
            int a, b;
            if (sscanf(sor + 4, "%d %d", &a, &b) == 2 && a > 0 && a < 4096 && b > 0 && b < 4096) {
                cs[REF].kuszob = a;
                cs[MERO].kuszob = b;
            }
            printf("THR %d %d\n", cs[REF].kuszob, cs[MERO].kuszob);
        } else if (strcmp(sor, "ADC") == 0) {
            if (allapot == VAR_FENY) {
                printf("NA\n");
            } else {
                adc_keres = true;
                for (int i = 0; i < 100 && adc_keres; i++) vTaskDelay(pdMS_TO_TICKS(1));
                if (adc_keres || adc_valasz[REF] < 0 || adc_valasz[MERO] < 0) {
                    adc_keres = false;
                    printf("NA\n");
                } else {
                    printf("ADC %d %d\n", adc_valasz[REF], adc_valasz[MERO]);
                }
            }
        }
        fflush(stdout);
    }
}
