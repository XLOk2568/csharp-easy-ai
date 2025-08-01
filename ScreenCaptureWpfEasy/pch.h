// pch.h — 预编译头
#ifndef PCH_H
#define PCH_H

#include "framework.h"
#include <cstdint>   // for uint8_t, uint16_t, etc.

#endif // PCH_H

// 在项目属性 → C/C++ → 预处理器 → 预处理器定义里添加：
//
//    SCREENCAPTUREWPF_EASY_EXPORTS
//
// 这样在本项目编译时就会把 SCEAPI 展开为 __declspec(dllexport)
//，在被其它项目引用时则为 __declspec(dllimport)。

#ifdef SCREENCAPTUREWPF_EASY_EXPORTS
#define SCEAPI __declspec(dllexport)
#else
#define SCEAPI __declspec(dllimport)
#endif

extern "C" {
    // 导出 CaptureFrame
    [[nodiscard]] SCEAPI bool __cdecl CaptureFrame(
        int   x,
        int   y,
        int   width,
        int   height,
        uint8_t** outBuffer,
        int* outWidth,
        int* outHeight) noexcept;

    // 导出 FreeBuffer
    SCEAPI void __cdecl FreeBuffer(
        uint8_t* buffer) noexcept;
}

