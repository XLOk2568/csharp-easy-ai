#include "pch.h"
#include <vector>
#include <string>
#include <mutex>
#include <stdexcept>
constexpr auto CL_DEVICE_NAME = 0x102B;
namespace CLMath
{
    // 全局 OpenCL 状态（内部链接）
    static bool                             g_inited = false;
    static cl_platform_id                   g_platform = nullptr;
    static std::vector<cl_device_id>        g_devices;
    static cl_context                       g_context = nullptr;
    static std::vector<cl_command_queue>    g_queues;
    static cl_program                       g_program = nullptr;
    static cl_kernel                        g_addKer = nullptr;
    static cl_kernel                        g_subKer = nullptr;
    static cl_kernel                        g_mulKer = nullptr;
    static cl_kernel                        g_divKer = nullptr;
    static std::mutex                       g_initMutex;

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
    )CLC";

    // 单次初始化：枚举 GPU、建 Context、编译 kernel
    static void InitOpenCL()
    {
        std::lock_guard<std::mutex> lock(g_initMutex);
        if (g_inited) return;

        // 1. 平台
        cl_uint platCnt = 0;
        clGetPlatformIDs(1, &g_platform, &platCnt);

        // 2. 枚举 GPU 设备
        cl_uint devCnt = 0;
        clGetDeviceIDs(g_platform, CL_DEVICE_TYPE_GPU, 0, nullptr, &devCnt);
        g_devices.resize(devCnt);
        clGetDeviceIDs(g_platform, CL_DEVICE_TYPE_GPU,
            devCnt, g_devices.data(), nullptr);

        // 3. 创建 Context
        g_context = clCreateContext(nullptr,
            devCnt,
            g_devices.data(),
            nullptr,
            nullptr,
            nullptr);

        // 4. 命令队列
        g_queues.reserve(devCnt);
        for (cl_uint i = 0; i < devCnt; ++i)
        {
            g_queues.push_back(
                clCreateCommandQueue(
                    g_context,
                    g_devices[i],
                    0,
                    nullptr));
        }

        // 5. 编译 Program + Kernel
		static const char* srcs[] = { kCLSrc };
        g_program = clCreateProgramWithSource(
            g_context,
            1,
            srcs,
            nullptr,
            nullptr);

        clBuildProgram(
            g_program,
            (cl_uint)g_devices.size(),
            g_devices.data(),
            nullptr,
            nullptr,
            nullptr);

        g_addKer = clCreateKernel(g_program, "add_k", nullptr);
        g_subKer = clCreateKernel(g_program, "sub_k", nullptr);
        g_mulKer = clCreateKernel(g_program, "mul_k", nullptr);
        g_divKer = clCreateKernel(g_program, "div_k", nullptr);

        g_inited = true;
    }

    // 在指定 GPU 上运行 kernel，返回结果
    static double RunKernel(cl_kernel kernel,
        const std::vector<double>& vec,
        int deviceIndex)
    {
        InitOpenCL();
        if (deviceIndex < 0 || deviceIndex >= (int)g_devices.size())
            throw std::out_of_range("deviceIndex");

        int   n = (int)vec.size();
        size_t bs = sizeof(double) * n;

        // 分配读/写缓冲
        cl_mem bufA = clCreateBuffer(
            g_context,
            CL_MEM_READ_ONLY | CL_MEM_COPY_HOST_PTR,
            bs,
            (void*)vec.data(),
            nullptr);

        cl_mem bufR = clCreateBuffer(
            g_context,
            CL_MEM_WRITE_ONLY,
            sizeof(double),
            nullptr,
            nullptr);

        // 设置参数
        clSetKernelArg(kernel, 0, sizeof(int), &n);
        clSetKernelArg(kernel, 1, sizeof(cl_mem), &bufA);
        clSetKernelArg(kernel, 2, sizeof(cl_mem), &bufR);

        // 调度执行
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

        // 读回
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

        // 释放临时缓冲
        clReleaseMemObject(bufA);
        clReleaseMemObject(bufR);

        return result;
    }

    // ===== 对外接口实现 =====

    double And(std::initializer_list<double> args)
    {
        return And(args, 0);
    }

    double And(std::initializer_list<double> args, int deviceIndex)
    {
        return RunKernel(g_addKer,
            std::vector<double>(args),
            deviceIndex);
    }

    double Jian(std::initializer_list<double> args)
    {
        return Jian(args, 0);
    }

    double Jian(std::initializer_list<double> args, int deviceIndex)
    {
        return RunKernel(g_subKer,
            std::vector<double>(args),
            deviceIndex);
    }

    double Cheng(std::initializer_list<double> args)
    {
        return Cheng(args, 0);
    }

    double Cheng(std::initializer_list<double> args, int deviceIndex)
    {
        return RunKernel(g_mulKer,
            std::vector<double>(args),
            deviceIndex);
    }

    double Chu(std::initializer_list<double> args)
    {
        return Chu(args, 0);
    }

    double Chu(std::initializer_list<double> args, int deviceIndex)
    {
        return RunKernel(g_divKer,
            std::vector<double>(args),
            deviceIndex);
    }

    std::vector<std::string> GetDeviceNames()
    {
        InitOpenCL();
        std::vector<std::string> names;
        for (auto& dev : g_devices)
        {
            size_t len = 0;
            clGetDeviceInfo(dev,
                CL_DEVICE_NAME,
                0, nullptr,
                &len);

            std::string buf(len, '\0');
            clGetDeviceInfo(dev,
                CL_DEVICE_NAME,
                len,
                &buf[0],
                nullptr);

            names.push_back(buf);
        }
        return names;
    }

    void Dispose()
    {
        if (!g_inited) return;

        for (auto& q : g_queues)
            clReleaseCommandQueue(q);

        clReleaseKernel(g_addKer);
        clReleaseKernel(g_subKer);
        clReleaseKernel(g_mulKer);
        clReleaseKernel(g_divKer);

        clReleaseProgram(g_program);
        clReleaseContext(g_context);

        g_inited = false;
    }
}
