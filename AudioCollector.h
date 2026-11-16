#pragma once
#include <krabs.hpp>
#include "EventQueue.h"
#include "DataTrace.Native/events.pb.h"

class AudioCollector {
public:
    static void SetupProvider(krabs::provider<>& provider, EventQueue& eventQueue);
};