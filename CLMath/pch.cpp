#include "pch.h"
#include <vector>
#include <string>
#include <mutex>
#include <stdexcept>
#include <numeric>
#include <cstring>   // memcpy
#include <cmath>
constexpr auto CL_DEVICE_NAME = 0x102B;

// 内部全局状态
static bool                             g_inited  = false;
static cl_platform_id                   g_platform= nullptr;
static std::vector<cl_device_id>        g_devices;
static cl_context                       g_context = nullptr;
static std::vector<cl_command_queue>    g_queues;
static cl_program                       g_program = nullptr;
static cl_kernel                        g_addKer  = nullptr;
static cl_kernel                        g_subKer  = nullptr;
static cl_kernel                        g_mulKer  = nullptr;
static cl_kernel                        g_divKer  = nullptr;
static std::mutex                       g_initMutex;
static cl_kernel                        g_slideKer=nullptr;
// OpenCL 内核源码
static constexpr const char* kCLSrc = R"CLC(
__kernel void add_k(int n, __global const double* a, __global double* r){
    double s = 0.0;
    for(int i = 0; i < n; ++i) s += a[i];
    r[0] = s;
}
__kernel void sub_k(int n, __global const double* a, __global double* r){
    double v = a[0];
    for(int i = 1; i < n; ++i) v -= a[i];
    r[0] = v;
}
__kernel void mul_k(int n, __global const double* a, __global double* r){
    double p = 1.0;
    for(int i = 0; i < n; ++i) p *= a[i];
    r[0] = p;
}
__kernel void div_k(int n, __global const double* a, __global double* r){
    double v = a[0];
    for(int i = 1; i < n; ++i) v /= a[i];
    r[0] = v;
}
__kernel void slide_k(
    __global const int* bigImg,  int bigW, int bigH,
    __global const int* tplImg,  int tplW, int tplH,
    int rows, int cols, int strideX, int strideY, int maxSAD,
    __global float* scores,
    __global int4*  infos)
{
    int gid = get_global_id(0);
    int total = rows * cols;
    if (gid >= total) return;
    int rowIdx = gid / cols;
    int colIdx = gid - rowIdx * cols;
    int y0 = rowIdx * strideY;
    int x0 = colIdx * strideX;
    /* 超界直接标记无效 */
    if (y0 + tplH > bigH || x0 + tplW > bigW)
    {
        scores[gid] = -1.f;
        infos [gid] = (int4)(0,0,0,0);
        return;
    }
    int sad = 0;
    for (int u = 0; u < tplH; ++u)
    {
        int baseA = (y0 + u) * bigW + x0;
        int baseB =  u       * tplW;
        for (int v = 0; v < tplW; ++v)
            sad += abs(bigImg[baseA + v] - tplImg[baseB + v]);
    }
    scores[gid] = 1.f - (float)sad / (float)maxSAD;
    infos [gid] = (int4)(x0, y0, tplW, tplH);
}
)CLC";

// 确保 OpenCL 库已动态加载
static void EnsureOpenCLLoaded()
{
    static bool loaded = false;
    if (!loaded)
    {
        if (!::LoadOpenCL())
            throw std::runtime_error("LoadOpenCL() failed");
        loaded = true;
    }
}

