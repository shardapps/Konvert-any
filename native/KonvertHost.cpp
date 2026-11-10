#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <shlobj.h>
#include <exdisp.h>
#include <shobjidl.h>
#include <wrl/client.h>
#include <algorithm>
#include <array>
#include <string>
#include <vector>
#include <fstream>
#include <filesystem>

using Microsoft::WRL::ComPtr;
namespace {
constexpr wchar_t WindowClass[] = L"Shardapps.Konvert.HotkeyHost";
constexpr UINT TrayMessage = WM_APP + 1;
constexpr UINT ForegroundChanged = WM_APP + 2;
constexpr UINT PopupDismissed = WM_APP + 3;
constexpr UINT BackgroundResult = WM_APP + 4;
constexpr int HotkeyId = 41;
HWND hostWindow{};
HWINEVENTHOOK foregroundHook{};
bool registered{};
bool paused{};
struct PopupProcess { HANDLE process{}; DWORD id{}; bool dismissed{}; };
std::array<PopupProcess, 2> popups{};
NOTIFYICONDATAW tray{sizeof(NOTIFYICONDATAW)};

struct Selection { std::wstring path; POINT anchor{}; LONG itemTop{}; };

std::wstring Quote(const std::wstring& value) {
    std::wstring result = L"\"";
    size_t slashes = 0;
    for (wchar_t c : value) {
        if (c == L'\\') { ++slashes; continue; }
        if (c == L'\"') result.append(slashes * 2 + 1, L'\\');
        else result.append(slashes, L'\\');
        slashes = 0;
        result += c;
    }
    result.append(slashes * 2, L'\\');
    return result + L'\"';
}

std::wstring OwnPath() {
    std::vector<wchar_t> buffer(32768);
    const DWORD count = GetModuleFileNameW(nullptr, buffer.data(), static_cast<DWORD>(buffer.size()));
    return std::wstring(buffer.data(), count);
}

bool IsExplorer(HWND window) {
    if (!window) return false;
    wchar_t name[128]{};
    GetClassNameW(window, name, 128);
    return wcscmp(name, L"CabinetWClass") == 0 || wcscmp(name, L"ExploreWClass") == 0 ||
        wcscmp(name, L"Progman") == 0 || wcscmp(name, L"WorkerW") == 0;
}

bool ReadView(IShellBrowser* browser, Selection& selection) {
    ComPtr<IShellView> view;
    if (FAILED(browser->QueryActiveShellView(&view))) return false;
    HWND viewWindow{};
    if (FAILED(view->GetWindow(&viewWindow)) || !IsWindowVisible(viewWindow)) return false;
    ComPtr<IFolderView2> folder;
    if (FAILED(view.As(&folder))) return false;
    ComPtr<IShellItemArray> items;
    if (FAILED(folder->GetSelection(FALSE, &items)) || !items) return false;
    DWORD count{};
    if (FAILED(items->GetCount(&count)) || count != 1) return false;
    ComPtr<IShellItem> item;
    if (FAILED(items->GetItemAt(0, &item))) return false;
    SFGAOF attributes{};
    if (FAILED(item->GetAttributes(SFGAO_FOLDER | SFGAO_FILESYSTEM, &attributes)) ||
        (attributes & SFGAO_FOLDER) || !(attributes & SFGAO_FILESYSTEM)) return false;
    PWSTR path{};
    if (FAILED(item->GetDisplayName(SIGDN_FILESYSPATH, &path))) return false;
    selection.path = path;
    CoTaskMemFree(path);
    GetCursorPos(&selection.anchor);
    selection.itemTop = selection.anchor.y;
    int index{};
    PITEMID_CHILD pidl{};
    POINT position{};
    if (SUCCEEDED(folder->GetSelectedItem(-1, &index)) && SUCCEEDED(folder->Item(index, &pidl))) {
        RECT viewBounds{};
        if (SUCCEEDED(folder->GetItemPosition(pidl, &position)) && GetClientRect(viewWindow, &viewBounds)) {
            const auto reportedDpi = GetDpiForWindow(viewWindow);
            const int dpi = reportedDpi ? static_cast<int>(reportedDpi) : 96;
            POINT spacing{MulDiv(64, dpi, 96), MulDiv(28, dpi, 96)};
            POINT measured{};
            if (SUCCEEDED(folder->GetSpacing(&measured)) && measured.x > 0 && measured.y > 0)
                spacing = measured;
            if (position.y < viewBounds.bottom && position.y + spacing.y > viewBounds.top) {
                // In Details view the reported width can span the row. Keep the anchor near its filename.
                const LONG width = std::clamp(spacing.x, 1L, static_cast<LONG>(MulDiv(160, dpi, 96)));
                POINT top{position.x, std::clamp(position.y, viewBounds.top, viewBounds.bottom)};
                POINT bottom{std::clamp(position.x + width / 2, viewBounds.left, viewBounds.right),
                    std::clamp(position.y + spacing.y, viewBounds.top, viewBounds.bottom)};
                if (ClientToScreen(viewWindow, &top) && ClientToScreen(viewWindow, &bottom)) {
                    selection.anchor = bottom;
                    selection.itemTop = top.y;
                }
            }
        }
        CoTaskMemFree(pidl);
    }
    return true;
}

bool SelectedFile(Selection& selection) {
    const HWND foreground = GetForegroundWindow();
    if (!IsExplorer(foreground)) return false;
    ComPtr<IShellWindows> windows;
    if (FAILED(CoCreateInstance(CLSID_ShellWindows, nullptr, CLSCTX_LOCAL_SERVER, IID_PPV_ARGS(&windows)))) return false;
    long count{};
    if (FAILED(windows->get_Count(&count))) return false;
    for (long i = 0; i < count; ++i) {
        VARIANT index{}; index.vt = VT_I4; index.lVal = i;
        ComPtr<IDispatch> dispatch;
        if (FAILED(windows->Item(index, &dispatch)) || !dispatch) continue;
        ComPtr<IWebBrowserApp> web;
        SHANDLE_PTR handle{};
        if (FAILED(dispatch.As(&web)) || FAILED(web->get_HWND(&handle))) continue;
        const HWND browserWindow = reinterpret_cast<HWND>(handle);
        if (GetAncestor(browserWindow, GA_ROOT) != GetAncestor(foreground, GA_ROOT)) continue;
        ComPtr<IServiceProvider> provider;
        ComPtr<IShellBrowser> browser;
        if (SUCCEEDED(dispatch.As(&provider)) && SUCCEEDED(provider->QueryService(SID_STopLevelBrowser, IID_PPV_ARGS(&browser))) &&
            ReadView(browser.Get(), selection)) return true;
    }
    // Never substitute an unrelated desktop selection for an empty Explorer selection.
    wchar_t foregroundClass[128]{};
    GetClassNameW(foreground, foregroundClass, 128);
    if (wcscmp(foregroundClass, L"Progman") != 0 && wcscmp(foregroundClass, L"WorkerW") != 0) return false;
    // Desktop selections are exposed separately from ordinary Explorer windows.
    VARIANT location{}, root{};
    location.vt = VT_I4;
    location.lVal = CSIDL_DESKTOP;
    long desktopHandle{};
    ComPtr<IDispatch> desktop;
    if (SUCCEEDED(windows->FindWindowSW(&location, &root, SWC_DESKTOP, &desktopHandle, SWFO_NEEDDISPATCH, &desktop)) && desktop) {
        ComPtr<IServiceProvider> provider;
        ComPtr<IShellBrowser> browser;
        if (SUCCEEDED(desktop.As(&provider)) && SUCCEEDED(provider->QueryService(SID_STopLevelBrowser, IID_PPV_ARGS(&browser))))
            return ReadView(browser.Get(), selection);
    }
    return false;
}

void Notify(const wchar_t* message) {
    tray.uFlags = NIF_INFO;
    wcscpy_s(tray.szInfoTitle, L"Konvert");
    wcsncpy_s(tray.szInfo, message, _TRUNCATE);
    tray.dwInfoFlags = NIIF_INFO;
    Shell_NotifyIconW(NIM_MODIFY, &tray);
}

void UpdateHotkey() {
    const bool wanted = !paused && IsExplorer(GetForegroundWindow());
    if (wanted == registered) return;
    if (wanted) {
        registered = RegisterHotKey(hostWindow, HotkeyId, MOD_CONTROL | MOD_NOREPEAT, 'K') != FALSE;
        if (!registered) Notify(L"Ctrl+K is already reserved. Use Explorer's Konvert command or change the conflicting shortcut.");
    } else { UnregisterHotKey(hostWindow, HotkeyId); registered = false; }
}

void CALLBACK ForegroundEvent(HWINEVENTHOOK, DWORD, HWND, LONG, LONG, DWORD, DWORD) {
    PostMessageW(hostWindow, ForegroundChanged, 0, 0);
}

void Launch(const std::wstring& arguments, PopupProcess* popup = nullptr) {
    const auto executable = (std::filesystem::path(OwnPath()).parent_path() / L"Konvert.exe").wstring();
    std::wstring command = Quote(executable) + L" " + arguments;
    STARTUPINFOW startup{sizeof(startup)};
    PROCESS_INFORMATION process{};
    if (CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr, nullptr, &startup, &process)) {
        CloseHandle(process.hThread);
        if (popup) {
            *popup = {process.hProcess, process.dwProcessId, false};
            AllowSetForegroundWindow(process.dwProcessId);
        } else CloseHandle(process.hProcess);
    } else Notify(L"Konvert.exe could not be opened. Repair or reinstall Konvert.");
}

