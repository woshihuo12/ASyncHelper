using UnityEngine;
using System;
using System.Collections;

namespace ASyncHelper
{
    [Serializable]
    public class OperationOutputs
    {
    }

    [Serializable]
    public class OperationErrors
    {
        public bool ExceptionError;
        public Exception ExceptionCause;
    }

    public class OperationProgress
    {
        public float Value;
    }

    public class ErrorEventArgs<TErrors> : EventArgs where TErrors : OperationErrors
    {
        public readonly TErrors Errors;
        public ErrorEventArgs(TErrors errors)
        {
            Errors = errors;
        }
    }
    public delegate void ErrorEventHandler<TErrors>(object sender, ErrorEventArgs<TErrors> e) where TErrors : OperationErrors;

    public class SuccessEventArgs<TOutputs> : EventArgs where TOutputs : OperationOutputs
    {
        public readonly TOutputs Outputs;
        public SuccessEventArgs(TOutputs outputs)
        {
            Outputs = outputs;
        }
    }
    public delegate void SuccessEventHandler<TOutputs>(object sender, SuccessEventArgs<TOutputs> e) where TOutputs : OperationOutputs;

    public class ProgressEventArgs<TProgress> : EventArgs where TProgress : OperationProgress
    {
        public readonly TProgress Progress;

        public ProgressEventArgs(TProgress progress)
        {
            Progress = progress;
        }
    }
    public delegate void ProgressEventHandler<TProgress>(object sender, ProgressEventArgs<TProgress> e) where TProgress : OperationProgress;

    public interface IOperationScript<TOutputs, TErrors>
        where TOutputs : OperationOutputs
        where TErrors : OperationErrors
    {
        event SuccessEventHandler<TOutputs> Success;
        event ErrorEventHandler<TErrors> Error;
        void Execute();
    }

    public interface IAsyncOperationScript<TOutputs, TErrors, TProgress> : IOperationScript<TOutputs, TErrors>
        where TOutputs : OperationOutputs
        where TErrors : OperationErrors
        where TProgress : OperationProgress
    {
        event ProgressEventHandler<TProgress> Progress;
        void Cancel();
    }

    [Serializable]
    public abstract class OperationScript<TOutputs, TErrors> : IOperationScript<TOutputs, TErrors>
        where TOutputs : OperationOutputs
        where TErrors : OperationErrors, new()
    {
        protected sealed class Result
        {
            public readonly TOutputs Outputs;
            public readonly TErrors Errors;

            public static implicit operator Result(TOutputs outputs)
            {
                return new Result(outputs);
            }

            public static implicit operator Result(TErrors errors)
            {
                return new Result(errors);
            }

            public Result(TOutputs outputs)
            {
                if (outputs == null)
                {
                    throw new ArgumentException("outputs was null");
                }
                Outputs = outputs;
            }

            public Result(TErrors errors)
            {
                if (errors == null)
                {
                    throw new ArgumentException("errors was null");
                }
                Errors = errors;
            }

            public bool IsSuccess()
            {
                return (Outputs != null);
            }

            public bool IsError()
            {
                return (Errors != null);
            }
        }

        public event SuccessEventHandler<TOutputs> Success;
        public event ErrorEventHandler<TErrors> Error;

        public void Execute()
        {
            try
            {
                Result result = ExecuteCore();
                if (result.IsSuccess() && (Success != null))
                {
                    Success(this, new SuccessEventArgs<TOutputs>(result.Outputs));
                }
                if (result.IsError() && (Error != null))
                {
                    Error(this, new ErrorEventArgs<TErrors>(result.Errors));
                }
            }
            catch (Exception e)
            {
                if (Error != null)
                {
                    Error(this, new ErrorEventArgs<TErrors>(new TErrors() { ExceptionError = true, ExceptionCause = e }));
                }
            }
        }
        protected abstract Result ExecuteCore();
    }

    /////////////////////////////////////////////////////////// 
    [Serializable]
    public class AsyncOperationErrors : OperationErrors
    {
        public bool ResultWasNullError;
        public bool AbortedError;
    }

    public abstract class AsyncOperationCollective<TOutputs, TErrors, TProgress> : AsyncOperationScript<TOutputs, TErrors, TProgress>
        where TOutputs : OperationOutputs, new()
        where TErrors : AsyncOperationErrors, new()
        where TProgress : OperationProgress, new()
    {
        protected abstract void Process();
        protected override IEnumerator ExecuteCore()
        {
            try
            {
                Process();
            }
            catch (Exception e)
            {
                HandleException(e);
            }
            yield break;
        }
    }

