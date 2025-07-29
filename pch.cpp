// pch.cpp — ScreenCaptureWpfEasy (C++20)
#include "pch.h"
#include <Windows.h>       // HDC / BITMAPINFO / CreateDIBSection…
#include <combaseapi.h>    // CoTaskMemAlloc / CoTaskMemFree
#include <cstdint>
[[nodiscard]] bool __cdecl CaptureFrame(
    int x, int y, int width, int height,
    uint8_t** outBuffer,
    int* outWidth,
    int* outHeight) noexcept
{
    if (!outBuffer || !outWidth || !outHeight)
        return false;
    HDC hScreenDC = GetDC(nullptr); // 1. 拿屏幕 DC & 创建兼容 DC
    HDC hMemDC = hScreenDC ? CreateCompatibleDC(hScreenDC) : nullptr;
    if (!hScreenDC || !hMemDC) {
        if (hScreenDC) ReleaseDC(nullptr, hScreenDC);
        if (hMemDC)    DeleteDC(hMemDC);
        return false;
    }
    BITMAPINFO bmi = {};    // 2. 准备 32bpp 自顶向下 DIBSection
    bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
    bmi.bmiHeader.biWidth = width;
    bmi.bmiHeader.biHeight = -height;      // 负值：自顶向下
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;           // BGRA32 源
    bmi.bmiHeader.biCompression = BI_RGB;
    void* pBits = nullptr;
    HBITMAP hBitmap = CreateDIBSection(
        hMemDC, &bmi, DIB_RGB_COLORS, &pBits, nullptr, 0);
    if (!hBitmap) {
        DeleteDC(hMemDC);
        ReleaseDC(nullptr, hScreenDC);
        return false;
    }
    SelectObject(hMemDC, hBitmap);    // 3. Blt 到 DIB
    if (!BitBlt(hMemDC, 0, 0, width, height, hScreenDC, x, y, SRCCOPY)) {
        DeleteObject(hBitmap);
        DeleteDC(hMemDC);
        ReleaseDC(nullptr, hScreenDC);
        return false;
    }
    size_t pixelCount = size_t(width) * size_t(height);    // 4. 分配 RGB565 缓冲
    size_t bufSize = pixelCount * 2;
    auto* buffer = static_cast<uint8_t*>(CoTaskMemAlloc(bufSize));
    if (!buffer) {
        DeleteObject(hBitmap);
        DeleteDC(hMemDC);
        ReleaseDC(nullptr, hScreenDC);
        return false;
    }
    auto* srcBase = static_cast<uint8_t*>(pBits);    // 5. 转换 BGRA32 → RGB565
    for (int row = 0; row < height; ++row) {
        auto* src = srcBase + size_t(row) * width * 4;
        auto* dst = buffer + size_t(row) * width * 2;
        for (int col = 0; col < width; ++col) {
            uint8_t B = src[0], G = src[1], R = src[2];
            uint16_t rgb565 = uint16_t((R & 0xF8) << 8)
                | uint16_t((G & 0xFC) << 3)
                | uint16_t(B >> 3);
            if (bufSize < 2) 
            {
                return false;
            }
            // 原来的写法保持不变
            dst[0] = uint8_t(rgb565 & 0xFF);
            dst[1] = uint8_t(rgb565 >> 8);
            src += 4;
            dst += 2;
        }
    }
    // 6. 清理 GDI
    DeleteObject(hBitmap);
    DeleteDC(hMemDC);
    ReleaseDC(nullptr, hScreenDC);
    // 7. 输出
    *outBuffer = buffer;
    *outWidth = width;
    *outHeight = height;
    return true;
}
void __cdecl FreeBuffer(uint8_t* buffer) noexcept
{
    CoTaskMemFree(buffer);
}