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
        string FileName;
        public FileWriter(string filename)
        {
            FileName = filename;
        }
        public void Write(string text)
        {
            File.WriteAllText(FileName, text);
        }
    }
}