void OpenSelection() {
    PopupProcess* available{};
    for (auto& popup : popups) {
        if (popup.process && WaitForSingleObject(popup.process, 0) != WAIT_TIMEOUT) {
            CloseHandle(popup.process);
            popup = {};
        }
        if (popup.process && !popup.dismissed) return;
        if (!popup.process) available = &popup;
    }
    if (!available) { Notify(L"Two conversions are running. Try again when one finishes."); return; }
    Selection selection;
    if (!SelectedFile(selection)) { Notify(L"Select one local file in Explorer or on the desktop, then press Ctrl+K."); return; }
    Launch(L"--popup --shortcut --anchor-x " + std::to_wstring(selection.anchor.x) + L" --anchor-y " +
        std::to_wstring(selection.anchor.y) + L" --anchor-top " + std::to_wstring(selection.itemTop) +
        L" " + Quote(selection.path), available);
}

int StopListener(const std::wstring& expectedPath) {
    const HWND window = FindWindowExW(HWND_MESSAGE, nullptr, WindowClass, nullptr);
    if (!window) return 0;
    DWORD processId{};
    GetWindowThreadProcessId(window, &processId);
    HANDLE process = OpenProcess(SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, FALSE, processId);
    if (!process) return IsWindow(window) ? 2 : 0;
    std::vector<wchar_t> path(32768);
    DWORD length = static_cast<DWORD>(path.size());
    const bool identified = QueryFullProcessImageNameW(process, 0, path.data(), &length) != FALSE;
    DWORD result = identified ? WAIT_OBJECT_0 : WaitForSingleObject(process, 0);
    if (identified && _wcsicmp(path.data(), expectedPath.c_str()) == 0) {
        DWORD currentProcessId{};
        GetWindowThreadProcessId(window, &currentProcessId);
        if (currentProcessId == processId) PostMessageW(window, WM_CLOSE, 0, 0);
        result = WaitForSingleObject(process, 5000);
    }
    CloseHandle(process);
    return result == WAIT_OBJECT_0 ? 0 : 2;
}

