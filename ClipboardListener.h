#pragma once
#include <windows.h>
#include <functional>
#include <thread>
#include <string>

class ClipboardListener {
private:
    std::thread listenerThread;
    HWND hwnd = NULL;

    // Статический коллбэк, чтобы WndProc мог его вызвать
    static std::function<void(const std::string&)> s_callback;
    static LRESULT CALLBACK WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);

public:
    void Start(std::function<void(const std::string&)> callback);
    ~ClipboardListener();
};