// pch.cpp — MouseMovementWin32ConvenienceLibrary (C++20)
#include "pch.h"
#include <Windows.h>
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
    // 将目标坐标限定为非负（屏幕左上为起点）
    int tx = x1 < 0 ? 0 : x1;
    int ty = y1 < 0 ? 0 : y1;
    // 尝试屏蔽用户输入（BlockInput 需要提升权限或以交互式会话运行）
    BOOL blocked = FALSE;
    if (BlockInput(TRUE))
    {
        blocked = TRUE;
    }
    // 移动光标到目标位置
    BOOL moved = SetCursorPos(tx, ty);
    // 小延迟以确保位置生效
    std::this_thread::sleep_for(std::chrono::milliseconds(50));
    // 恢复用户输入（如果之前成功屏蔽）
    if (blocked)
    {
        BlockInput(FALSE);
    }
    return moved ? 1 : 0;
}
