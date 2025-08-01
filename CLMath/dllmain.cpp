// dllmain.cpp — CaptureRGB3 DLL 入口 & OpenCL Loader 定义

#include "pch.h"
#include <Windows.h>

//
// 1) 全局句柄 & 指针定义（与 pch.h 中 extern 对应）
//
#ifdef _WIN32
HMODULE gOpenCLLib = nullptr;
#else
void* gOpenCLLib = nullptr;
#endif

PFN_clGetPlatformIDs           clGetPlatformIDs = nullptr;
PFN_clGetDeviceIDs             clGetDeviceIDs = nullptr;
PFN_clCreateContext            clCreateContext = nullptr;
PFN_clCreateCommandQueue       clCreateCommandQueue = nullptr;
PFN_clCreateProgramWithSource  clCreateProgramWithSource = nullptr;
PFN_clBuildProgram             clBuildProgram = nullptr;
PFN_clCreateKernel             clCreateKernel = nullptr;
PFN_clCreateBuffer             clCreateBuffer = nullptr;
PFN_clSetKernelArg             clSetKernelArg = nullptr;
PFN_clEnqueueNDRangeKernel     clEnqueueNDRangeKernel = nullptr;
PFN_clFinish                   clFinish = nullptr;
PFN_clEnqueueReadBuffer        clEnqueueReadBuffer = nullptr;
PFN_clReleaseMemObject         clReleaseMemObject = nullptr;
PFN_clReleaseCommandQueue      clReleaseCommandQueue = nullptr;
PFN_clReleaseKernel            clReleaseKernel = nullptr;
PFN_clReleaseProgram           clReleaseProgram = nullptr;
PFN_clReleaseContext           clReleaseContext = nullptr;
PFN_clGetDeviceInfo            clGetDeviceInfo = nullptr;

//
// 2) LoadOpenCL / UnloadOpenCL 实现
//
bool LoadOpenCL()
{
#ifdef _WIN32
    gOpenCLLib = LoadLibraryA("OpenCL.dll");
    if (!gOpenCLLib) return false;
#define LOAD_FN(fn) (fn = (PFN_##fn)GetProcAddress(gOpenCLLib, #fn)) != nullptr
#else
    gOpenCLLib = dlopen("libOpenCL.so", RTLD_LAZY | RTLD_LOCAL);
    if (!gOpenCLLib) return false;
#define LOAD_FN(fn) (fn = (PFN_##fn)dlsym(gOpenCLLib, #fn)) != nullptr
#endif

    bool ok =
        LOAD_FN(clGetPlatformIDs) &&
        LOAD_FN(clGetDeviceIDs) &&
        LOAD_FN(clCreateContext) &&
        LOAD_FN(clCreateCommandQueue) &&
        LOAD_FN(clCreateProgramWithSource) &&
        LOAD_FN(clBuildProgram) &&
        LOAD_FN(clCreateKernel) &&
        LOAD_FN(clCreateBuffer) &&
        LOAD_FN(clSetKernelArg) &&
        LOAD_FN(clEnqueueNDRangeKernel) &&
        LOAD_FN(clFinish) &&
        LOAD_FN(clEnqueueReadBuffer) &&
        LOAD_FN(clReleaseMemObject) &&
        LOAD_FN(clReleaseCommandQueue) &&
        LOAD_FN(clReleaseKernel) &&
        LOAD_FN(clReleaseProgram) &&
        LOAD_FN(clReleaseContext) &&
        LOAD_FN(clGetDeviceInfo);

#undef LOAD_FN
    return ok;
}

void UnloadOpenCL()
{
    if (!gOpenCLLib) return;

#ifdef _WIN32
    FreeLibrary(gOpenCLLib);
#else
    dlclose(gOpenCLLib);
#endif
    gOpenCLLib = nullptr;

#define CLR(fn) fn = nullptr
    CLR(clGetPlatformIDs);
    CLR(clGetDeviceIDs);
    CLR(clCreateContext);
    CLR(clCreateCommandQueue);
    CLR(clCreateProgramWithSource);
    CLR(clBuildProgram);
    CLR(clCreateKernel);
    CLR(clCreateBuffer);
    CLR(clSetKernelArg);
    CLR(clEnqueueNDRangeKernel);
    CLR(clFinish);
    CLR(clEnqueueReadBuffer);
    CLR(clReleaseMemObject);
    CLR(clReleaseCommandQueue);
    CLR(clReleaseKernel);
    CLR(clReleaseProgram);
    CLR(clReleaseContext);
    CLR(clGetDeviceInfo);
#undef CLR
}

//
// 3) DLLMain：只做必要的线程通知关闭
//
BOOL APIENTRY DllMain(
    HMODULE hModule,
    DWORD   ul_reason_for_call,
    LPVOID  /*lpReserved*/
) noexcept
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_PROCESS_DETACH:
        // 可选择调用 UnloadOpenCL() 清理
        break;
    }
    return TRUE;
}

