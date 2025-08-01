#pragma once

// -----------------------------------------------------------------------------
// pch.h – Precompiled header + 动态加载 OpenCL + CLMath API
// -----------------------------------------------------------------------------

// C Standard
#include <stddef.h>            // size_t

// C++ Standard
#include <initializer_list>
#include <vector>
#include <string>

#ifdef _WIN32
#include <windows.h>
#else
#include <dlfcn.h>
#endif

#ifdef __cplusplus
extern "C" {
#endif

    // -----------------------------------------------------------------------------
    // 基本类型定义
    // -----------------------------------------------------------------------------
    typedef int                 cl_int;
    typedef unsigned int        cl_uint;
    typedef unsigned long long  cl_bitfield;
    typedef cl_bitfield         cl_device_type;
    typedef cl_bitfield         cl_mem_flags;
    typedef cl_bitfield         cl_command_queue_properties;
    typedef size_t              cl_context_properties;
    typedef cl_uint             cl_bool;
    typedef cl_uint             cl_device_info;

    typedef struct _cl_platform_id* cl_platform_id;
    typedef struct _cl_device_id* cl_device_id;
    typedef struct _cl_context* cl_context;
    typedef struct _cl_command_queue* cl_command_queue;
    typedef struct _cl_program* cl_program;
    typedef struct _cl_kernel* cl_kernel;
    typedef struct _cl_mem* cl_mem;

#define CL_SUCCESS                 0
#define CL_TRUE                    1

#define CL_DEVICE_TYPE_GPU         (1 << 2)

#define CL_MEM_READ_ONLY           (1 << 0)
#define CL_MEM_WRITE_ONLY          (1 << 1)
#define CL_MEM_COPY_HOST_PTR       (1 << 3)

    // -----------------------------------------------------------------------------
    // OpenCL 函数指针 typedef
    // -----------------------------------------------------------------------------
    typedef cl_int(*PFN_clGetPlatformIDs)         (cl_uint, cl_platform_id*, cl_uint*);
    typedef cl_int(*PFN_clGetDeviceIDs)           (cl_platform_id, cl_device_type,
        cl_uint, cl_device_id*, cl_uint*);
    typedef cl_context(*PFN_clCreateContext)      (const cl_context_properties*,
        cl_uint,
        const cl_device_id*,
        void (*pfn_notify)(const char*, const void*, size_t, void*),
        void*,
        cl_int*);
    typedef cl_command_queue(*PFN_clCreateCommandQueue)
        (cl_context,
            cl_device_id,
            cl_command_queue_properties,
            cl_int*);
    typedef cl_program(*PFN_clCreateProgramWithSource)
        (cl_context,
            cl_uint,
            const char**,
            const size_t*,
            cl_int*);
    typedef cl_int(*PFN_clBuildProgram)           (cl_program,
        cl_uint,
        const cl_device_id*,
        const char*,
        void (*pfn_notify)(cl_program, void*),
        void*);
    typedef cl_kernel(*PFN_clCreateKernel)        (cl_program,
        const char*,
        cl_int*);
    typedef cl_mem(*PFN_clCreateBuffer)           (cl_context,
        cl_mem_flags,
        size_t,
        void*,
        cl_int*);
    typedef cl_int(*PFN_clSetKernelArg)           (cl_kernel,
        cl_uint,
        size_t,
        const void*);
    typedef cl_int(*PFN_clEnqueueNDRangeKernel)   (cl_command_queue,
        cl_kernel,
        cl_uint,
        const size_t*,
        const size_t*,
        const size_t*,
        cl_uint,
        const void*,
        void*);
    typedef cl_int(*PFN_clFinish)                 (cl_command_queue);
    typedef cl_int(*PFN_clEnqueueReadBuffer)      (cl_command_queue,
        cl_mem,
        cl_bool,
        size_t,
        size_t,
        void*,
        cl_uint,
        const void*,
        void*);
    typedef cl_int(*PFN_clReleaseMemObject)       (cl_mem);
    typedef cl_int(*PFN_clReleaseCommandQueue)    (cl_command_queue);
    typedef cl_int(*PFN_clReleaseKernel)          (cl_kernel);
    typedef cl_int(*PFN_clReleaseProgram)         (cl_program);
    typedef cl_int(*PFN_clReleaseContext)         (cl_context);
    typedef cl_int(*PFN_clGetDeviceInfo)          (cl_device_id,
        cl_device_info,
        size_t,
        void*,
        size_t*);

    // -----------------------------------------------------------------------------
    // extern 声明所有函数指针变量
    // -----------------------------------------------------------------------------
    extern PFN_clGetPlatformIDs           clGetPlatformIDs;
    extern PFN_clGetDeviceIDs             clGetDeviceIDs;
    extern PFN_clCreateContext            clCreateContext;
    extern PFN_clCreateCommandQueue       clCreateCommandQueue;
    extern PFN_clCreateProgramWithSource  clCreateProgramWithSource;
    extern PFN_clBuildProgram             clBuildProgram;
    extern PFN_clCreateKernel             clCreateKernel;
    extern PFN_clCreateBuffer             clCreateBuffer;
    extern PFN_clSetKernelArg             clSetKernelArg;
    extern PFN_clEnqueueNDRangeKernel     clEnqueueNDRangeKernel;
    extern PFN_clFinish                   clFinish;
    extern PFN_clEnqueueReadBuffer        clEnqueueReadBuffer;
    extern PFN_clReleaseMemObject         clReleaseMemObject;
    extern PFN_clReleaseCommandQueue      clReleaseCommandQueue;
    extern PFN_clReleaseKernel            clReleaseKernel;
    extern PFN_clReleaseProgram           clReleaseProgram;
    extern PFN_clReleaseContext           clReleaseContext;
    extern PFN_clGetDeviceInfo            clGetDeviceInfo;

#ifdef __cplusplus
} // extern "C"
#endif

