namespace Tracer.Example
{
    interface IWriter
    {
        void Write(string text);
    }
    public class ConsoleWriter : IWriter
    {
        public void Write(string text)
        {
            Console.WriteLine(text);
        }
    }
    public class FileWriter : IWriter
    {
        private string _fileName;
        public FileWriter(string filename)
        {
            _fileName = filename;
        }
        public void Write(string text)
        {
            File.WriteAllText(_fileName, text);
        }
    }
}
