#pragma once
#include <queue>
#include <mutex>
#include <condition_variable>
#include "DataTrace.Native/events.pb.h"

class EventQueue {
private:
    std::queue<datatrace::EventWrapper> queue;
    std::mutex mtx;
    std::condition_variable cv;
    bool stop = false;

public:
    void Push(const datatrace::EventWrapper& evt);
    bool Pop(datatrace::EventWrapper& evt);
    void Stop();
};