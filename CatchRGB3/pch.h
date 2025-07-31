#pragma once
#include <cstdint>
#ifdef SCREENCAPTUREWPF_EASY_EXPORTS
// 编译 DLL 时定义此宏以导出符号
#define SCREENCAPTURE_API __declspec(dllexport)
#else
// 使用 DLL 时自动导入符号
#define SCREENCAPTURE_API __declspec(dllimport)
#endif
extern "C"
{
    /// <summary>
    /// 抓取屏幕指定区域 (x,y,width,height)，并按行优先分别输出三个 8-bit 通道矩阵：R,G,B。
    /// 成功返回 true，并通过 outR/outG/outB/outWidth/outHeight 返回数据；
    /// 失败返回 false，不分配缓冲区。
    /// </summary>
    /// <param name="x">屏幕区左上角 X 坐标</param>
    /// <param name="y">屏幕区左上角 Y 坐标</param>
    /// <param name="width">区域宽度</param>
    /// <param name="height">区域高度</param>
    /// <param name="outR">输出 R 通道缓冲（长度 = width*height）</param>
    /// <param name="outG">输出 G 通道缓冲（长度 = width*height）</param>
    /// <param name="outB">输出 B 通道缓冲（长度 = width*height）</param>
    /// <param name="outWidth">返回实际宽度 (同 width)</param>
    /// <param name="outHeight">返回实际高度 (同 height)</param>
    SCREENCAPTURE_API bool __cdecl CaptureFrameRGB(
        int x,
        int y,
        int width,
        int height,
        uint8_t** outR,
        uint8_t** outG,
        uint8_t** outB,
        int* outWidth,
        int* outHeight) noexcept;
    /// <summary>
    /// 释放由 CaptureFrameRGB 分配的单通道缓冲区
    /// </summary>
    /// <param name="buffer">待释放的通道缓冲指针</param>
    SCREENCAPTURE_API void __cdecl FreeBuffer(
        uint8_t* buffer) noexcept;
} // extern "C"

