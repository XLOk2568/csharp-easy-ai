// pch.cpp — CatchRGB3 (C++20) VS2022 //提醒自己这代码的环境
#include "pch.h"
#include <Windows.h>
#include <combaseapi.h>
#include <cstdint>
#include <memory>
#include <cstring>
struct BI16
{
    BITMAPINFOHEADER hdr{};
    DWORD            masks[3]{};
};
struct ScreenDC
{
    HDC hdc;
    ScreenDC(HWND hwnd = nullptr) : hdc(GetDC(hwnd)) {}
    ~ScreenDC() { if (hdc) ReleaseDC(nullptr, hdc); }
    explicit operator bool() const noexcept { return hdc != nullptr; }
};
struct MemDC
{
    HDC hdc;
    MemDC(HDC src) : hdc(src ? CreateCompatibleDC(src) : nullptr) {}
    ~MemDC() { if (hdc) DeleteDC(hdc); }
    explicit operator bool() const noexcept { return hdc != nullptr; }
};
struct DibSection
{
    HBITMAP bmp;
    void* bits;
    DibSection(HDC dc, BI16 const& bmi): bmp(nullptr), bits(nullptr)
    {
        bmp = CreateDIBSection(dc,reinterpret_cast<BITMAPINFO const*>(&bmi),DIB_RGB_COLORS,&bits, nullptr, 0);
    }
    ~DibSection() { if (bmp) DeleteObject(bmp); }
    explicit operator bool() const noexcept 
    { 
        return bmp != nullptr; 
    }
};
extern "C" {
    [[nodiscard]] bool __cdecl CaptureFrameRGB(int x, int y,int width, int height,uint8_t** outR,uint8_t** outG,uint8_t** outB,int* outWidth,int* outHeight) noexcept
    {
        if (!outR || !outG || !outB || !outWidth || !outHeight)return false;
        ScreenDC scrDC;        // 1 获取屏幕 DC
        if (!scrDC)return false;
        MemDC memDC(scrDC.hdc);        // 2 创建兼容内存 DC
        if (!memDC)return false;
        BI16 bmi{};        // 3 准备 RGB565 DIBInfo
        bmi.hdr.biSize = sizeof(BITMAPINFOHEADER);
        bmi.hdr.biWidth = width;
        bmi.hdr.biHeight = -height;       // top-down
        bmi.hdr.biPlanes = 1;
        bmi.hdr.biBitCount = 16;
        bmi.hdr.biCompression = BI_BITFIELDS;
        bmi.masks[0] = 0xF800;        // R5
        bmi.masks[1] = 0x07E0;        // G6
        bmi.masks[2] = 0x001F;        // B5
        DibSection dib(memDC.hdc, bmi);        //  创建 DIBSection，获取 pBits 指针
        if (!dib)return false;
        SelectObject(memDC.hdc, dib.bmp);        // 5 抓屏
        if (!BitBlt(memDC.hdc, 0, 0, width, height, scrDC.hdc, x, y, SRCCOPY))return false;
        size_t pixelCount = size_t(width) * height;        // 6 分配三块 8-bit 缓冲（用 unique_ptr + CoTaskMemFree 自动管理）
        using CoFreePtr = std::unique_ptr<uint8_t, decltype(&CoTaskMemFree)>;
        CoFreePtr rBuf{ static_cast<uint8_t*>(CoTaskMemAlloc(pixelCount)), &CoTaskMemFree };
        CoFreePtr gBuf{ static_cast<uint8_t*>(CoTaskMemAlloc(pixelCount)), &CoTaskMemFree };
        CoFreePtr bBuf{ static_cast<uint8_t*>(CoTaskMemAlloc(pixelCount)), &CoTaskMemFree };
        if (!rBuf || !gBuf || !bBuf)return false;
        int    stride = ((width * 16 + 31) / 32) * 4;        // 7  RGB565 → R/G/B 三通道
        auto   srcBase = static_cast<uint8_t*>(dib.bits);
        for (int row = 0; row < height; ++row)
        {
            auto srcLine = reinterpret_cast<uint16_t*>(srcBase + size_t(row) * stride);
            size_t base = size_t(row) * width;
            for (int col = 0; col < width; ++col)
            {
                uint16_t v = srcLine[col];
                uint8_t r5 = (v >> 11) & 0x1F;
                uint8_t g6 = (v >> 5) & 0x3F;
                uint8_t b5 = (v) & 0x1F;
                rBuf.get()[base + col] = uint8_t((r5 * 255 + 15) / 31);
                gBuf.get()[base + col] = uint8_t((g6 * 255 + 31) / 63);
                bBuf.get()[base + col] = uint8_t((b5 * 255 + 15) / 31);
            }
        }
        *outR = rBuf.release();        // 8 输出   //标有12...8仅仅为个人认为的流程，不喜勿喷
        *outG = gBuf.release();
        *outB = bBuf.release();
        *outWidth = width;
        *outHeight = height;
        return true;
    }
    void __cdecl FreeBuffer(uint8_t* buffer) noexcept
    {
        CoTaskMemFree(buffer);
    }
} // extern "C"
