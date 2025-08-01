// dllmain.cpp — CaptureRGB3 DLL 入口（最小改动版）

// 1. 一定要在包含 pch.h 之前定义，
//    否则 SCREENCAPTURE_API 会被当成 __declspec(dllimport)
#define SCREENCAPTUREWPF_EASY_EXPORTS

#include "pch.h"        // 内部会为 CaptureFrameRGB/FreeBuffer 生成 __declspec(dllexport)
#include <Windows.h>    // 定义 HMODULE、DisableThreadLibraryCalls、APIENTRY 等
#pragma comment(lib, "Ole32.lib")  // 确保 CoTaskMemFree/Alloc 正常链接

// DLL 入口点：APIENTRY == WINAPI（__stdcall），Windows 要求的签名
BOOL APIENTRY DllMain(
    HMODULE hModule,
    DWORD   ul_reason_for_call,
    LPVOID  lpReserved
) noexcept
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        // 关闭线程通知，减少不必要的开销
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        // 若有全局资源需要清理，可放这里
        break;
    }
    return TRUE;
}

