namespace Tracer.Core.Tests
{
    class InnerClass
    {
        private ITracer _tracer;
        public InnerClass(ITracer tracer)
        {
            _tracer = tracer;
        }
        public void InnerMethod()
        {
            _tracer.StartTrace();
            Thread.Sleep(500);
            _tracer.StopTrace();
        }
    }

    class ExampleClass
    {
        ITracer _tracer;
        InnerClass _innerClass;
        public ExampleClass(ITracer tracer)
        {
            _tracer = tracer;
            _innerClass = new InnerClass(_tracer);
        }
        public void SimpleMethod1()
        {
            _tracer.StartTrace();
            Thread.Sleep(200);
            _tracer.StopTrace();
        }
        public void SimpleMethod2()
        {
            _tracer.StartTrace();
            Thread.Sleep(300);
            _tracer.StopTrace();
        }
        public void OutsideMethod()
        {
            _tracer.StartTrace();
            _innerClass.InnerMethod();
            _tracer.StopTrace();
        }
        public void Recursion(int count)
        {
            _tracer.StartTrace();
            Thread.Sleep(100);
            count--;
            if (count > 0)
            {
                Recursion(count);
            }
            _tracer.StopTrace();
        }
    }
    public class Tests
    {
        Tracer _tracer;
        ExampleClass _obj;
        int _currentThreadId;