// -----------------------------------------------------------------------------
// 动态加载/卸载 OpenCL 运行时
// -----------------------------------------------------------------------------
#ifdef __cplusplus
static
#ifdef _WIN32
HMODULE
#else
void*
#endif
gOpenCLLib = nullptr;

inline bool LoadOpenCL()
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
inline void UnloadOpenCL()
{
    if (!gOpenCLLib) return;
#ifdef _WIN32
    FreeLibrary(gOpenCLLib);
#else
    dlclose(gOpenCLLib);
#endif
    gOpenCLLib = nullptr;
#define CLEAR(fn) fn = nullptr
    CLEAR(clGetPlatformIDs);
    CLEAR(clGetDeviceIDs);
    CLEAR(clCreateContext);
    CLEAR(clCreateCommandQueue);
    CLEAR(clCreateProgramWithSource);
    CLEAR(clBuildProgram);
    CLEAR(clCreateKernel);
    CLEAR(clCreateBuffer);
    CLEAR(clSetKernelArg);
    CLEAR(clEnqueueNDRangeKernel);
    CLEAR(clFinish);
    CLEAR(clEnqueueReadBuffer);
    CLEAR(clReleaseMemObject);
    CLEAR(clReleaseCommandQueue);
    CLEAR(clReleaseKernel);
    CLEAR(clReleaseProgram);
    CLEAR(clReleaseContext);
    CLEAR(clGetDeviceInfo);
#undef CLEAR
}
#endif // __cplusplus
// -----------------------------------------------------------------------------
// CLMath API – C++ interface for arithmetic operations on GPU
// -----------------------------------------------------------------------------
namespace CLMath
{
    // Summation (add)
    double And(std::initializer_list<double> args);
    double And(std::initializer_list<double> args, int deviceIndex);
    // Subtraction
    double Jian(std::initializer_list<double> args);
    double Jian(std::initializer_list<double> args, int deviceIndex);
    // Multiplication
    double Cheng(std::initializer_list<double> args);
    double Cheng(std::initializer_list<double> args, int deviceIndex);
    // Division
    double Chu(std::initializer_list<double> args);
    double Chu(std::initializer_list<double> args, int deviceIndex);
    // Query all available GPU device names
    std::vector<std::string> GetDeviceNames();
    // Release all OpenCL resources
    void Dispose();
}