// 单次初始化
static void InitOpenCL()
{
    std::lock_guard<std::mutex> lock(g_initMutex);
    if (g_inited) return;
    EnsureOpenCLLoaded();
    cl_uint platCnt = 0;
    clGetPlatformIDs(1, &g_platform, &platCnt);
    cl_uint devCnt = 0;
    clGetDeviceIDs(g_platform, CL_DEVICE_TYPE_GPU, 0, nullptr, &devCnt);
    g_devices.resize(devCnt);
    clGetDeviceIDs(g_platform, CL_DEVICE_TYPE_GPU,devCnt, g_devices.data(), nullptr);
    g_context = clCreateContext(nullptr, devCnt, g_devices.data(),nullptr, nullptr, nullptr);
    for (cl_uint i = 0; i < devCnt; ++i)
    {
        g_queues.push_back(
            clCreateCommandQueue(g_context,
                                 g_devices[i],
                                 0,
                                 nullptr));
    }
    const char* srcs[] = { kCLSrc };
    g_program = clCreateProgramWithSource(
        g_context, 1, srcs, nullptr, nullptr);

    clBuildProgram(g_program,
                   (cl_uint)g_devices.size(),
                   g_devices.data(),
                   nullptr, nullptr, nullptr);
    g_addKer = clCreateKernel(g_program, "add_k", nullptr);
    g_subKer = clCreateKernel(g_program, "sub_k", nullptr);
    g_mulKer = clCreateKernel(g_program, "mul_k", nullptr);
    g_divKer = clCreateKernel(g_program, "div_k", nullptr);
    g_slideKer = clCreateKernel(g_program, "slide_k", nullptr);
    g_inited = true;
}
// 调用任意 kernel
static double RunKernel(cl_kernel kernel,
                        const double* arr,
                        int count,
                        int deviceIndex)
{
    InitOpenCL();
    if (deviceIndex < 0 || deviceIndex >= (int)g_devices.size())
        throw std::out_of_range("deviceIndex");

    size_t bs = sizeof(double) * count;
    cl_mem bufA = clCreateBuffer(
        g_context,
        CL_MEM_READ_ONLY | CL_MEM_COPY_HOST_PTR,
        bs,
        (void*)arr,
        nullptr);

    cl_mem bufR = clCreateBuffer(
        g_context,
        CL_MEM_WRITE_ONLY,
        sizeof(double),
        nullptr,
        nullptr);

    clSetKernelArg(kernel, 0, sizeof(int), &count);
    clSetKernelArg(kernel, 1, sizeof(cl_mem), &bufA);
    clSetKernelArg(kernel, 2, sizeof(cl_mem), &bufR);

    size_t global = 1;
    clEnqueueNDRangeKernel(
        g_queues[deviceIndex],
        kernel,
        1,
        nullptr,
        &global,
        nullptr,
        0,
        nullptr,
        nullptr);

    clFinish(g_queues[deviceIndex]);

    double result = 0.0;
    clEnqueueReadBuffer(
        g_queues[deviceIndex],
        bufR,
        CL_TRUE,
        0,
        sizeof(double),
        &result,
        0,
        nullptr,
        nullptr);

    clReleaseMemObject(bufA);
    clReleaseMemObject(bufR);

    return result;
}

