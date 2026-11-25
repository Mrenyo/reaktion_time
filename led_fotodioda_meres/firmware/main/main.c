// "START" parancsra MERESEK_SZAMA-szor: LED be -> vár, amíg a fotodióda eléri a KUSZOB-ot ->
// LED még VILAGIT_MS-ig ég -> LED ki -> SZUNET_MS szünet.
// Minden mért időt rögtön elküld egy sorban (µs, -1 = nem jött fény 1 s alatt), a végén "VEGE".

#include <stdio.h>
#include <string.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "driver/gpio.h"
#include "driver/uart.h"
#include "driver/uart_vfs.h"
#include "esp_timer.h"
#include "esp_adc/adc_oneshot.h"
#include "hal/adc_ll.h"

int MERESEK_SZAMA = 500;
int KUSZOB        = 150;
int VILAGIT_MS    = 1000;   // ennyi ideig ég a LED (a mérés közben már kész)
int SZUNET_MS     = 500;    // szünet a LED kikapcsolása után

#define LED_GPIO   GPIO_NUM_15
#define FOTODIODA  ADC_CHANNEL_3   // GPIO4

// Gyors olvasás közvetlenül a regiszterből. Az IDF adc_oneshot_read() minden olvasásnál újra
// beállítja és kalibrálja az ADC-t; ezt itt egyszer, az első adc_oneshot_read() végzi el.
static inline int gyors_olvas(void)
{
    adc_oneshot_ll_start(ADC_UNIT_1);
    while (!adc_oneshot_ll_get_event(ADC_LL_EVENT_ADC1_ONESHOT_DONE)) { }
    return adc_oneshot_ll_get_raw_result(ADC_UNIT_1);
}

void app_main(void)
{
    gpio_reset_pin(LED_GPIO);
    gpio_set_direction(LED_GPIO, GPIO_MODE_OUTPUT);
    gpio_set_level(LED_GPIO, 0);

    adc_oneshot_unit_handle_t adc;
    adc_oneshot_unit_init_cfg_t adc_cfg = { .unit_id = ADC_UNIT_1 };
    adc_oneshot_new_unit(&adc_cfg, &adc);
    adc_oneshot_chan_cfg_t ch_cfg = { .atten = ADC_ATTEN_DB_0, .bitwidth = ADC_BITWIDTH_12 };
    adc_oneshot_config_channel(adc, FOTODIODA, &ch_cfg);
    int v;
    adc_oneshot_read(adc, FOTODIODA, &v);   // beállítás és kalibrálás

    uart_driver_install(UART_NUM_0, 256, 0, 0, NULL, 0);
    uart_vfs_dev_use_driver(UART_NUM_0);

    char sor[32];
    while (1) {
        if (fgets(sor, sizeof sor, stdin) == NULL || strncmp(sor, "START", 5) != 0) {
            clearerr(stdin);
            vTaskDelay(pdMS_TO_TICKS(10));
            continue;
        }

        for (int i = 0; i < MERESEK_SZAMA; i++) {
            int64_t t0 = esp_timer_get_time();
            gpio_set_level(LED_GPIO, 1);
            int64_t eltelt = -1;
            while (esp_timer_get_time() - t0 < 1000000) {
                if (gyors_olvas() >= KUSZOB) {
                    eltelt = esp_timer_get_time() - t0;
                    break;
                }
            }
            vTaskDelay(pdMS_TO_TICKS(VILAGIT_MS));
            gpio_set_level(LED_GPIO, 0);
            printf("%lld\n", eltelt);
            fflush(stdout);
            vTaskDelay(pdMS_TO_TICKS(SZUNET_MS));
        }
        printf("VEGE\n");
        fflush(stdout);
    }
}