        [SetUp]
        public void Setup()
        {
            _tracer = new Tracer();
            _obj = new ExampleClass(_tracer);
            _currentThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [Test]
        public void OneThread_OneSimpleMethod()
        {
            _obj.SimpleMethod1();
            TraceResult traceResult = _tracer.GetTraceResult();

            var threads = traceResult.Threads;
            // Check 1 thread
            Assert.That(threads.Count, Is.EqualTo(1));
            // Check 1 method in 1st thread
            Assert.That(threads.Keys.Contains(_currentThreadId));
            Assert.That(threads[_currentThreadId].Methods.Count, Is.EqualTo(1));

            var method = threads[_currentThreadId].Methods[0];
            // Check method information
            Assert.Multiple(() =>
            {
                Assert.That(threads[_currentThreadId].Time, Is.AtLeast(200));
                Assert.That(method.Time, Is.AtLeast(200));
                Assert.That(method.Methods.Count, Is.EqualTo(0));
                Assert.That(method.MethodName, Is.EqualTo("SimpleMethod1"));
                Assert.That(method.ClassName, Is.EqualTo("ExampleClass"));
            });
        }
        [Test]
        public void OneThread_TwoSimpleMethods()
        {
            _obj.SimpleMethod1();
            _obj.SimpleMethod2();
            TraceResult traceResult = _tracer.GetTraceResult();

            var threads = traceResult.Threads;
            // Check 1 thread
            Assert.That(threads.Count, Is.EqualTo(1));
            // Check 1 method in 1st thread
            Assert.That(threads.Keys.Contains(_currentThreadId));
            Assert.That(threads[_currentThreadId].Methods.Count, Is.EqualTo(2));

            var method = threads[_currentThreadId].Methods[0];
            Assert.That(threads[_currentThreadId].Time, Is.AtLeast(200 + 300));
            // Check 1st method information
            Assert.Multiple(() =>
            {
                Assert.That(method.Time, Is.AtLeast(200));
                Assert.That(method.Methods.Count, Is.EqualTo(0));
                Assert.That(method.MethodName, Is.EqualTo("SimpleMethod1"));
                Assert.That(method.ClassName, Is.EqualTo("ExampleClass"));
            });
            method = threads[_currentThreadId].Methods[1];
            // Check 2nd method information
            Assert.Multiple(() =>
            {
                Assert.That(method.Time, Is.AtLeast(300));
                Assert.That(method.Methods.Count, Is.EqualTo(0));
                Assert.That(method.MethodName, Is.EqualTo("SimpleMethod2"));
                Assert.That(method.ClassName, Is.EqualTo("ExampleClass"));
            });
        }
        [Test]
        public void OneThread_OneOuterMethod()
        { 
            _obj.OutsideMethod();
            TraceResult traceResult = _tracer.GetTraceResult();

            var threads = traceResult.Threads;

            // Check 1 thread
            Assert.That(threads.Count, Is.EqualTo(1));
            // Check 2 methods in 1st thread
            Assert.That(threads.Keys.Contains(_currentThreadId));
            Assert.That(threads[_currentThreadId].Methods.Count, Is.EqualTo(1));

            Assert.That(threads[_currentThreadId].Time, Is.AtLeast(500));
            // Check outside method
            var method = threads[_currentThreadId].Methods[0];
            Assert.Multiple(() =>
            {
                Assert.That(method.Time, Is.AtLeast(500));
                Assert.That(method.Methods.Count, Is.EqualTo(1));
                Assert.That(method.MethodName, Is.EqualTo("OutsideMethod"));
                Assert.That(method.ClassName, Is.EqualTo("ExampleClass"));
            });
            // Check inner method
            method = method.Methods[0];
            Assert.Multiple(() =>
            { 
                Assert.That(method.Time, Is.AtLeast(500));
                Assert.That(method.Methods.Count, Is.EqualTo(0));
                Assert.That(method.MethodName, Is.EqualTo("InnerMethod"));
                Assert.That(method.ClassName, Is.EqualTo("InnerClass"));
            });
        }
        [Test]
        public void OneThread_RecursionMethod()
        {
            _obj.Recursion(3);
            TraceResult traceResult = _tracer.GetTraceResult();

            var threads = traceResult.Threads;

            Assert.That(threads.Count, Is.EqualTo(1));
            Assert.That(threads.Keys.Contains(_currentThreadId));
            Assert.That(threads[_currentThreadId].Methods.Count, Is.EqualTo(1));

            var method = threads[_currentThreadId].Methods[0];
            Assert.Multiple(() =>
            {
                Assert.That(method.Time, Is.AtLeast(100 * 3));
                Assert.That(method.Methods.Count, Is.EqualTo(1));
                Assert.That(method.MethodName, Is.EqualTo("Recursion"));
                Assert.That(method.ClassName, Is.EqualTo("ExampleClass"));
            });
            method = method.Methods[0];
            Assert.Multiple(() =>
            {
                Assert.That(method.Time, Is.AtLeast(100 * 2));
                Assert.That(method.Methods.Count, Is.EqualTo(1));
                Assert.That(method.MethodName, Is.EqualTo("Recursion"));
                Assert.That(method.ClassName, Is.EqualTo("ExampleClass"));
            });
            method = method.Methods[0];
            Assert.Multiple(() =>
            {
                Assert.That(method.Time, Is.AtLeast(100));
                Assert.That(method.Methods.Count, Is.EqualTo(0));
                Assert.That(method.MethodName, Is.EqualTo("Recursion"));
                Assert.That(method.ClassName, Is.EqualTo("ExampleClass"));
            });
        }
        [Test]
        public void TwoThreads_OneSimpleMethodEach()
        {
            int _currentThreadId1 = 0;
            int _currentThreadId2 = 0;
            Thread thread1 = new Thread(() =>
            {
                _obj.SimpleMethod1();
                _currentThreadId1 = Thread.CurrentThread.ManagedThreadId;
            });
            Thread thread2 = new Thread(() =>
            {
                _obj.SimpleMethod2();
                _currentThreadId2 = Thread.CurrentThread.ManagedThreadId;
            });
            thread1.Start();
            thread2.Start();

            thread1.Join();
            thread2.Join();

            TraceResult traceResult = _tracer.GetTraceResult();

            var threads = traceResult.Threads;
            // Check 1 thread
            Assert.That(threads.Count, Is.EqualTo(2));
            // Check 1 method in 1st thread
            Assert.That(threads.Keys.Contains(_currentThreadId1));
            Assert.That(threads.Keys.Contains(_currentThreadId2));
            Assert.That(threads[_currentThreadId1].Methods.Count, Is.EqualTo(1));
            Assert.That(threads[_currentThreadId2].Methods.Count, Is.EqualTo(1));

            var method = threads[_currentThreadId1].Methods[0];
            // Check thread1 information
            Assert.Multiple(() =>
            {
                Assert.That(method.Time, Is.AtLeast(200));
                Assert.That(method.Methods.Count, Is.EqualTo(0));
                Assert.That(method.MethodName, Is.EqualTo("SimpleMethod1"));
                Assert.That(method.ClassName, Is.EqualTo("ExampleClass"));
            }); 
            method = threads[_currentThreadId2].Methods[0];
            // Check thread2 information
            Assert.Multiple(() =>
            {
                Assert.That(method.Time, Is.AtLeast(300));
                Assert.That(method.Methods.Count, Is.EqualTo(0));
                Assert.That(method.MethodName, Is.EqualTo("SimpleMethod2"));
                Assert.That(method.ClassName, Is.EqualTo("ExampleClass"));
            });
        }
    }
}