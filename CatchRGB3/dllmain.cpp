// dllmain.cpp — 保证包含 Windows.h，且与预编译头一致
#include "pch.h"        // 如果你启用了预编译头，确保它包含 <Windows.h>
#include <Windows.h>    // 绝对需要，否则 APIENTRY/LPVOID 等都未定义

BOOL APIENTRY DllMain(
    HMODULE hModule,
    DWORD   ul_reason_for_call,
    LPVOID  lpReserved
)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        // TODO: 这里进程第一次加载 DLL 时做初始化
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
        // 线程创建/销毁时可选逻辑
        break;
    case DLL_PROCESS_DETACH:
        // 进程卸载 DLL 时做清理
        break;
    }
    return TRUE;
}