extern "C"
{
    int __cdecl SlideOnce(const int* bigImg, int bigH, int bigW,const int* tplImg, int tplH, int tplW,int times,float* scoreBuf,int* infoBuf)
    {
        return RunSlideKernel(bigImg, bigH, bigW, tplImg, tplH, tplW, times, scoreBuf, infoBuf);
    }
// 返回设备数量
    int __cdecl GetDeviceNamesCount()
    {
    InitOpenCL();
    return (int)g_devices.size();
    }

// 获取设备名称
    int __cdecl GetDeviceNames(int index, char* buf, int bufSize)
    {
    InitOpenCL();
    if (index < 0 || index >= (int)g_devices.size())
        return 0;

    size_t len = 0;
    clGetDeviceInfo(g_devices[index],
                    CL_DEVICE_NAME,
                    0, nullptr,
                    &len);

    std::vector<char> tmp(len);
    clGetDeviceInfo(g_devices[index],
                    CL_DEVICE_NAME,
                    len,
                    tmp.data(),
                    nullptr);
    int toCopy = len < static_cast<size_t>(bufSize - 1)
        ? static_cast<int>(len)
        : (bufSize - 1);
    memcpy(buf, tmp.data(), toCopy);
    buf[toCopy] = '\0';
    return toCopy;
    }
// 四则运算
    double __cdecl CL_Add(const double* arr, int count, int deviceIndex)
    {
    return RunKernel(g_addKer, arr, count, deviceIndex);
    DisposeOpenCL();
    }
    double __cdecl CL_Sub(const double* arr, int count, int deviceIndex)
    {
    return RunKernel(g_subKer, arr, count, deviceIndex); 
    DisposeOpenCL();

    }

    double __cdecl CL_Mul(const double* arr, int count, int deviceIndex)
    {
    return RunKernel(g_mulKer, arr, count, deviceIndex);
    DisposeOpenCL();
    }
    double __cdecl CL_Div(const double* arr, int count, int deviceIndex)
    {
    return RunKernel(g_divKer, arr, count, deviceIndex);    
    DisposeOpenCL();
    }
//网络 
    static int RunSlideKernel(const int* bigImg, int bigH, int bigW,const int* tplImg, int tplH, int tplW,int times,float* scoreBuf,int* infoBuf)
    {
        InitOpenCL();
    /* 0) 行列 / 步幅计算 */
        int rows = (int)ceil(sqrt((double)times));
     int cols = (int)ceil((double)times / rows);
      int strideY = (rows <= 1) ? (bigH - tplH) : (bigH - tplH) / (rows - 1);
      int strideX = (cols <= 1) ? (bigW - tplW) : (bigW - tplW) / (cols - 1);
       int tplPix = tplH * tplW;
       int maxSAD = 255 * tplPix;
      int total = rows * cols;
       /* 1) 设备缓冲区 */
       size_t bigSz = sizeof(int) * bigH * bigW;
       size_t tplSz = sizeof(int) * tplH * tplW;
      size_t scoSz = sizeof(float) * total;
      size_t infSz = sizeof(cl_int4) * total;
      cl_mem dBig = clCreateBuffer(g_context, CL_MEM_READ_ONLY | CL_MEM_COPY_HOST_PTR, bigSz, (void*)bigImg, nullptr);
      cl_mem dTpl = clCreateBuffer(g_context, CL_MEM_READ_ONLY | CL_MEM_COPY_HOST_PTR, tplSz, (void*)tplImg, nullptr);
      cl_mem dSco = clCreateBuffer(g_context, CL_MEM_WRITE_ONLY, scoSz, nullptr, nullptr);
      cl_mem dInf = clCreateBuffer(g_context, CL_MEM_WRITE_ONLY, infSz, nullptr, nullptr);
    /* 2) 设参 */
       int idx = 0;
      clSetKernelArg(g_slideKer, idx++, sizeof(cl_mem), &dBig);
       clSetKernelArg(g_slideKer, idx++, sizeof(int), &bigW);
        clSetKernelArg(g_slideKer, idx++, sizeof(int), &bigH);
        clSetKernelArg(g_slideKer, idx++, sizeof(cl_mem), &dTpl);
        clSetKernelArg(g_slideKer, idx++, sizeof(int), &tplW);
        clSetKernelArg(g_slideKer, idx++, sizeof(int), &tplH);
      clSetKernelArg(g_slideKer, idx++, sizeof(int), &rows);
       clSetKernelArg(g_slideKer, idx++, sizeof(int), &cols);
        clSetKernelArg(g_slideKer, idx++, sizeof(int), &strideX);
       clSetKernelArg(g_slideKer, idx++, sizeof(int), &strideY);
      clSetKernelArg(g_slideKer, idx++, sizeof(int), &maxSAD);
       clSetKernelArg(g_slideKer, idx++, sizeof(cl_mem), &dSco);
       clSetKernelArg(g_slideKer, idx++, sizeof(cl_mem), &dInf);
    /* 3) 启动核并同步 */
    size_t global = total;
        clEnqueueNDRangeKernel(g_queues[0], g_slideKer, 1, nullptr, &global, nullptr, 0, nullptr, nullptr);
         clFinish(g_queues[0]);
    /* 4) 取回结果 */
        std::vector<float>   tmpSco(total);
        std::vector<cl_int4> tmpInf(total);
        clEnqueueReadBuffer(g_queues[0], dSco, CL_TRUE, 0, scoSz, tmpSco.data(), 0, nullptr, nullptr);
        clEnqueueReadBuffer(g_queues[0], dInf, CL_TRUE, 0, infSz, tmpInf.data(), 0, nullptr, nullptr);

        clReleaseMemObject(dBig);
        clReleaseMemObject(dTpl);
        clReleaseMemObject(dSco);
        clReleaseMemObject(dInf);

    /* 5) 过滤无效窗 */
         int valid = 0;
        for (int i = 0; i < total; ++i)
        {
            if (tmpSco[i] < 0) continue;
            scoreBuf[valid] = tmpSco[i];
            infoBuf[valid * 4 + 0] = tmpInf[i].s[0];
            infoBuf[valid * 4 + 1] = tmpInf[i].s[1];
            infoBuf[valid * 4 + 2] = tmpInf[i].s[2];
            infoBuf[valid * 4 + 3] = tmpInf[i].s[3];
            ++valid;
        }
    return valid;
    }
// 释放所有 OpenCL 资源
void __cdecl DisposeOpenCL()
{
    if (!g_inited) return;
    // 释放所有命令队列
    for (auto& q : g_queues)
        clReleaseCommandQueue(q);
    g_queues.clear();
    // 释放 kernel
    clReleaseKernel(g_addKer);
    clReleaseKernel(g_subKer);
    clReleaseKernel(g_mulKer);
    clReleaseKernel(g_divKer);
    g_addKer = g_subKer = g_mulKer = g_divKer = nullptr;
    // 释放 program
    if (g_program) clReleaseProgram(g_program);
    g_program = nullptr;
    // 释放 context
    if (g_context) clReleaseContext(g_context);
    g_context = nullptr;
    // 清空设备列表
    g_devices.clear();
    // 卸载 OpenCL 库
    UnloadOpenCL();
    g_inited = false;
}
//RGB  feature  work

}  // extern "C"
