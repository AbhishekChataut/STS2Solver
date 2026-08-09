using System.IO;

namespace SpireSolver.SpireSolverCode.Simulation.Learning.Ipc;

public static class PipeFramer
{
    public static void WriteFrame(Stream stream, byte[] payload)
    {
        var writer = new BinaryWriter(stream);
        writer.Write(payload.Length);
        writer.Write(payload);
        writer.Flush();
    }

    public static byte[] ReadFrame(Stream stream)
    {
        var reader = new BinaryReader(stream);
        int length = reader.ReadInt32();
        return reader.ReadBytes(length);
    }
}