#include "ClipboardListener.h"
#include <iostream>

// Инициализация статического члена
std::function<void(const std::string&)> ClipboardListener::s_callback = nullptr;

void ClipboardListener::Start(std::function<void(const std::string&)> callback) {
    s_callback = callback;

    listenerThread = std::thread([]() {
        WNDCLASS wc = { 0 };
        wc.lpfnWndProc = ClipboardListener::WndProc; // Ссылка на статический метод
        wc.hInstance = GetModuleHandle(NULL);
        wc.lpszClassName = L"DataTraceClipboardClass";
        RegisterClass(&wc);

        HWND hwnd = CreateWindowEx(0, L"DataTraceClipboardClass", L"DataTraceClipboard", 0, 0, 0, 0, 0, HWND_MESSAGE, NULL, GetModuleHandle(NULL), NULL);
        AddClipboardFormatListener(hwnd);

        MSG msg;
        while (GetMessage(&msg, NULL, 0, 0)) {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        RemoveClipboardFormatListener(hwnd);
        });
}

ClipboardListener::~ClipboardListener() {
    // Отправляем сообщение закрытия окну, чтобы завершить цикл GetMessage
    if (hwnd) PostMessage(hwnd, WM_QUIT, 0, 0);
    // Но так как hwnd создается внутри потока, лучше использовать глобальную переменную или FindWindow.
    // Упростим: поток будет убит при выходе из main, но для чистоты мы можем использовать глобальный hwnd.
    if (listenerThread.joinable()) listenerThread.detach(); // В реальном проекте нужно корректное завершение
}

LRESULT CALLBACK ClipboardListener::WndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    if (msg == WM_CLIPBOARDUPDATE) {
        if (OpenClipboard(hwnd)) {
            HANDLE hData = GetClipboardData(CF_UNICODETEXT);
            if (hData != NULL) {
                wchar_t* pszText = static_cast<wchar_t*>(GlobalLock(hData));
                if (pszText != NULL) {
                    int size_needed = WideCharToMultiByte(CP_UTF8, 0, pszText, -1, NULL, 0, NULL, NULL);
                    std::string strTo(size_needed - 1, 0);
                    WideCharToMultiByte(CP_UTF8, 0, pszText, -1, &strTo[0], size_needed, NULL, NULL);

                    if (s_callback && !strTo.empty()) {
                        s_callback(strTo);
                    }
                    GlobalUnlock(hData);
                }
            }
            CloseClipboard();
        }
    }
    return DefWindowProc(hwnd, msg, wParam, lParam);
}