#include "Win32kCollector.h"
#include <iostream>

void Win32kCollector::SetupProvider(krabs::provider<>& provider, EventQueue& eventQueue) {
    // Слушаем события от подсистемы пользовательского ввода
    provider.add_on_event_callback([&eventQueue](const EVENT_RECORD& record, const krabs::trace_context& trace_context) {
        krabs::schema schema(record, trace_context.schema_locator);

        // Нас интересуют ID 1001 (Raw Input), 1002 (Hooks), 1003 (Polling)
        if (schema.event_id() == 1001 || schema.event_id() == 1002 || schema.event_id() == 1003) {
            try {
                krabs::parser parser(schema);
                datatrace::Win32kEvent k_evt;
                k_evt.set_event_id(schema.event_id());

                // PID обычно находится в поле ProcessID
                k_evt.set_pid(parser.parse<uint32_t>(L"ProcessID"));

                std::string details = "";
                std::string api_name = "";

                // Пытаемся вытащить специфичные поля (в зависимости от версии Windows они могут отличаться)
                if (schema.event_id() == 1001) {
                    api_name = "RegisterRawInputDevices";
                    try { details = "Flags: " + std::to_string(parser.parse<uint32_t>(L"Flags")); }
                    catch (...) {}
                }
                else if (schema.event_id() == 1002) {
                    api_name = "SetWindowsHookEx";
                    try {
                        uint16_t filterType = parser.parse<uint16_t>(L"FilterType");
                        if (filterType == 13) details = "WH_KEYBOARD_LL"; // 13 = низкоуровневый хук
                        else details = "FilterType: " + std::to_string(filterType);
                    }
                    catch (...) {}
                }
                else if (schema.event_id() == 1003) {
                    api_name = "GetAsyncKeyState";
                    try { details = "CallCount: " + std::to_string(parser.parse<uint32_t>(L"BackgroundCallCount")); }
                    catch (...) {}
                }

                k_evt.set_api_name(api_name);
                k_evt.set_details(details);

                auto now = std::chrono::system_clock::now();
                k_evt.set_timestamp_utc(std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()).count());

                datatrace::EventWrapper wrapper;
                *wrapper.mutable_win32k_event() = k_evt;
                eventQueue.Push(wrapper);

                std::cout << "[WIN32K] Event: " << schema.event_id() << " | PID: " << k_evt.pid() << " | " << api_name << "\n";
            }
            catch (...) {
                // Игнорируем ошибки парсинга
            }
        }
        });
}