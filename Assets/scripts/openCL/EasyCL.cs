﻿using Cloo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

public class EasyCL
{
    ComputeContext context;
    ComputeCommandQueue queue;
    ComputeProgram program;

    AcceleratorDevice _device;
    public AcceleratorDevice Accelerator
    {
        get
        {
            return _device;
        }
        set
        {
            if (value != _device)
            {
                _device = value;
                CreateContext();
                if (kernel != null)
                {
                    LoadKernel(kernel);
                }
            }
        }
    }

    public static string PerformanceKernel
    {
        get
        {
            return @"#if defined(cl_khr_fp64)  // Khronos extension available?
                    #pragma OPENCL EXTENSION cl_khr_fp64 : enable
                    #elif defined(cl_amd_fp64)  // AMD extension available?
                    #pragma OPENCL EXTENSION cl_amd_fp64 : enable
                    #endif

                    #undef MAD_4
                    #undef MAD_16
                    #undef MAD_64

                    #define MAD_4(x, y)     x = y*x+y;			y = x*y+x;			x = y*x+y;			y = x*y+x;

                    #define MAD_16(x, y)    MAD_4(x, y);        MAD_4(x, y);        MAD_4(x, y);        MAD_4(x, y);

                    #define MAD_64(x, y)    MAD_16(x, y);       MAD_16(x, y);       MAD_16(x, y);       MAD_16(x, y);

                    #define MAD_256(x,y)	MAD_64(x, y);		MAD_64(x, y);		MAD_64(x, y);		MAD_64(x, y);

                    #define MAD_1024(x,y)	MAD_256(x, y);		MAD_256(x, y);		MAD_256(x, y);		MAD_256(x, y);

                    #define MAD_4096(x,y)	MAD_1024(x, y);		MAD_1024(x, y);		MAD_1024(x, y);		MAD_1024(x, y);


                    __kernel void SinglePerformance(__global float *ptr, float _A)
                    {
    
                    float x = _A;
    
                    float y = (float)get_local_id(0);

    
                    MAD_1024(x,y);
	                    MAD_1024(x,y);

    
                    ptr[get_global_id(0)] = y;

                    }


                    __kernel void DoublePerformance(__global double *ptr, double _A)
                    {
                        double x = _A;
                        double y = (double)get_local_id(0);

                        MAD_1024(x,y);
	                    MAD_1024(x,y);

                        ptr[get_global_id(0)] = y;
                    }

                    __kernel void Bandwidth(global int *ptr)
                    {
                        int index=get_local_id(0);

	                    ptr[index]=1;
                    }";
        }
    }

    public static ComputeMethod CompileKernel(string Kernel, string Method, AcceleratorDevice Device)
    {
        return new ComputeMethod(Kernel, Method, Device);
    }

    static double GetGFlops(ComputeMethod Method, bool IsFloat)
    {
        int upper = 10000000;

        float[] fvalues = Enumerable.Range(0, upper).Select(x => Convert.ToSingle(x)).ToArray();
        double[] dvalues = Enumerable.Range(0, upper).Select(x => Convert.ToDouble(x)).ToArray();

        long worksize = upper / 1000000;
        if (IsFloat)
        {
            Method.Invoke(worksize, fvalues, 2.0f); //invoke once to cache everything
        }
        else
        {
            Method.Invoke(worksize, dvalues, 2.0); //invoke once to cache everything
        }

        Stopwatch watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalMilliseconds < 25) //loop until 25ms or 10000000 elements
        {
            watch.Restart();

            if (IsFloat)
            {
                Method.Invoke(worksize);
            }
            else
            {
                Method.Invoke(worksize);
            }

            if (worksize == upper) break;
            worksize = Math.Min(worksize * 2, upper);
        }

        

        double flops = (double)4096 * worksize / (watch.Elapsed.TotalSeconds);
        double gflops = (flops / 1000000000.0);

        Method.Dispose();

