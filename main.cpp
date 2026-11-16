#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <krabs.hpp>
#include <iostream>
#include <chrono>
#include "EventQueue.h"
#include "PipeWriter.h"
#include "ClipboardListener.h"
#include "Win32kCollector.h"
#include "DataTrace.Native/events.pb.h"
#include "CameraCollector.h"
#include "AudioCollector.h"

#pragma comment(lib, "ws2_32.lib")


#include <psapi.h> // Для GetModuleBaseName
#pragma comment(lib, "psapi.lib")

// Функция для получения имени процесса по PID
std::wstring GetProcessNameFromPid(DWORD pid) {
    // Пытаемся открыть процесс с МИНИМАЛЬНЫМИ правами (без VM_READ)
    HANDLE hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, pid);
    if (hProcess == NULL) {
        // Если не удалось (это системный или уже закрытый процесс)
        DWORD err = GetLastError();
        if (err == ERROR_ACCESS_DENIED) return L"<Protected>";
        return L"<Exited>";
    }

    WCHAR buffer[MAX_PATH];
    DWORD size = MAX_PATH;
    std::wstring processName = L"<Unknown>";

    if (QueryFullProcessImageNameW(hProcess, 0, buffer, &size)) {
        std::wstring fullPath(buffer);
        size_t pos = fullPath.find_last_of(L"\\/");
        if (pos != std::wstring::npos) {
            processName = fullPath.substr(pos + 1);
        }
        else {
            processName = fullPath;
        }
    }
    CloseHandle(hProcess);
    return processName;
}

// Глобальный указатель, чтобы обработчик мог достучаться до трейса
krabs::user_trace* g_trace = nullptr;

// Обработчик системных сигналов (закрытие окна, Ctrl+C)
BOOL WINAPI ConsoleCtrlHandler(DWORD dwCtrlType) {
    switch (dwCtrlType) {
    case CTRL_C_EVENT:       // Нажат Ctrl+C
    case CTRL_BREAK_EVENT:   // Нажат Ctrl+Break
    case CTRL_CLOSE_EVENT:   // Закрыто окно консоли (крестик)
    case CTRL_LOGOFF_EVENT:  // Выход пользователя из системы
    case CTRL_SHUTDOWN_EVENT:// Выключение компьютера
        if (g_trace) {
            std::cout << "\n[Native] Graceful shutdown: stopping ETW session...\n";
            g_trace->stop(); // Корректно останавливаем сессию!
        }
        // Даем ОС 5 секунд, чтобы процесс успел завершиться
        return TRUE;
    default:
        return FALSE;
    }
}

// Помощник для конвертации wstring в UTF-8 string
std::string WStringToString(const std::wstring& wstr) {
    if (wstr.empty()) return std::string();
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), NULL, 0, NULL, NULL);
    std::string strTo(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, &wstr[0], (int)wstr.size(), &strTo[0], size_needed, NULL, NULL);
    return strTo;
}

