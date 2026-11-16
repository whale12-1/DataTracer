#pragma once
#include <windows.h>
#include <thread>
#include <string>
#include "EventQueue.h"

class PipeWriter {
private:
    HANDLE hPipe;
    EventQueue& queue;
    std::thread writerThread;
    bool running = true;

public:
    PipeWriter(EventQueue& q);
    ~PipeWriter();
    void Start();
};