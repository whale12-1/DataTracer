#include "CameraCollector.h"
#include <iostream>

void CameraCollector::SetupProvider(krabs::provider<>& provider, EventQueue& eventQueue) {
    provider.add_on_event_callback([&eventQueue](const EVENT_RECORD& record, const krabs::trace_context& trace_context) {
        krabs::schema schema(record, trace_context.schema_locator);

        // Ищем события, связанные с завершением заполнения буфера кадра
        // Точный ID может зависеть от версии Windows, часто это 209 (USBVideo_FillUserBufferStop)
        if (schema.event_id() == 209) {
            try {
                krabs::parser parser(schema);
                datatrace::CameraEvent cam_evt;
                // PID процесса, который инициировал захват
                cam_evt.set_pid(parser.parse<uint32_t>(L"ProcessID"));

                auto now = std::chrono::system_clock::now();
                cam_evt.set_timestamp_utc(std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()).count());

                datatrace::EventWrapper wrapper;
                *wrapper.mutable_camera_event() = cam_evt;
                eventQueue.Push(wrapper);

                std::cout << "[CAMERA] Access detected! PID: " << cam_evt.pid() << "\n";
            }
            catch (...) {
                // Игнорируем ошибки парсинга
            }
        }
        });
}