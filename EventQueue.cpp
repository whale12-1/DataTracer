#include "EventQueue.h"

void EventQueue::Push(const datatrace::EventWrapper& evt) {
    std::lock_guard<std::mutex> lock(mtx);
    if (queue.size() > 10000) queue.pop(); // Защита от переполнения памяти
    queue.push(evt);
    cv.notify_one();
}

bool EventQueue::Pop(datatrace::EventWrapper& evt) {
    std::unique_lock<std::mutex> lock(mtx);
    cv.wait(lock, [this] { return !queue.empty() || stop; });
    if (stop && queue.empty()) return false;
    evt = queue.front();
    queue.pop();
    return true;
}

void EventQueue::Stop() {
    std::lock_guard<std::mutex> lock(mtx);
    stop = true;
    cv.notify_all();
}