    public class AsyncOperationDelegate : AsyncOperationScript<OperationOutputs, AsyncOperationErrors, OperationProgress>
    {
        public static AsyncOperationDelegate Call(Func<IEnumerator> callback)
        {
            AsyncOperationDelegate asyncOps = new AsyncOperationDelegate(callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public static AsyncOperationDelegate Call(string name, Func<IEnumerator> callback)
        {
            AsyncOperationDelegate asyncOps = new AsyncOperationDelegate(name, callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public AsyncOperationDelegate(Func<IEnumerator> callback)
            : this("AsyncOperationDelegate", callback)
        {
        }

        public AsyncOperationDelegate(string name, Func<IEnumerator> callback)
            : base()
        {
            this.name = name;
            this.callback = callback;
            nullResultIsSuccess = true;
        }

        protected override IEnumerator ExecuteCore()
        {
            yield return null;
        }
    }

    [Serializable]
    public abstract partial class AsyncOperationScript<TOutputs, TErrors, TProgress> : IAsyncOperationScript<TOutputs, TErrors, TProgress>
        where TOutputs : OperationOutputs, new()
        where TErrors : AsyncOperationErrors, new()
        where TProgress : OperationProgress, new()
    {
        protected sealed class Result
        {
            public readonly TOutputs Outputs;
            public readonly TErrors Errors;

            public static implicit operator Result(TOutputs outputs)
            {
                return new Result(outputs);
            }

            public static implicit operator Result(TErrors errors)
            {
                return new Result(errors);
            }

            public Result(TOutputs outputs)
            {
                if (outputs == null)
                {
                    throw new ArgumentException("outputs was null");
                }
                Outputs = outputs;
            }

            public Result(TErrors errors)
            {
                if (errors == null)
                {
                    throw new ArgumentException("errors was null");
                }
                Errors = errors;
            }

            public bool IsSuccess()
            {
                return (Outputs != null);
            }

            public bool IsError()
            {
                return (Errors != null);
            }
        }

        public event SuccessEventHandler<TOutputs> Success;
        public event ErrorEventHandler<TErrors> Error;
        public event ProgressEventHandler<TProgress> Progress;

        protected string name;
        protected Func<IEnumerator> callback;
        protected Executor executor;
        protected Result result;
        protected bool nullResultIsSuccess;

        public AsyncOperationScript()
        {
            name = "UnityAsyncOperationScript";
            callback = ExecuteCore;
        }

        public virtual void Execute()
        {
            result = null;
            GameObject gameObject = new GameObject(name);
            executor = gameObject.AddComponent<Executor>();
            executor.ExecuteCoroutine = callback;
            executor.SendResult = SendResult;
            executor.AbortCallback = HandleAbort;
            executor.Execute();
        }

        public void Cancel()
        {
            if (executor == null)
            {
                return;
            }
            executor.Cancel();
        }

        protected abstract IEnumerator ExecuteCore();

        protected virtual void NotifyProgress(float value)
        {
            NotifyProgress(new TProgress() { Value = value });
        }

        protected virtual void NotifyProgress(TProgress progress)
        {
            if (Progress == null)
            {
                return;
            }
            Progress(this, new ProgressEventArgs<TProgress>(progress));
        }

        protected virtual void WaitForResult()
        {
            if (executor == null)
            {
                HandleAbort();
                return;
            }
            executor.WaitForSendResult();
        }

        protected virtual void CompleteWaitForResult()
        {
            if (executor == null)
            {
                HandleAbort();
                return;
            }
            executor.CompleteWaitForResult();
        }

        protected virtual void SendResult()
        {
            if (result == null)
            {
                if (nullResultIsSuccess)
                {
                    if (Success != null)
                    {
                        Success(this, new SuccessEventArgs<TOutputs>(new TOutputs()));
                    }
                }
                else
                {
                    if (Error != null)
                    {
                        Error(this, new ErrorEventArgs<TErrors>(new TErrors() { ResultWasNullError = true }));
                    }
                }
                return;
            }
            if (result.IsSuccess() && (Success != null))
            {
                Success(this, new SuccessEventArgs<TOutputs>(result.Outputs));
            }
            if (result.IsError() && (Error != null))
            {
                Error(this, new ErrorEventArgs<TErrors>(result.Errors));
            }
        }

        protected virtual void HandleException(Exception e)
        {
            result = new TErrors() { ExceptionError = true, ExceptionCause = e };
            CompleteWaitForResult();
        }

        protected virtual void HandleAbort()
        {
            if (Error != null)
            {
                Error(this, new ErrorEventArgs<TErrors>(new TErrors() { AbortedError = true }));
            }
        }
    }

    public class Executor : MonoBehaviour
    {
        public Func<IEnumerator> ExecuteCoroutine;
        public Action SendResult;
        public Action<Exception> ExceptionCallback;
        public Action AbortCallback;
        protected bool isOrderdExecute = false;
        protected bool isOrderdSendResult = true;
        bool isProcessedWaitForExecuteOrder = false;
        bool isProcessedExecuteCoroutine = false;
        bool isProcessedWaitForSendResultOrder = false;
        bool isProcessedSendResult = false;

        public void Execute()
        {
            if (isOrderdExecute)
            {
                throw new InvalidOperationException("already executed!");
            }
            isOrderdExecute = true;
        }

        public void WaitForSendResult()
        {
            isOrderdSendResult = false;
        }

        public void CompleteWaitForResult()
        {
            isOrderdSendResult = true;
        }

        public void Cancel()
        {
            StopAllCoroutines();
            this.DestroyObject(gameObject);
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        IEnumerator Start()
        {
            if (!isOrderdExecute)
            {
                yield return StartCoroutine(WaitForExecuteOrder());
            }
            isProcessedWaitForExecuteOrder = true;

            if (ExecuteCoroutine != null)
            {
                yield return StartCoroutine(ExecuteCoroutine());
            }
            isProcessedExecuteCoroutine = true;

            if (!isOrderdSendResult)
            {
                yield return StartCoroutine(WaitForSendResultOrder());
            }
            isProcessedWaitForSendResultOrder = true;

            if (SendResult != null)
            {
                SendResult();
            }
            isProcessedSendResult = true;

            this.DestroyObject(gameObject);
        }

        void OnDestroy()
        {
            if (IsCompleted())
            {
                return;
            }
            if (AbortCallback != null)
            {
                AbortCallback();
            }
        }

        void OnApplicationQuit()
        {
            if (IsCompleted())
            {
                return;
            }
            AbortCallback = null;
        }

        IEnumerator WaitForExecuteOrder()
        {
            while (!isOrderdExecute)
            {
                yield return null;
            }
        }

        IEnumerator WaitForSendResultOrder()
        {
            while (!isOrderdSendResult)
            {
                yield return null;
            }
        }

        bool IsCompleted()
        {
            return isProcessedWaitForExecuteOrder
                && isProcessedExecuteCoroutine
                && isProcessedWaitForSendResultOrder
                && isProcessedSendResult;
        }

        void DestroyObject(GameObject gameobject)
        {
#if UNITY_EDITOR
            UnityEngine.GameObject.DestroyImmediate(gameobject);
#else
            UnityEngine.GameObject.Destroy(gameobject);
#endif
        }
    }

    [Serializable]
    public class InvokeAfterDelay : AsyncOperationScript<OperationOutputs, AsyncOperationErrors, OperationProgress>
    {
        protected Action delayInvokeCallback;
        float delay;
        float startTime;

        public static InvokeAfterDelay Call(Action callback)
        {
            InvokeAfterDelay asyncOps = new InvokeAfterDelay(callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public static InvokeAfterDelay Call(Action callback, float delay)
        {
            InvokeAfterDelay asyncOps = new InvokeAfterDelay(callback, delay);
            asyncOps.Execute();
            return asyncOps;
        }

        public static InvokeAfterDelay Call(float delay, Action callback)
        {
            InvokeAfterDelay asyncOps = new InvokeAfterDelay(delay, callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public static InvokeAfterDelay Call(string name, Action callback, float delay)
        {
            InvokeAfterDelay asyncOps = new InvokeAfterDelay(name, callback, delay);
            asyncOps.Execute();
            return asyncOps;
        }

        public static InvokeAfterDelay Call(string name, float delay, Action callback)
        {
            InvokeAfterDelay asyncOps = new InvokeAfterDelay(name, delay, callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public InvokeAfterDelay(Action callback)
            : this(callback, 0.0f)
        {
        }

        public InvokeAfterDelay(float delay, Action callback)
            : this(callback, delay)
        {
        }

        public InvokeAfterDelay(Action callback, float delay)
            : this("InvokeAfterDelay", callback, delay)
        {
        }

        public InvokeAfterDelay(string name, float delay, Action callback)
            : this(name, callback, delay)
        {
        }

        public InvokeAfterDelay(string name, Action callback, float delay)
        {
            this.name = name;
            this.delayInvokeCallback = callback;
            this.delay = delay;
            this.nullResultIsSuccess = true;
        }

        public override void Execute()
        {
            startTime = Time.realtimeSinceStartup;
            base.Execute();
        }

        protected override IEnumerator ExecuteCore()
        {
            while (true)
            {
                float elapsed = Time.realtimeSinceStartup - startTime;
                if (elapsed >= delay)
                {
                    break;
                }
                yield return null;
            }
            try
            {
                delayInvokeCallback();
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }
    }

    [Serializable]
    public class InvokeAfterFrame : AsyncOperationScript<OperationOutputs, AsyncOperationErrors, OperationProgress>
    {
        protected Action delayInvokeCallback;
        int delayFrame;
        int startFrame;

        public static InvokeAfterFrame Call(Action callback)
        {
            InvokeAfterFrame asyncOps = new InvokeAfterFrame(callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public static InvokeAfterFrame Call(Action callback, int delayFrame)
        {
            InvokeAfterFrame asyncOps = new InvokeAfterFrame(callback, delayFrame);
            asyncOps.Execute();
            return asyncOps;
        }

        public static InvokeAfterFrame Call(int delayFrame, Action callback)
        {
            InvokeAfterFrame asyncOps = new InvokeAfterFrame(delayFrame, callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public static InvokeAfterFrame Call(string name, Action callback, int delayFrame)
        {
            InvokeAfterFrame asyncOps = new InvokeAfterFrame(name, callback, delayFrame);
            asyncOps.Execute();
            return asyncOps;
        }

        public static InvokeAfterFrame Call(string name, int delayFrame, Action callback)
        {
            InvokeAfterFrame asyncOps = new InvokeAfterFrame(name, delayFrame, callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public InvokeAfterFrame(Action callback)
            : this(callback, 1)
        {
        }

        public InvokeAfterFrame(int delayFrame, Action callback)
            : this(callback, delayFrame)
        {
        }

        public InvokeAfterFrame(Action callback, int delayFrame)
            : this("InvokeAfterFrame", callback, delayFrame)
        {
        }

        public InvokeAfterFrame(string name, int delayFrame, Action callback)
            : this(name, callback, delayFrame)
        {
        }

        public InvokeAfterFrame(string name, Action callback, int delayFrame)
        {
            this.name = name;
            this.delayInvokeCallback = callback;
            this.delayFrame = delayFrame;
            this.nullResultIsSuccess = true;
        }

        public override void Execute()
        {
            startFrame = Time.frameCount;
            base.Execute();
        }

        protected override IEnumerator ExecuteCore()
        {
            while (true)
            {
                int elapsed = Time.frameCount - startFrame;
                if (elapsed >= delayFrame)
                {
                    break;
                }
                yield return null;
            }
            try
            {
                delayInvokeCallback();
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }
    }

    [Serializable]
    public class InvokeNextFrame : AsyncOperationScript<OperationOutputs, AsyncOperationErrors, OperationProgress>
    {
        protected Action delayInvokeCallback;
        int startFrame;

        public static InvokeNextFrame Call(Action callback)
        {
            InvokeNextFrame asyncOps = new InvokeNextFrame(callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public static InvokeNextFrame Call(string name, Action callback)
        {
            InvokeNextFrame asyncOps = new InvokeNextFrame(name, callback);
            asyncOps.Execute();
            return asyncOps;
        }

        public InvokeNextFrame(Action callback)
            : this("InvokeNextFrame", callback)
        {
        }

        public InvokeNextFrame(string name, Action callback)
        {
            this.name = name;
            this.delayInvokeCallback = callback;
            this.nullResultIsSuccess = true;
        }

        public override void Execute()
        {
            startFrame = Time.frameCount;
            base.Execute();
        }

        protected override IEnumerator ExecuteCore()
        {
            while (true)
            {
                int elapsed = Time.frameCount - startFrame;
                if (elapsed > 0)
                {
                    break;
                }
                yield return null;
            }
            try
            {
                delayInvokeCallback();
            }
            catch (Exception e)
            {
                HandleException(e);
            }
        }
    }
}