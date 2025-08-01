// pch.cpp — MouseMovementWin32ConvenienceLibrary (C++20)
#include "pch.h"
#include <Windows.h>
#include <random>
#include <thread>
#include <chrono>
extern "C" __declspec(dllexport)
int __cdecl MouseControl(int x1, int y1, int x2, int y2) noexcept
{
    // 传入 (-1, -1, -1, -1) 时返回当前鼠标位置
    // x 表示距离屏幕左边缘的像素数，y 表示距离屏幕上边缘的像素数
    if (x1 == -1 && y1 == -1 && x2 == -1 && y2 == -1)
    {
        POINT pt;
        if (GetCursorPos(&pt))
        {
            int x = pt.x < 0 ? 0 : pt.x;
            int y = pt.y < 0 ? 0 : pt.y;
            return (x << 16) | (y & 0xFFFF);
        }
        return -1;
    }
    // 模拟鼠标从 (x1,y1) 移动到 (x2,y2)，带随机像素偏移并屏蔽用户干扰
    constexpr int steps = 30;
    // 锁定鼠标活动区域到起点那一像素，避免用户干扰
    RECT lockRect = { x1, y1, x1 + 1, y1 + 1 };
    ClipCursor(&lockRect);
    ShowCursor(FALSE);
    // 定位到起点
    SetCursorPos(x1, y1);
    // 随机引擎与分布
    std::mt19937 rng(static_cast<unsigned>(GetTickCount64() & 0xFFFFFFFF));
    std::uniform_int_distribution<int> jitter(-2, 2);   // 每步像素抖动
    std::uniform_int_distribution<int> delayDist(1, 4); // 随机延迟
    for (int i = 1; i <= steps; ++i)
    {
        double t = static_cast<double>(i) / steps;
        int x = static_cast<int>(x1 + (x2 - x1) * t) + jitter(rng);
        int y = static_cast<int>(y1 + (y2 - y1) * t) + jitter(rng);
        SetCursorPos(x, y);
        // 每步间隔 delayDist 毫秒，模拟更自然的速度抖动
        std::this_thread::sleep_for(std::chrono::milliseconds(delayDist(rng)));
    }
    // 最终定位 & 恢复状态
    SetCursorPos(x2, y2);
    ClipCursor(NULL);
    ShowCursor(TRUE);
    return 1;
}