LRESULT CALLBACK WindowProcedure(HWND window, UINT message, WPARAM wparam, LPARAM lparam) {
    switch (message) {
    case WM_HOTKEY: if (wparam == HotkeyId) OpenSelection(); return 0;
    case ForegroundChanged: UpdateHotkey(); return 0;
    case PopupDismissed:
        for (auto& popup : popups) if (popup.id == wparam) popup.dismissed = true;
        return 0;
    case BackgroundResult:
        if (wparam == 1) Notify(L"Conversion finished. The file was saved to the selected folder.");
        if (wparam == 2) Notify(L"Conversion did not finish. Retry with the popup open to see the error.");
        return 0;
    case TrayMessage:
        if (lparam == WM_LBUTTONUP) Launch(L"");
        if (lparam == WM_RBUTTONUP) {
            HMENU menu = CreatePopupMenu();
            AppendMenuW(menu, MF_STRING, 1, L"Open Konvert");
            AppendMenuW(menu, MF_STRING, 2, L"Settings");
            AppendMenuW(menu, MF_STRING | (paused ? MF_CHECKED : 0), 3, L"Pause Ctrl+K");
            AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
            AppendMenuW(menu, MF_STRING, 4, L"Quit Konvert shortcut listener");
            POINT point{}; GetCursorPos(&point); SetForegroundWindow(window);
            const UINT choice = TrackPopupMenu(menu, TPM_RETURNCMD | TPM_NONOTIFY, point.x, point.y, 0, window, nullptr);
            DestroyMenu(menu);
            if (choice == 1) Launch(L"");
            if (choice == 2) Launch(L"--settings");
            if (choice == 3) { paused = !paused; UpdateHotkey(); }
            if (choice == 4) PostMessageW(window, WM_CLOSE, 0, 0);
        }
        return 0;
    case WM_CLOSE: DestroyWindow(window); return 0;
    case WM_DESTROY: PostQuitMessage(0); return 0;
    default: return DefWindowProcW(window, message, wparam, lparam);
    }
}
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    int count{};
    LPWSTR* args = CommandLineToArgvW(GetCommandLineW(), &count);
    if (!args) return 1;
    const bool stopAt = count > 1 && wcscmp(args[1], L"--stop-at") == 0;
    const std::filesystem::path stopTarget = stopAt && count == 3 ? args[2] : L"";
    const bool exitRequested = count == 2 && wcscmp(args[1], L"--exit") == 0;
    const bool selfTest = count > 1 && wcscmp(args[1], L"--self-test") == 0;
    LocalFree(args);
    if (selfTest) return Quote(L"a b\\") == L"\"a b\\\\\"" && Quote(L"x\"y") == L"\"x\\\"y\"" ? 0 : 1;
    if (exitRequested) return StopListener(OwnPath());
    if (stopAt) {
        if (!stopTarget.is_absolute() || _wcsicmp(stopTarget.filename().c_str(), L"KonvertHost.exe") != 0) return 1;
        return StopListener(stopTarget.lexically_normal().wstring());
    }
    HANDLE mutex = CreateMutexW(nullptr, TRUE, L"Local\\Shardapps.Konvert.HotkeyHost");
    if (!mutex || GetLastError() == ERROR_ALREADY_EXISTS) { if (mutex) CloseHandle(mutex); return 0; }
    if (FAILED(CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED))) { CloseHandle(mutex); return 1; }
    WNDCLASSW klass{}; klass.lpfnWndProc = WindowProcedure; klass.hInstance = instance; klass.lpszClassName = WindowClass;
    RegisterClassW(&klass);
    hostWindow = CreateWindowExW(0, WindowClass, L"Konvert shortcut listener", 0, 0, 0, 0, 0, HWND_MESSAGE, nullptr, instance, nullptr);
    if (!hostWindow) { CoUninitialize(); CloseHandle(mutex); return 1; }
    tray.hWnd = hostWindow; tray.uID = 1; tray.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP; tray.uCallbackMessage = TrayMessage;
    tray.hIcon = LoadIconW(instance, MAKEINTRESOURCEW(101));
    wcscpy_s(tray.szTip, L"Konvert · select a file and press Ctrl+K");
    Shell_NotifyIconW(NIM_ADD, &tray);
    foregroundHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, nullptr, ForegroundEvent, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    UpdateHotkey();
    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0) { TranslateMessage(&message); DispatchMessageW(&message); }
    if (registered) UnregisterHotKey(hostWindow, HotkeyId);
    if (foregroundHook) UnhookWinEvent(foregroundHook);
    Shell_NotifyIconW(NIM_DELETE, &tray);
    for (auto& popup : popups) if (popup.process) CloseHandle(popup.process);
    CoUninitialize(); CloseHandle(mutex);
    return 0;
}
