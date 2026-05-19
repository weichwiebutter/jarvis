using System.Text;

namespace Hermes.Runtime;

public sealed class JsonlLogger : IDisposable
{
    private readonly object _sync = new();
    private readonly StreamWriter _writer;

    public JsonlLogger(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("JSONL log directory could not be resolved.");

        Directory.CreateDirectory(directory);

        var stream = new FileStream(
            filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read);

        _writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void AppendLine(string jsonLine)
    {
        lock (_sync)
        {
            _writer.WriteLine(jsonLine);
        }
    }

    public void Flush()
    {
        lock (_sync)
        {
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _writer.Flush();
            _writer.Dispose();
        }
    }
}
