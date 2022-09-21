using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;

namespace Tracer.Core
{
    internal abstract class ReadWriteTreeNode
    {
        public bool isCurrentNode = true;
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
            public Stopwatch stopwatch;
            public MethodInfo(ReadWriteMethodTrace method)
            {
                Method = method;
                stopwatch = new Stopwatch();
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
        public ReadOnlyCollection<MethodTrace> MethodList { get; protected set; }
        public MethodTrace(
            string methodName,
            string className,
            long time,
            ReadOnlyCollection<MethodTrace> methodList)
        {
            MethodName = methodName;
            ClassName = className;
            Time = time;
            MethodList = methodList;
        }
    }
    public class ThreadTrace
    {
        public long Time { get; private set; }
        public ReadOnlyCollection<MethodTrace> MethodList { get; private set; }
        public ThreadTrace(long time, ReadOnlyCollection<MethodTrace> methodList)
        {
            Time = time;
            MethodList = methodList;
        }
    }
    public class TraceResult
    {
        public ReadOnlyDictionary<int, ThreadTrace> ThreadDictionary { get; }
        public TraceResult(ReadOnlyDictionary<int, ThreadTrace> threadDictionary)
        {
            ThreadDictionary = threadDictionary;
        }
    }

    public interface ITracer
    {
        // вызывается в начале замеряемого метода
        void StartTrace();
        // вызывается в конце замеряемого метода
        void StopTrace();
        // получить результаты измерений
        TraceResult GetTraceResult();
    }

    public class Tracer : ITracer
    {
        ReadWriteTraceResult traceResult;
        public Tracer()
        {
            traceResult = new ReadWriteTraceResult();
        }
        private void AddMethodToThread(int threadId, string methodName, string className)
        {
            if (!traceResult.ThreadDictionary.ContainsKey(threadId))
            {
                traceResult.ThreadDictionary.TryAdd(threadId, new ReadWriteThreadTrace(threadId));
            }
            AddMethod(threadId, methodName, className, traceResult.ThreadDictionary[threadId]);
        }
        private void AddMethod(int threadId, string methodName, string className, ReadWriteTreeNode treeNode)
        {
            if (treeNode.isCurrentNode)
            {
                ReadWriteMethodTrace methodTrace = new ReadWriteMethodTrace(methodName, className);
                treeNode.MethodQueue.Enqueue(methodTrace);
                treeNode.isCurrentNode = false;
                traceResult.ThreadDictionary[threadId].MethodStack.Push(new ReadWriteThreadTrace.MethodInfo(methodTrace));
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
            bool findCurrentNode = false;
            foreach (ReadWriteTreeNode node in treeNode.MethodQueue)
            {
                if (node.isCurrentNode)
                {
                    node.isCurrentNode = false;
                    treeNode.isCurrentNode = true;
                    findCurrentNode = true;
                    return true;
                }
            }
            if (!findCurrentNode)
            {
                foreach (ReadWriteTreeNode node in treeNode.MethodQueue)
                {
                    if (CloseNode(node)) return true;
                }
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
            // со стекового фрейма берется второй метод, так как первый - StartTrace
            if (stackTrace.FrameCount > 1)
            {
                stackFrame = stackTrace.GetFrame(1);
                className = stackFrame.GetMethod().DeclaringType.Name;
                methodName = stackFrame.GetMethod().Name;
                if (className != typeof(Tracer).Name)
                {
                    Console.WriteLine("class:  " + className);
                    Console.WriteLine("method: " + methodName);
                }
                Console.WriteLine('\n');
                AddMethodToThread(threadId, methodName, className);
                ReadWriteThreadTrace.MethodInfo methodInfo;
                while (!traceResult.ThreadDictionary[threadId].MethodStack.TryPeek(out methodInfo)) { }
                methodInfo.stopwatch.Start();
            }
        }
        public void StopTrace()
        {
            int threadId = Thread.CurrentThread.ManagedThreadId;
            ReadWriteThreadTrace.MethodInfo methodInfo;
            while (!traceResult.ThreadDictionary[threadId].MethodStack.TryPop(out methodInfo)) { }
            methodInfo.stopwatch.Stop();
            methodInfo.Method.Time = methodInfo.stopwatch.ElapsedMilliseconds;
            CloseNode(traceResult.ThreadDictionary[threadId]);
        }
        public TraceResult GetTraceResult()
        {
            foreach (KeyValuePair<int, ReadWriteThreadTrace> keyValuePair in traceResult.ThreadDictionary)
            {
                foreach (ReadWriteMethodTrace readWriteMethodTrace in keyValuePair.Value.MethodQueue)
                {
                    keyValuePair.Value.Time += readWriteMethodTrace.Time;
                }
            }
            TraceResult TraceResult = new TraceResult(ConvertToReadOnly(traceResult.ThreadDictionary));
            return TraceResult;
        }
    }
}