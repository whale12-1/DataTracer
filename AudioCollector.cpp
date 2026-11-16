#include "AudioCollector.h"
#include <iostream>

void AudioCollector::SetupProvider(krabs::provider<>& provider, EventQueue& eventQueue) {
    provider.add_on_event_callback([&eventQueue](const EVENT_RECORD& record, const krabs::trace_context& trace_context) {
        krabs::schema schema(record, trace_context.schema_locator);

        // Точный ID события для захвата микрофона может отличаться.
        // Нам нужно найти событие, которое указывает на создание потока Capture.
        // Часто это событие с ID 100 или similar. 
        // В реальной разработке этот ID нужно уточнить с помощью EtwExplorer.
        // Для примера мы будем реагировать на все события и искать те, где есть ProcessID.
        try {
            krabs::parser parser(schema);
            // Пытаемся получить PID. Если поле есть, значит событие связано с процессом.
            uint32_t pid = parser.parse<uint32_t>(L"ProcessID");

            datatrace::AudioEvent audio_evt;
            audio_evt.set_pid(pid);

            auto now = std::chrono::system_clock::now();
            audio_evt.set_timestamp_utc(std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()).count());

            datatrace::EventWrapper wrapper;
            *wrapper.mutable_audio_event() = audio_evt;
            eventQueue.Push(wrapper);

            std::cout << "[AUDIO] Access detected! PID: " << pid << "\n";
        }
        catch (...) {
            // Если поля ProcessID нет, это событие нам не подходит
        }
        });
}