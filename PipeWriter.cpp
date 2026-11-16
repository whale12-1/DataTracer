#include "PipeWriter.h"
#include <iostream>

PipeWriter::PipeWriter(EventQueue& q) : queue(q), hPipe(INVALID_HANDLE_VALUE) {}

void PipeWriter::Start() {
    while (hPipe == INVALID_HANDLE_VALUE) {
        hPipe = CreateFileW(L"\\\\.\\pipe\\DataTraceEvents", GENERIC_WRITE, 0, NULL, OPEN_EXISTING, 0, NULL);
        if (hPipe == INVALID_HANDLE_VALUE) Sleep(1000);
    }
    std::cout << "[Native] Connected to C# Service!\n";

    writerThread = std::thread([this]() {
        datatrace::EventWrapper evt;
        while (running && queue.Pop(evt)) {
            std::string serialized;
            evt.SerializeToString(&serialized);
            uint32_t len = static_cast<uint32_t>(serialized.size());
            DWORD written;
            WriteFile(hPipe, &len, sizeof(len), &written, NULL);
            WriteFile(hPipe, serialized.data(), len, &written, NULL);
        }
        });
}

PipeWriter::~PipeWriter() {
    running = false;
    queue.Stop(); // Будим очередь, если она ждет
    if (writerThread.joinable()) writerThread.join();
    if (hPipe != INVALID_HANDLE_VALUE) CloseHandle(hPipe);
}