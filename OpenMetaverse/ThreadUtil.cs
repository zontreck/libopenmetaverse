// Written by Peter A. Bromberg, found at
// http://www.eggheadcafe.com/articles/20060727.asp

using System;

/// <summary>
/// </summary>
public class ThreadUtil
{
    /// <summary>
    ///     An instance of DelegateWrapper which calls InvokeWrappedDelegate,
    ///     which in turn calls the DynamicInvoke method of the wrapped
    ///     delegate
    /// </summary>
    private static readonly DelegateWrapper wrapperInstance = InvokeWrappedDelegate;

    /// <summary>
    ///     Callback used to call EndInvoke on the asynchronously
    ///     invoked DelegateWrapper
    /// </summary>
    private static readonly AsyncCallback callback = EndWrapperInvoke;

    /// <summary>
    ///     Executes the specified delegate with the specified arguments
    ///     asynchronously on a thread pool thread
    /// </summary>
    /// <param name="d"></param>
    /// <param name="args"></param>
    public static void FireAndForget(Delegate d, params object[] args)
    {
        // Invoke the wrapper asynchronously, which will then
        // execute the wrapped delegate synchronously (in the
        // thread pool thread)
        if (d != null) wrapperInstance.BeginInvoke(d, args, callback, null);
    }

    /// <summary>
    ///     Invokes the wrapped delegate synchronously
    /// </summary>
    /// <param name="d"></param>
    /// <param name="args"></param>
    private static void InvokeWrappedDelegate(Delegate d, object[] args)
    {
        d.DynamicInvoke(args);
    }

    /// <summary>
    ///     Calls EndInvoke on the wrapper and Close on the resulting WaitHandle
    ///     to prevent resource leaks
    /// </summary>
    /// <param name="ar"></param>
    private static void EndWrapperInvoke(IAsyncResult ar)
    {
        wrapperInstance.EndInvoke(ar);
        ar.AsyncWaitHandle.Close();
    }

    /// <summary>
    ///     Delegate to wrap another delegate and its arguments
    /// </summary>
    /// <param name="d"></param>
    /// <param name="args"></param>
    private delegate void DelegateWrapper(Delegate d, object[] args);
}