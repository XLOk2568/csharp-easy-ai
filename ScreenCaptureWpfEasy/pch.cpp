// pch.cpp — ScreenCaptureWpfEasy (C++20)
#include "pch.h"
#include <Windows.h>       // HDC / BITMAPINFO / CreateDIBSection…
#include <combaseapi.h>    // CoTaskMemAlloc / CoTaskMemFree
#include <cstdint>
#include <cstring>         // std::memcpy
constexpr int BYTES_PER_PIXEL_32 = 4;   // ★ 32-bit BGRA
extern "C" {
    [[nodiscard]] bool __cdecl CaptureFrame(
        int x, int y,
        const int width, const int height,
        uint8_t** outBuffer,
        int* outWidth, int* outHeight) noexcept
    {
        if (!outBuffer || !outWidth || !outHeight)
            return false;
        // —— 所有局部变量放到最前面 —— 
        void* pBits = nullptr;
        HBITMAP hBitmap = nullptr;
        constexpr int bytesPerPixel = 2;
        size_t  rowBytes = 0;
        int     stride = 0;
        size_t  bufSize = 0;
        uint8_t* buffer = nullptr;
        uint8_t* srcBase = nullptr;
        // 把 BI16 结构和 bmi16 初始化也挪到这里，避免被 goto 跳过
        struct BI16 {
            BITMAPINFOHEADER hdr;
            DWORD            masks[3];
        } bmi16 = {};
        HDC hScreenDC = GetDC(nullptr);
        if (!hScreenDC)
            return false;
        HDC hMemDC = CreateCompatibleDC(hScreenDC);
        if (!hMemDC)
            goto cleanup_dc;
        // 原来的字段赋值移动后依然保持
        bmi16.hdr.biSize = sizeof(BITMAPINFOHEADER);
        bmi16.hdr.biWidth = width;
        bmi16.hdr.biHeight = -height;
        bmi16.hdr.biPlanes = 1;
        bmi16.hdr.biBitCount = 16;
        bmi16.hdr.biCompression = BI_BITFIELDS;
        bmi16.masks[0] = 0xF800;
        bmi16.masks[1] = 0x07E0;
        bmi16.masks[2] = 0x001F;
        hBitmap = CreateDIBSection(
            hMemDC,
            reinterpret_cast<BITMAPINFO*>(&bmi16),
            DIB_RGB_COLORS,
            &pBits, nullptr, 0
        );
        if (!hBitmap)
            goto cleanup_dc;
        SelectObject(hMemDC, hBitmap);
        if (!BitBlt(hMemDC, 0, 0, width, height, hScreenDC, x, y, SRCCOPY))
            goto cleanup_bitmap;
        rowBytes = size_t(width) * bytesPerPixel;
        stride = ((width * 16 + 31) / 32) * 4;
        bufSize = size_t(rowBytes) * height;
        srcBase = static_cast<uint8_t*>(pBits);
        buffer = static_cast<uint8_t*>(CoTaskMemAlloc(bufSize));
        if (!buffer)
            goto cleanup_bitmap;
        for (int row = 0; row < height; ++row) 
        {
            std::memcpy
            (
                buffer + size_t(row) * rowBytes,
                srcBase + size_t(row) * stride,
                rowBytes
            );
        }
        *outBuffer = buffer;
        *outWidth = width;
        *outHeight = height;
        DeleteObject(hBitmap);
        DeleteDC(hMemDC);
        ReleaseDC(nullptr, hScreenDC);
        return true;
    cleanup_bitmap:
        DeleteObject(hBitmap);
    cleanup_dc:
        if (hMemDC)    DeleteDC(hMemDC);
        if (hScreenDC) ReleaseDC(nullptr, hScreenDC);
        return false;
    }//以下是RGB矩阵接口
    // ★ 新增：提取 RGB 三通道矩阵（每像素 3 字节：R,G,B）
    [[nodiscard]] bool __cdecl RGBData(
        int x, int y,
        const int width, const int height,
        uint8_t** outMatrix,   // 返回裸指针，CoTaskMemAlloc
        int* outW, int* outH) noexcept
    {
        if (!outMatrix || !outW || !outH || width <= 0 || height <= 0)
            return false;
        HDC hScreenDC = GetDC(nullptr);
        if (!hScreenDC) return false;
        HDC hMemDC = CreateCompatibleDC(hScreenDC);
        if (!hMemDC) { ReleaseDC(nullptr, hScreenDC); return false; }
        /* --------- 32-bit DIB --------- */
        BITMAPINFO bmi{};
        bmi.bmiHeader.biSize = sizeof(BITMAPINFOHEADER);
        bmi.bmiHeader.biWidth = width;
        bmi.bmiHeader.biHeight = -height;          // top-down
        bmi.bmiHeader.biPlanes = 1;
        bmi.bmiHeader.biBitCount = 32;               // BGRA
        bmi.bmiHeader.biCompression = BI_RGB;
        void* pBits = nullptr;
        HBITMAP hBmp = CreateDIBSection(
            hMemDC, &bmi, DIB_RGB_COLORS,
            &pBits, nullptr, 0);
        if (!hBmp) { DeleteDC(hMemDC); ReleaseDC(nullptr, hScreenDC); return false; }
        SelectObject(hMemDC, hBmp);
        if (!BitBlt(hMemDC, 0, 0, width, height, hScreenDC, x, y, SRCCOPY))
        {
            DeleteObject(hBmp); DeleteDC(hMemDC); ReleaseDC(nullptr, hScreenDC);
            return false;
        }
        const int stride = ((width * 32 + 31) / 32) * 4;
        const size_t rowRGB = size_t(width) * 3;
        const size_t bufSz = rowRGB * height;
        uint8_t* dst = static_cast<uint8_t*>(CoTaskMemAlloc(bufSz));
        if (!dst)
        {
            DeleteObject(hBmp); DeleteDC(hMemDC); ReleaseDC(nullptr, hScreenDC);
            return false;
        }
        uint8_t* srcBase = static_cast<uint8_t*>(pBits);
        for (int row = 0; row < height; ++row)
        {
            uint8_t* src = srcBase + row * stride;     // BGRA...
            uint8_t* d = dst + row * rowRGB;
            for (int col = 0; col < width; ++col, src += BYTES_PER_PIXEL_32, d += 3)
            {
                d[0] = src[2]; // R
                d[1] = src[1]; // G
                d[2] = src[0]; // B
            }
        }
        *outMatrix = dst;
        *outW = width;
        *outH = height;
        DeleteObject(hBmp);
        DeleteDC(hMemDC);
        ReleaseDC(nullptr, hScreenDC);
        return true;
    }
    void __cdecl FreeBuffer(uint8_t* buffer) noexcept
    {
        CoTaskMemFree(buffer);
    }
} // extern "C"