int main() {
    SetConsoleOutputCP(CP_UTF8);
    std::cout << "[Native] Pipeline started...\n";

    // 1. Инициализация компонентов
    EventQueue eventQueue;
    PipeWriter pipeWriter(eventQueue);
    pipeWriter.Start();

    ClipboardListener clipboard;
    clipboard.Start([&eventQueue](const std::string& text) {
        datatrace::ClipboardEvent c_evt;
        c_evt.set_text(text);
        auto now = std::chrono::system_clock::now();
        c_evt.set_timestamp_utc(std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()).count());

        std::cout << "[CLIPBOARD] Copied text (" << text.length() << " chars)\n";

        datatrace::EventWrapper wrapper;
        *wrapper.mutable_clipboard_event() = c_evt;
        eventQueue.Push(wrapper);
        });

    // 2. Настройка ETW
    std::wstring sessionName = L"DataTraceSession_" + std::to_wstring(GetCurrentProcessId());
    krabs::user_trace trace(sessionName);

    g_trace = &trace; // Сохраняем указатель для обработчика
    SetConsoleCtrlHandler(ConsoleCtrlHandler, TRUE); // Регистрируем обработчик


    // --- Провайдер процессов ---
    krabs::provider<> process_provider(L"Microsoft-Windows-Kernel-Process");
    process_provider.any(0x10);
    process_provider.add_on_event_callback([&eventQueue](const EVENT_RECORD& record, const krabs::trace_context& trace_context) {
        krabs::schema schema(record, trace_context.schema_locator);
        krabs::parser parser(schema);

        if (schema.event_opcode() == 1) { // Process Start
            try {
                uint32_t pid = parser.parse<uint32_t>(L"ProcessID");
                uint32_t ppid = parser.parse<uint32_t>(L"ParentProcessID");

                std::wstring image_name = L"";

                // 1. Пытаемся прочитать имя прямо из ETW-события (самый быстрый способ)
                try {
                    image_name = parser.parse<std::wstring>(L"ImageName");
                }
                catch (...) {
                    // Если в событии нет ImageName, идем в ОС
                }

                // 2. Если ETW не дал имя, запрашиваем у ОС
                if (image_name.empty()) {
                    image_name = GetProcessNameFromPid(pid);
                }

                datatrace::ProcessEvent p_evt;
                p_evt.set_pid(pid);
                p_evt.set_parent_pid(ppid);
                p_evt.set_image_name(WStringToString(image_name));
                p_evt.set_timestamp_utc(std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count());

                std::cout << "[ETW] Process found: " << WStringToString(image_name) << " (PID: " << pid << ")\n";

                datatrace::EventWrapper wrapper;
                *wrapper.mutable_process_event() = p_evt;
                eventQueue.Push(wrapper);
            }
            catch (const std::exception& e) {
                std::cout << "[ETW] Parse error: " << e.what() << "\n";
            }
        }
        });

    // --- Провайдер сети ---
    krabs::provider<> network_provider(L"Microsoft-Windows-Kernel-Network");
    network_provider.any(0x10);
    network_provider.add_on_event_callback([&eventQueue](const EVENT_RECORD& record, const krabs::trace_context& trace_context) {
        krabs::schema schema(record, trace_context.schema_locator);
        krabs::parser parser(schema);
        if (schema.event_id() == 12 || schema.event_id() == 13) {
            try {
                datatrace::NetworkEvent n_evt;
                n_evt.set_pid(parser.parse<uint32_t>(L"PID"));
                n_evt.set_local_port(parser.parse<uint16_t>(L"sport"));
                n_evt.set_remote_port(parser.parse<uint16_t>(L"dport"));

                uint32_t daddr = parser.parse<uint32_t>(L"daddr");
                uint32_t saddr = parser.parse<uint32_t>(L"saddr");

                if (schema.event_id() == 12) {
                    daddr = ntohl(daddr); saddr = ntohl(saddr);
                    n_evt.set_remote_ip(std::to_string((daddr >> 24) & 0xFF) + "." + std::to_string((daddr >> 16) & 0xFF) + "." + std::to_string((daddr >> 8) & 0xFF) + "." + std::to_string(daddr & 0xFF));
                    n_evt.set_local_ip(std::to_string((saddr >> 24) & 0xFF) + "." + std::to_string((saddr >> 16) & 0xFF) + "." + std::to_string((saddr >> 8) & 0xFF) + "." + std::to_string(saddr & 0xFF));
                }
                else {
                    n_evt.set_remote_ip("IPv6"); n_evt.set_local_ip("IPv6");
                }

                n_evt.set_protocol(6);
                n_evt.set_timestamp_utc(std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count());

                std::cout << "[NET] PID: " << n_evt.pid() << " -> " << n_evt.remote_ip() << ":" << n_evt.remote_port() << "\n";

                datatrace::EventWrapper wrapper;
                *wrapper.mutable_network_event() = n_evt;
                eventQueue.Push(wrapper);
            }
            catch (...) {}
        }
        });
    krabs::provider<> win32k_provider(L"Microsoft-Windows-Win32k");
    Win32kCollector::SetupProvider(win32k_provider, eventQueue);

    krabs::provider<> camera_provider(L"Microsoft-Windows-USBVideo");
    CameraCollector::SetupProvider(camera_provider, eventQueue);

    krabs::provider<> audio_provider(L"Microsoft-Windows-Audio");
    AudioCollector::SetupProvider(audio_provider, eventQueue);
    // 3. Запуск ETW
    try {
        trace.enable(process_provider);
        trace.enable(network_provider);
        trace.enable(win32k_provider);
        std::cout << "[Native] Waiting for system events...\n";
        trace.start(); // Блокирующий вызов
    }
    catch (const std::exception& e) {
        std::cout << "\n!!! CRITICAL ETW ERROR !!!\n" << e.what() << "\n";
        std::cout << "DID YOU RUN AS ADMINISTRATOR?\n";
        std::cin.get();
    }

    return 0;
}