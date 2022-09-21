using Tracer.Core;

namespace Tracer.Example
{
    public class Foo
    {
        private Bar _bar;
        private ITracer _tracer;
        private int recursionCount = 3;

        internal Foo(ITracer tracer)
        {
            _tracer = tracer;
            _bar = new Bar(_tracer);
        }

        public void MyMethod1()
        {
            _tracer.StartTrace();
            Thread.Sleep(300);
            _bar.InnerMethod1();
            _bar.InnerMethod2();
            _tracer.StopTrace();
        }
        public void MyMethod2()
        {
            _tracer.StartTrace();
            _bar.InnerMethod2();
            _tracer.StopTrace();
        }

        public void Recursion()
        {
            _tracer.StartTrace();
            Thread.Sleep(50);
            recursionCount--;
            if (recursionCount > 0)
            {
                Recursion();
            }
            _tracer.StopTrace();
        }
    }
    public class Bar
    {
        private ITracer _tracer;
        internal Bar(ITracer tracer)
        {
            _tracer = tracer;
        }
        public void InnerMethod1()
        {
            _tracer.StartTrace();
            Thread.Sleep(100);
            _tracer.StopTrace();
        }
        public void InnerMethod2()
        {
            _tracer.StartTrace();
            Thread.Sleep(200);
            _tracer.StopTrace();
        }
    }
    class Program
    {
        static void Main()
        {
            Core.Tracer tracer = new Core.Tracer();
            Foo foo = new Foo(tracer);
            foo.MyMethod1();
            foo.MyMethod2();
            foo.Recursion();
            TraceResult traceResult = tracer.GetTraceResult();
        }
    }
}