        return gflops;
    }

    public static double GetDeviceGFlops_Double(AcceleratorDevice Device)
    {
        ComputeMethod method = new ComputeMethod(PerformanceKernel, "DoublePerformance", Device);
        return GetGFlops(method, false);
    }

    public static double GetDeviceGFlops_Single(AcceleratorDevice Device)
    {
        ComputeMethod method = new ComputeMethod(PerformanceKernel, "SinglePerformance", Device);
        return GetGFlops(method, true);
    }

    public static double GetDeviceBandwidth_GBps(AcceleratorDevice Device)
    {
        ComputeMethod method = new ComputeMethod(PerformanceKernel, "Bandwidth", Device);
        int[] values = Enumerable.Range(0, 1000000).ToArray();
        double bytes = values.Length * sizeof(int);

        method.Invoke(values.Length, values);
        method.Invoke(values.Length);

        Stopwatch total = Stopwatch.StartNew();
        method.Invoke(values.Length);

        double bytespersec = bytes / total.Elapsed.TotalSeconds;
        double GBytespersec = bytespersec / (8 * 1000 * 1000 * 1000.0);

        method.Dispose();
        return GBytespersec;
    }

    void CreateContext()
    {
        ComputeContextPropertyList computeContextPropertyList = new ComputeContextPropertyList(Accelerator.Device.Platform);
        context = new ComputeContext(_device.Type, computeContextPropertyList, null, IntPtr.Zero);
        queue = new ComputeCommandQueue(context, context.Devices[0], ComputeCommandQueueFlags.None);
    }

    string kernel;
    public void LoadKernel(string Kernel)
    {
        this.kernel = Kernel;
        program = new ComputeProgram(context, Kernel);

        try
        {
            program.Build(null, null, null, IntPtr.Zero);   //compile
        }
        catch (BuildProgramFailureComputeException)
        {
            string message = program.GetBuildLog(Accelerator.Device);
            program.Dispose();
            throw new ArgumentException(message);
        }
    }

    List<ComputeMemory> memory;
    void Setargument(ComputeKernel kernel, int index, object arg)
    {
        if (arg == null) throw new ArgumentException("Argument " + index + " is null");

        Type argtype = arg.GetType();
        if (argtype.IsArray)
        {
            ComputeMemory messageBuffer = (ComputeMemory)Activator.CreateInstance(typeof(ComputeBuffer<>).MakeGenericType(argtype.GetElementType()), new object[]
            {
                    context,
                    ComputeMemoryFlags.ReadWrite | ComputeMemoryFlags.UseHostPointer,
                    arg
            });
            kernel.SetMemoryArgument(index, messageBuffer); // set the array
            if (memory == null)
                memory = new List<ComputeMemory>();
            memory.Add(messageBuffer);
        }
        else
        {
            typeof(ComputeKernel).GetMethod("SetValueArgument").MakeGenericMethod(argtype).Invoke(kernel, new object[] { index, arg });
        }
    }

    ComputeKernel LastKernel = null;
    string LastMethod = null;

    ComputeKernel CreateKernel(string Method, object[] args)
    {
        if (args == null) throw new ArgumentException("You have to pass an argument to a kernel");

        ComputeKernel kernel;
        if (LastMethod == Method && LastKernel != null) //Kernel caching, do not compile twice
        {
            kernel = LastKernel;
        }
        else
        {
            kernel = program.CreateKernel(Method);
            LastKernel = kernel;
        }
        LastMethod = Method;

        for (int i = 0; i < args.Length; i++)
        {
            Setargument(kernel, i, args[i]);
        }

        return kernel;
    }

    /// <summary>
    /// Subsequent calls to Invoke work faster without arguments
    /// </summary>
    public void Invoke(string Method, long Offset, long Worksize)
    {
        if (LastKernel == null) throw new InvalidOperationException("You need to call Invoke with arguments before. All Arguments are saved");

        ComputeEventList eventList = new ComputeEventList();
        InvokeStarted?.Invoke(this, EventArgs.Empty);

        queue.Execute(LastKernel, new long[] { Offset }, new long[] { Worksize }, null, eventList);

        eventList[0].Completed += (sender, e) => EasyCL_Completed(sender, null);
        eventList[0].Aborted += (sender, e) => EasyCL_Aborted(sender, Method);

        queue.Finish();

        eventList.Dispose();
    }

    public void Invoke(string Method, long Offset, long Worksize, params object[] Args)
    {
        ComputeKernel kernel = CreateKernel(Method, Args);

        ComputeEventList eventList = new ComputeEventList();
        InvokeStarted?.Invoke(this, EventArgs.Empty);

        queue.Execute(kernel, new long[] { Offset }, new long[] { Worksize }, null, eventList);

        eventList[0].Completed += (sender, e) => EasyCL_Completed(sender, null);
        eventList[0].Aborted += (sender, e) => EasyCL_Aborted(sender, Method);

        queue.Finish();

        eventList.Dispose();
    }

    //waits for event to be fired
    Dictionary<string, AsyncManualResetEvent> CompletionLocks = new Dictionary<string, AsyncManualResetEvent>();
    public async Task InvokeAsync(string Method, long worksize, params object[] Args)
    {
        ComputeKernel kernel = CreateKernel(Method, Args);
        ComputeEventList eventList = new ComputeEventList();

        InvokeStarted?.Invoke(this, EventArgs.Empty);
        string jid = Guid.NewGuid().ToString();
        AsyncManualResetEvent evt = new AsyncManualResetEvent(false);
        lock (CompletionLocks)
        {
            CompletionLocks.Add(jid, evt);
        }

        queue.Execute(kernel, null, new long[] { worksize }, null, eventList);

        eventList[0].Completed += (sender, e) => EasyCL_Completed(sender, jid);
        eventList[0].Aborted += (sender, e) => EasyCL_Aborted(sender, Method);

        await evt.WaitAsync();

        lock (CompletionLocks)
        {
            CompletionLocks.Remove(jid);
        }
    }

    public void Dispose()
    {
        if (LastKernel != null)
            LastKernel.Dispose();
        if (queue != null)
            queue.Dispose();
        if (context != null)
            context.Dispose();
        if (program != null)
            program.Dispose();
        if (memory != null)
        {
            for (int i = 0; i < memory.Count; i++)
            {
                if (memory[i] != null)
                    memory[i].Dispose();
            }
            memory.Clear();
        }
    }

    private void EasyCL_Aborted(object sender, string Method)
    {
        UnityEngine.Debug.Log(Method + " was terminated");
        InvokeAborted?.Invoke(this, Method);
    }

    private void EasyCL_Completed(object sender, string JobID)
    {
        InvokeEnded?.Invoke(this, EventArgs.Empty);

        if (JobID != null && CompletionLocks.ContainsKey(JobID))
        {
            CompletionLocks[JobID].Set();
        }
    }

    public event EventHandler InvokeStarted;
    public event EventHandler InvokeEnded;
    public event EventHandler<string> InvokeAborted;
}