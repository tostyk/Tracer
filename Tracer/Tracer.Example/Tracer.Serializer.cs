using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using System.Xml.Serialization;
using Tracer.Core;

namespace Tracer.Example
{
    public interface ITracerSerializer
    {
        string Serialize(TraceResult traceResult);
    }

    [Serializable]
    public class SerializableMethodTrace
    {
        [XmlAttribute(AttributeName = "name")]
        [JsonInclude]
        [JsonPropertyName("name")]
        public string MethodName;
        [XmlAttribute(AttributeName = "class")]
        [JsonInclude]
        [JsonPropertyName("class")]
        public string ClassName;
        [XmlAttribute(AttributeName = "time")]
        [JsonInclude]
        [JsonPropertyName("time")]
        public long Time;
        [XmlElement(ElementName = "method")]
        [JsonInclude]
        [JsonPropertyName("methods")]
        public List<SerializableMethodTrace> MethodList;
        public SerializableMethodTrace(string methodName, string className, long time, List<SerializableMethodTrace> methodList)
        {
            MethodName = methodName;
            ClassName = className;
            Time = time;
            MethodList = methodList;
        }
        public SerializableMethodTrace()
        {
            MethodList = new List<SerializableMethodTrace>();
            MethodName = "";
            ClassName = "";
        }
    }
    [Serializable]
    public class SerializableThreadTrace
    {
        [XmlAttribute(AttributeName = "id")]
        [JsonInclude]
        [JsonPropertyName("id")]
        public int ID;
        [XmlAttribute(AttributeName = "time")]
        [JsonInclude]
        [JsonPropertyName("time")]
        public long Time;
        [XmlElement(ElementName = "method")]
        [JsonInclude]
        [JsonPropertyName("methods")]
        public List<SerializableMethodTrace> MethodList;
        public SerializableThreadTrace(int id, long time, List<SerializableMethodTrace> methodList)
        {
            ID = id;
            Time = time;
            MethodList = methodList;
        }
        public SerializableThreadTrace()
        {
            MethodList = new List<SerializableMethodTrace>();
        }
    }
    [XmlRoot(ElementName = "root")]
    public class SerializableTraceResult
    {
        [XmlElement(ElementName = "thread")]
        [JsonInclude]
        [JsonPropertyName("threads")]
        public List<SerializableThreadTrace> ThreadList;
        public SerializableTraceResult()
        {
            ThreadList = new List<SerializableThreadTrace>();
        }
        public static List<SerializableMethodTrace> ConvertToSerializable(ReadOnlyCollection<MethodTrace> methodsTrace)
        {
            List<SerializableMethodTrace> serializableMethodTraces = new List<SerializableMethodTrace>();
            foreach (MethodTrace methodTrace in methodsTrace)
            {
                if (methodTrace.Methods.Count > 0)
                {
                    serializableMethodTraces.Add(new SerializableMethodTrace(
                        methodTrace.MethodName,
                        methodTrace.ClassName,
                        methodTrace.Time,
                        ConvertToSerializable(methodTrace.Methods)));
                }
                else
                {
                    serializableMethodTraces.Add(new SerializableMethodTrace(
                        methodTrace.MethodName,
                        methodTrace.ClassName,
                        methodTrace.Time,
                        new List<SerializableMethodTrace>()));
                }
            }
            return serializableMethodTraces;
        }
        public static SerializableTraceResult ConvertToSerializable(TraceResult traceResult)
        {
            SerializableTraceResult serializableTraceResult = new SerializableTraceResult();
            foreach (KeyValuePair<int, ThreadTrace> threadTrace in traceResult.Threads)
            {
                serializableTraceResult.ThreadList.Add(
                    new SerializableThreadTrace(
                        threadTrace.Key,
                        threadTrace.Value.Time,
                        ConvertToSerializable(threadTrace.Value.Methods)));
            }
            return serializableTraceResult;
        }
    }
    
    public class XmlTracerSerializer : ITracerSerializer
    {
        public string Serialize(TraceResult traceResult)
        {
            SerializableTraceResult serializableTraceResult = SerializableTraceResult.ConvertToSerializable(traceResult);
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(SerializableTraceResult));
            StringWriter writer = new StringWriter();
            xmlSerializer.Serialize(writer, serializableTraceResult);
            writer.Close();
            return XElement.Parse(writer.ToString()).ToString();
        }
    }

    public class JsonTracerSerializer : ITracerSerializer
    {
        public string Serialize(TraceResult traceResult)
        {
            SerializableTraceResult serializableTraceResult = SerializableTraceResult.ConvertToSerializable(traceResult);
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true};
            return JsonSerializer.Serialize(serializableTraceResult, jsonSerializerOptions);
        }
    }
}
