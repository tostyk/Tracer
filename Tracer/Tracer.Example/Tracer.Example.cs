using Tracer.Core;

namespace Tracer.Example
{
    public class Foo
    {
        private Bar _bar;
        private ITracer _tracer;

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
        public void StartRecursion(int count)
        {
            Recursion(count);
        }
        public void Recursion(int count)
        {
            _tracer.StartTrace();
            Thread.Sleep(50);
            count--;
            if (count > 0)
            {
                Recursion(count);
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

            void ThreadMethod() => 
                foo.MyMethod2(); 
                foo.Recursion(3);

            Thread myThread = new Thread(ThreadMethod);
            myThread.Start();
            foo.MyMethod1();
            foo.MyMethod2();
            foo.Recursion(2);
            TraceResult traceResult = tracer.GetTraceResult();

            XmlTracerSerializer xmlTracerSerializer = new XmlTracerSerializer();
            JsonTracerSerializer jsonTracerSerializer = new JsonTracerSerializer();

            string xml = xmlTracerSerializer.Serialize(traceResult);
            string json = jsonTracerSerializer.Serialize(traceResult);

            ConsoleWriter consoleWriter = new ConsoleWriter();
            consoleWriter.Write(xml);
            consoleWriter.Write(json);

            FileWriter xmlFileWriter = new FileWriter("file.xml.txt");
            xmlFileWriter.Write(xml);
            FileWriter jsonFileWriter = new FileWriter("file.json.txt");
            jsonFileWriter.Write(json);
        }
    }
}