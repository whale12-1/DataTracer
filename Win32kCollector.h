#pragma once
#include <krabs.hpp>
#include <chrono>
#include "EventQueue.h"
#include "DataTrace.Native/events.pb.h"

class Win32kCollector {
public:
    static void SetupProvider(krabs::provider<>& provider, EventQueue& eventQueue);
};