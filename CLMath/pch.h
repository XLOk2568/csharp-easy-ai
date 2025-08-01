#pragma once

// -----------------------------------------------------------------------------
// pch.h – Precompiled header + 动态加载 OpenCL + C API 导出声明
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
// OpenCL 基本类型定义（保持原样）
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

typedef struct _cl_platform_id*     cl_platform_id;
typedef struct _cl_device_id*       cl_device_id;
typedef struct _cl_context*         cl_context;
typedef struct _cl_command_queue*   cl_command_queue;
typedef struct _cl_program*         cl_program;
typedef struct _cl_kernel*          cl_kernel;
typedef struct _cl_mem*             cl_mem;

#define CL_SUCCESS                 0
#define CL_TRUE                    1
//支持 int4 对齐
#ifdef _MSC_VER
__declspec(align(16))
#endif
typedef struct { cl_int s[4]; } cl_int4;
#ifndef _MSC_VER
__attribute__((aligned(16)))
#endif
#define CL_DEVICE_TYPE_GPU         (1 << 2)

#define CL_MEM_READ_ONLY           (1 << 0)
#define CL_MEM_WRITE_ONLY          (1 << 1)
#define CL_MEM_COPY_HOST_PTR       (1 << 3)

// -----------------------------------------------------------------------------
// OpenCL 函数指针 typedef 与 extern 声明（保持原样）
// -----------------------------------------------------------------------------
typedef cl_int  (*PFN_clGetPlatformIDs)        (cl_uint, cl_platform_id*, cl_uint*);
typedef cl_int  (*PFN_clGetDeviceIDs)          (cl_platform_id, cl_device_type,
                                                cl_uint, cl_device_id*, cl_uint*);
typedef cl_context (*PFN_clCreateContext)      (const cl_context_properties*,
                                                cl_uint,
                                                const cl_device_id*,
                                                void (*pfn_notify)(const char*, const void*, size_t, void*),
                                                void*,
                                                cl_int*);
typedef cl_command_queue (*PFN_clCreateCommandQueue)
                                                (cl_context,
                                                 cl_device_id,
                                                 cl_command_queue_properties,
                                                 cl_int*);
typedef cl_program (*PFN_clCreateProgramWithSource)
                                                (cl_context,
                                                 cl_uint,
                                                 const char**,
                                                 const size_t*,
                                                 cl_int*);
typedef cl_int  (*PFN_clBuildProgram)          (cl_program,
                                                cl_uint,
                                                const cl_device_id*,
                                                const char*,
                                                void (*pfn_notify)(cl_program, void*),
                                                void*);
typedef cl_kernel (*PFN_clCreateKernel)        (cl_program,
                                                const char*,
                                                cl_int*);
typedef cl_mem   (*PFN_clCreateBuffer)         (cl_context,
                                                cl_mem_flags,
                                                size_t,
                                                void*,
                                                cl_int*);
typedef cl_int  (*PFN_clSetKernelArg)          (cl_kernel,
                                                cl_uint,
                                                size_t,
                                                const void*);
typedef cl_int  (*PFN_clEnqueueNDRangeKernel)  (cl_command_queue,
                                                cl_kernel,
                                                cl_uint,
                                                const size_t*,
                                                const size_t*,
                                                const size_t*,
                                                cl_uint,
                                                const void*,
                                                void*);
typedef cl_int  (*PFN_clFinish)                (cl_command_queue);
typedef cl_int  (*PFN_clEnqueueReadBuffer)     (cl_command_queue,
                                                cl_mem,
                                                cl_bool,
                                                size_t,
                                                size_t,
                                                void*,
                                                cl_uint,
                                                const void*,
                                                void*);
typedef cl_int  (*PFN_clReleaseMemObject)      (cl_mem);
typedef cl_int  (*PFN_clReleaseCommandQueue)   (cl_command_queue);
typedef cl_int  (*PFN_clReleaseKernel)         (cl_kernel);
typedef cl_int  (*PFN_clReleaseProgram)        (cl_program);
typedef cl_int  (*PFN_clReleaseContext)        (cl_context);
typedef cl_int  (*PFN_clGetDeviceInfo)         (cl_device_id,
                                                cl_device_info,
                                                size_t,
                                                void*,
                                                size_t*);

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

// 动态加载/卸载 OpenCL
bool LoadOpenCL();
void UnloadOpenCL();

// -----------------------------------------------------------------------------
// 下面全部改为 C API 导出声明（删除原 namespace CLMath）
// -----------------------------------------------------------------------------

// 返回可用 GPU 设备数量
__declspec(dllexport) int __cdecl GetDeviceNamesCount();

// 获取单个设备名称，写入 buf 并返回实际长度
__declspec(dllexport) int __cdecl GetDeviceNames(int index,
                                                 char* buf,
                                                 int bufSize);

// 四则运算接口
__declspec(dllexport) double __cdecl CL_Add(const double* arr,
                                            int count,
                                            int deviceIndex);

__declspec(dllexport) double __cdecl CL_Sub(const double* arr,
                                            int count,
                                            int deviceIndex);

__declspec(dllexport) double __cdecl CL_Mul(const double* arr,
                                            int count,
                                            int deviceIndex);

__declspec(dllexport) double __cdecl CL_Div(const double* arr,
                                            int count,
                                            int deviceIndex);

// 释放所有 OpenCL 资源
__declspec(dllexport) void __cdecl DisposeOpenCL();
#ifdef __cplusplus
} // extern "C"
#endif
