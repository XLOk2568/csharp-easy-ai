#include "pch.h"
#include <vector>
#include <string>
#include <mutex>
#include <stdexcept>
#include <numeric>
#include <cstring>   // memcpy
#include <cmath>
#include <algorithm>          // ★ std::max 用到
// 主机侧安全读取 4×int 的结构体，避免 cl_int4 不同编译器成员布局差异
typedef struct { int x; int y; int z; int w; } int4_host;
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
static cl_kernel                        g_deformAutoV2Ker = nullptr; // 新增：自动可变形V2核
extern bool  LoadOpenCL();
extern void  UnloadOpenCL();
using cl_float2 = struct { float x, y; };
static std::mutex          g_initMtx;
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
// 自动可变形卷积v2：每个模板像素在局部半径内(默认±1)寻找最小差作为位移匹配代价，再累积SAD
__kernel void deform_auto_v2_k(
    __global const int* bigImg,  int bigW, int bigH,
    __global const int* tplImg,  int tplW, int tplH,
    int rows, int cols, int strideX, int strideY, int maxSAD,
    int maxShift,                      // 局部位移半径，建议 1 或 2
    __global float* scores,            // [rows*cols]
    __global int4*  infos)             // [rows*cols]
{
    int gid   = get_global_id(0);
    int total = rows * cols;
    if (gid >= total) return;

    int rowIdx   = gid / cols;
    int colIdx   = gid % cols;
    int y0_base  = rowIdx * strideY;
    int x0_base  = colIdx * strideX;

    if (y0_base + tplH > bigH || x0_base + tplW > bigW) {
        scores[gid] = -1.f;
        infos[gid]  = (int4)(0,0,0,0);
        return;
    }
    int tplPix = tplW * tplH;
    float sad  = 0.f;
    // 对模板每个像素，允许在大图局部邻域内搜索最小差 (可变形对齐的近似)
    for (int u = 0; u < tplH; ++u) {
        for (int v = 0; v < tplW; ++v) {
            float tgt = (float)tplImg[u * tplW + v];

            float bestLocal = FLT_MAX;
            for (int dy = -maxShift; dy <= maxShift; ++dy) {
                for (int dx = -maxShift; dx <= maxShift; ++dx) {
                    int yy = clamp(y0_base + u + dy, 0, bigH - 1);
                    int xx = clamp(x0_base + v + dx, 0, bigW - 1);
                    float val = (float)bigImg[yy * bigW + xx];
                    float diff = fabs(val - tgt);
                    bestLocal = fmin(bestLocal, diff);
                }
            }
            sad += bestLocal;
        }
    }
    float score = 1.f - sad / (float)maxSAD;
    scores[gid] = score;
    infos[gid]  = (int4)(x0_base, y0_base, tplW, tplH);
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
    g_deformAutoV2Ker = clCreateKernel(g_program, "deform_auto_v2_k", nullptr);
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
        clGetDeviceInfo(g_devices[index],CL_DEVICE_NAME,0, nullptr,&len);
        std::vector<char> tmp(len);
        clGetDeviceInfo(g_devices[index],CL_DEVICE_NAME,len,tmp.data(),nullptr);
        int toCopy = len < static_cast<size_t>(bufSize - 1)
        ? static_cast<int>(len): (bufSize - 1);
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
    extern "C" __declspec(dllexport) int __cdecl NetWorkUsingV2(
        const int* tplList, int tplCount, int tplWidth,
        const int* bigList, int bigCount, int bigWidth,
        const int* deviceList, int deviceCount,           // 多设备 ID 数组
        const int* oldRegion,                             // 上次匹配区域 (x,y,w,h)
        int* outX, int* outY, int* outW, int* outH, float* outScore)
    {
        try
        {
            // 基础校验
            if (!tplList || !bigList || !deviceList || deviceCount <= 0 ||
                !outX || !outY || !outW || !outH || !outScore) return 0;
            if (tplWidth <= 0 || bigWidth <= 0 || tplCount <= 0 || bigCount <= 0) return 0;
            if (tplCount % tplWidth != 0 || bigCount % bigWidth != 0) return 0;
            const int tplH = tplCount / tplWidth;
            const int tplW = tplWidth;
            const int bigH = bigCount / bigWidth;
            const int bigW = bigWidth;
            if (tplW > bigW || tplH > bigH) return 0;
            // 计算搜索区域
            int searchX = 0, searchY = 0, searchW = bigW, searchH = bigH;
            if (oldRegion && !(oldRegion[0] == 0 && oldRegion[1] == 0 &&
                oldRegion[2] == 0 && oldRegion[3] == 0))
            {
                const int margin = 20;
                // 计算 searchX
                searchX = oldRegion[0] - margin;
                if (searchX < 0)
                {
                    searchX = 0;
                }
                searchY = oldRegion[1] - margin;
                if (searchY < 0)
                {
                    searchY = 0;
                }
                searchW = oldRegion[2] + margin * 2;
                if (searchW > bigW)
                {
                    searchW = bigW;
                }
                searchH = oldRegion[3] + margin * 2;
                if (searchH > bigH)
                {
                    searchH = bigH;
                }
            }
            InitOpenCL();
            if (!g_deformAutoV2Ker) return 0;
            const int tplPix = tplW * tplH;
            const int maxSAD = tplPix * 255;
            const int strideX = 1, strideY = 1;
            int maxShift = 1;
            float bestScore = -1.0e30f;
            cl_int4 bestInfo = { 0,0,0,0 };
            // 遍历设备
            for (int di = 0; di < deviceCount; ++di) {
                int devIndex = deviceList[di];
                if (devIndex < 0 || devIndex >= (int)g_devices.size()) continue;
                // 提取当前搜索区域数据
                std::vector<int> bigRegion(searchW * searchH);
                for (int r = 0; r < searchH; ++r) {
                    memcpy(&bigRegion[r * searchW],
                        &bigList[(searchY + r) * bigW + searchX],
                        sizeof(int) * searchW);
                }
                const int rows = searchH - tplH + 1;
                const int cols = searchW - tplW + 1;
                if (rows <= 0 || cols <= 0) continue;
                const int total = rows * cols;
                size_t bytesTpl = sizeof(int) * tplPix;
                size_t bytesBig = sizeof(int) * (searchW * searchH);
                size_t bytesScores = sizeof(float) * total;
                size_t bytesInfos = sizeof(cl_int4) * total;
                cl_int err;
                cl_mem bufBig = clCreateBuffer(g_context, CL_MEM_READ_ONLY | CL_MEM_COPY_HOST_PTR, bytesBig, bigRegion.data(), &err);
                if (err != CL_SUCCESS) continue;
                cl_mem bufTpl = clCreateBuffer(g_context, CL_MEM_READ_ONLY | CL_MEM_COPY_HOST_PTR, bytesTpl, (void*)tplList, &err);
                if (err != CL_SUCCESS) { clReleaseMemObject(bufBig); continue; }
                cl_mem bufScores = clCreateBuffer(g_context, CL_MEM_WRITE_ONLY, bytesScores, nullptr, &err);
                if (err != CL_SUCCESS) { clReleaseMemObject(bufBig); clReleaseMemObject(bufTpl); continue; }
                cl_mem bufInfos = clCreateBuffer(g_context, CL_MEM_WRITE_ONLY, bytesInfos, nullptr, &err);
                if (err != CL_SUCCESS) { clReleaseMemObject(bufBig); clReleaseMemObject(bufTpl); clReleaseMemObject(bufScores); continue; }
                // 设置 kernel 参数
                clSetKernelArg(g_deformAutoV2Ker, 0, sizeof(cl_mem), &bufBig);
                clSetKernelArg(g_deformAutoV2Ker, 1, sizeof(int), &searchW);
                clSetKernelArg(g_deformAutoV2Ker, 2, sizeof(int), &searchH);
                clSetKernelArg(g_deformAutoV2Ker, 3, sizeof(cl_mem), &bufTpl);
                clSetKernelArg(g_deformAutoV2Ker, 4, sizeof(int), &tplW);
                clSetKernelArg(g_deformAutoV2Ker, 5, sizeof(int), &tplH);
                clSetKernelArg(g_deformAutoV2Ker, 6, sizeof(int), &rows);
                clSetKernelArg(g_deformAutoV2Ker, 7, sizeof(int), &cols);
                clSetKernelArg(g_deformAutoV2Ker, 8, sizeof(int), &strideX);
                clSetKernelArg(g_deformAutoV2Ker, 9, sizeof(int), &strideY);
                clSetKernelArg(g_deformAutoV2Ker, 10, sizeof(int), &maxSAD);
                clSetKernelArg(g_deformAutoV2Ker, 11, sizeof(int), &maxShift);
                clSetKernelArg(g_deformAutoV2Ker, 12, sizeof(cl_mem), &bufScores);
                clSetKernelArg(g_deformAutoV2Ker, 13, sizeof(cl_mem), &bufInfos);
                size_t global = (size_t)total;
                clEnqueueNDRangeKernel(g_queues[devIndex], g_deformAutoV2Ker, 1, nullptr, &global, nullptr, 0, nullptr, nullptr);
                clFinish(g_queues[devIndex]);
                // 读取结果
                std::vector<float>    hostScores((size_t)total);
                std::vector<int4_host> hostInfos((size_t)total);
                // 注意：按主机侧结构体尺寸回读
                clEnqueueReadBuffer(g_queues[devIndex], bufScores, CL_TRUE, 0,
                    sizeof(float) * (size_t)total, hostScores.data(), 0, nullptr, nullptr);
                clEnqueueReadBuffer(g_queues[devIndex], bufInfos, CL_TRUE, 0,
                    sizeof(int4_host) * (size_t)total, hostInfos.data(), 0, nullptr, nullptr);
                // 选最佳（基础 for + if）
                float bestScore = -1.0e30f;
                int bestX = 0;
                int bestY = 0;
                for (int i = 0; i < total; ++i)
                {
                    float s = hostScores[i];
                    if (s > bestScore)
                    {
                        bestScore = s;
                        bestX = hostInfos[i].x; // 只信任 x/y
                        bestY = hostInfos[i].y;
                    }
                }
                // 无有效结果
                if (bestScore <= -1.0e29f)
                {
                    clReleaseMemObject(bufInfos);
                    clReleaseMemObject(bufScores);
                    clReleaseMemObject(bufTpl);
                    clReleaseMemObject(bufBig);
                    return 0;
                }
                // 安全夹取位置到图像内（基础 if）
                int maxX = bigW - tplW;
                int maxY = bigH - tplH;
                if (maxX < 0) maxX = 0;
                if (maxY < 0) maxY = 0;
                if (bestX < 0) bestX = 0;
                if (bestX > maxX) bestX = maxX;
                if (bestY < 0) bestY = 0;
                if (bestY > maxY) bestY = maxY;
                // 输出（宽高一律用模板尺寸，避免 4000+ 离谱值）
                *outX = bestX;
                *outY = bestY;
                *outW = tplW;
                *outH = tplH;
                // 分数夹取到 [0,1]（基础 if）
                if (bestScore < 0.0f) bestScore = 0.0f;
                if (bestScore > 1.0f) bestScore = 1.0f;
                *outScore = bestScore;
                return 1;
            }
        }                     
        catch (...)           
        {              
            return 0;      
        }
    }
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
    if (g_deformAutoV2Ker) clReleaseKernel(g_deformAutoV2Ker);    // 释放自动可变形卷积
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
