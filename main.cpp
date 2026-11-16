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
#include "DataTrace.Native/events.pb.h"

#pragma comment(lib, "ws2_32.lib")

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

    // --- Провайдер процессов ---
    krabs::provider<> process_provider(L"Microsoft-Windows-Kernel-Process");
    process_provider.any(0x10);
    process_provider.add_on_event_callback([&eventQueue](const EVENT_RECORD& record, const krabs::trace_context& trace_context) {
        krabs::schema schema(record, trace_context.schema_locator);
        krabs::parser parser(schema);
        if (schema.event_opcode() == 1) {
            try {
                uint32_t pid = parser.parse<uint32_t>(L"ProcessID");
                uint32_t ppid = parser.parse<uint32_t>(L"ParentProcessID");
                std::wstring image_name = parser.parse<std::wstring>(L"ImageName");

                datatrace::ProcessEvent p_evt;
                p_evt.set_pid(pid);
                p_evt.set_parent_pid(ppid);
                p_evt.set_image_name(WStringToString(image_name));
                p_evt.set_timestamp_utc(std::chrono::duration_cast<std::chrono::milliseconds>(std::chrono::system_clock::now().time_since_epoch()).count());

                std::cout << "[ETW] Process found: " << p_evt.image_name() << " (PID: " << pid << ")\n";

                datatrace::EventWrapper wrapper;
                *wrapper.mutable_process_event() = p_evt;
                eventQueue.Push(wrapper);
            }
            catch (...) {}
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

    // 3. Запуск ETW
    try {
        trace.enable(process_provider);
        trace.enable(network_provider);
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