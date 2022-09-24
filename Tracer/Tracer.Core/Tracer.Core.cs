using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Tracer.Core
{
    internal abstract class ReadWriteTreeNode
    {
        public bool IsCurrentNode = true;
        public ConcurrentQueue<ReadWriteMethodTrace> MethodQueue = new();
    }
    internal class ReadWriteMethodTrace : ReadWriteTreeNode
    {
        public string MethodName;
        public string ClassName;
        public long Time;
        public ReadWriteMethodTrace(string methodName, string className)
        {
            MethodName = methodName;
            ClassName = className;
            Time = 0;
        }
    }
    internal class ReadWriteThreadTrace : ReadWriteTreeNode
    {
        public int ThreadId;
        public long Time;
        public ConcurrentStack<MethodInfo> MethodStack = new ConcurrentStack<MethodInfo>();
        public ReadWriteThreadTrace(int threadId)
        {
            ThreadId = threadId;
            Time = 0;
        }
        public class MethodInfo
        {
            public ReadWriteMethodTrace Method;
            public Stopwatch Stopwatch;
            public MethodInfo(ReadWriteMethodTrace method)
            {
                Method = method;
                Stopwatch = new Stopwatch();
            }
        }
    }
    internal class ReadWriteTraceResult
    {
        public ConcurrentDictionary<int, ReadWriteThreadTrace> ThreadDictionary { get; internal set; }
        public ReadWriteTraceResult()
        {
            ThreadDictionary = new ConcurrentDictionary<int, ReadWriteThreadTrace>();
        }
    }

    public class MethodTrace
    {
        public string MethodName { get; private set; }
        public string ClassName { get; private set; }
        public long Time { get; private set; }
        public ReadOnlyCollection<MethodTrace> Methods { get; protected set; }
        public MethodTrace(
            string methodName,
            string className,
            long time,
            ReadOnlyCollection<MethodTrace> methods)
        {
            MethodName = methodName;
            ClassName = className;
            Time = time;
            Methods = methods;
        }
    }
    public class ThreadTrace
    {
        public long Time { get; private set; }
        public ReadOnlyCollection<MethodTrace> Methods { get; private set; }
        public ThreadTrace(long time, ReadOnlyCollection<MethodTrace> methods)
        {
            Time = time;
            Methods = methods;
        }
    }
    public class TraceResult
    {
        public ReadOnlyDictionary<int, ThreadTrace> Threads { get; }
        public TraceResult(ReadOnlyDictionary<int, ThreadTrace> threadDictionary)
        {
            Threads = threadDictionary;
        }
    }

    public interface ITracer
    {
        void StartTrace();
        void StopTrace();
        TraceResult GetTraceResult();
    }

    public class Tracer : ITracer
    {
        ReadWriteTraceResult _traceResult;
        public Tracer()
        {
            _traceResult = new ReadWriteTraceResult();
        }
        private void AddMethodToThread(int threadId, string methodName, string className)
        {
            if (!_traceResult.ThreadDictionary.ContainsKey(threadId))
            {
                _traceResult.ThreadDictionary.TryAdd(threadId, new ReadWriteThreadTrace(threadId));
            }
            AddMethod(threadId, methodName, className, _traceResult.ThreadDictionary[threadId]);
        }
        private void AddMethod(int threadId, string methodName, string className, ReadWriteTreeNode treeNode)
        {
            if (treeNode.IsCurrentNode)
            {
                ReadWriteMethodTrace methodTrace = new ReadWriteMethodTrace(methodName, className);
                treeNode.MethodQueue.Enqueue(methodTrace);
                treeNode.IsCurrentNode = false;
                _traceResult.ThreadDictionary[threadId].MethodStack.Push(new ReadWriteThreadTrace.MethodInfo(methodTrace));
            }
            else
            {
                foreach (ReadWriteTreeNode method in treeNode.MethodQueue)
                {
                    AddMethod(threadId, methodName, className, method);
                }
            }
        }
        private bool CloseNode(ReadWriteTreeNode treeNode)
        {
            foreach (ReadWriteTreeNode node in treeNode.MethodQueue)
            {
                if (node.IsCurrentNode)
                {
                    node.IsCurrentNode = false;
                    treeNode.IsCurrentNode = true;
                    return true;
                }
            }
            foreach (ReadWriteTreeNode node in treeNode.MethodQueue)
            {
                if (CloseNode(node)) return true;
            }
            return false;
        }
        private ReadOnlyCollection<MethodTrace> ConvertToReadOnly(ConcurrentQueue<ReadWriteMethodTrace> methodList)
        {
            List<MethodTrace> list = new List<MethodTrace>();
            foreach (ReadWriteMethodTrace methodTrace in methodList)
                if (methodTrace.MethodQueue.Count == 0)
                {
                    list.Add(new MethodTrace(
                        methodTrace.MethodName,
                        methodTrace.ClassName,
                        methodTrace.Time,
                        new ReadOnlyCollection<MethodTrace>(new List<MethodTrace>())
                        ));
                }
                else
                {
                    list.Add(new MethodTrace(
                        methodTrace.MethodName,
                        methodTrace.ClassName,
                        methodTrace.Time,
                        new ReadOnlyCollection<MethodTrace>(ConvertToReadOnly(methodTrace.MethodQueue))
                        ));
                }
            return new ReadOnlyCollection<MethodTrace>(list);
        }
        private ReadOnlyDictionary<int, ThreadTrace> ConvertToReadOnly(ConcurrentDictionary<int, ReadWriteThreadTrace> threadDictionary)
        {
            Dictionary<int, ThreadTrace> dictionary = new Dictionary<int, ThreadTrace>();
            foreach (ReadWriteThreadTrace threadTrace in threadDictionary.Values)
            {
                dictionary.Add(threadTrace.ThreadId,
                    new ThreadTrace(
                        threadTrace.Time,
                        new ReadOnlyCollection<MethodTrace>(ConvertToReadOnly(threadTrace.MethodQueue))
                    ));
            }
            return new ReadOnlyDictionary<int, ThreadTrace>(dictionary);
        }
        public void StartTrace()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            StackTrace stackTrace = new StackTrace(true);
            StackFrame stackFrame;
            string className;
            string methodName;
            // Со стекового фрейма берется второй метод, так как первый - StartTrace
            if (stackTrace.FrameCount > 1)
            {
                stackFrame = stackTrace.GetFrame(1);
                className = stackFrame.GetMethod().DeclaringType.Name;
                methodName = stackFrame.GetMethod().Name;
                /*if (className != typeof(Tracer).Name)
                {
                    Console.WriteLine("class:  " + className);
                    Console.WriteLine("method: " + methodName);
                }
                Console.WriteLine('\n'); */
                AddMethodToThread(threadId, methodName, className);
                ReadWriteThreadTrace.MethodInfo methodInfo;
                while (!_traceResult.ThreadDictionary[threadId].MethodStack.TryPeek(out methodInfo)) { }
                methodInfo.Stopwatch.Start();
            }
        }
        public void StopTrace()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            ReadWriteThreadTrace.MethodInfo methodInfo;
            while (!_traceResult.ThreadDictionary[threadId].MethodStack.TryPop(out methodInfo)) { }
            methodInfo.Stopwatch.Stop();
            methodInfo.Method.Time = methodInfo.Stopwatch.ElapsedMilliseconds;
            CloseNode(_traceResult.ThreadDictionary[threadId]);
        }
        public TraceResult GetTraceResult()
        {
            foreach (KeyValuePair<int, ReadWriteThreadTrace> keyValuePair in _traceResult.ThreadDictionary)
            {
                foreach (ReadWriteMethodTrace readWriteMethodTrace in keyValuePair.Value.MethodQueue)
                {
                    keyValuePair.Value.Time += readWriteMethodTrace.Time;
                }
            }
            TraceResult TraceResult = new TraceResult(ConvertToReadOnly(_traceResult.ThreadDictionary));
            return TraceResult;
        }
    }
}