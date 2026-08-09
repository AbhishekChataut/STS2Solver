using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;

namespace SpireSolver.SpireSolverCode.Simulation.Learning.Ipc;

public sealed class EngineClient : IDisposable
{
    private const byte CmdInfer = 0;
    private const byte CmdTrainBatch = 1;

    private readonly NamedPipeClientStream _pipe;
    private readonly object _lock = new();

    public EngineClient(string pipeName)
    {
        _pipe = new NamedPipeClientStream(
            ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    }

    public void Connect(int timeoutMs = 5000) => _pipe.Connect(timeoutMs);
    public bool IsConnected => _pipe.IsConnected;

    public float Infer(float[] stateAndAction)
    {
        lock (_lock)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(CmdInfer);
            foreach (float f in stateAndAction) writer.Write(f);
            writer.Flush();

            PipeFramer.WriteFrame(_pipe, ms.ToArray());
            byte[] response = PipeFramer.ReadFrame(_pipe);
            return BitConverter.ToSingle(response, 0);
        }
    }

    public void TrainBatch(IReadOnlyList<Experience> experiences)
    {
        if (experiences.Count == 0) return;

        lock (_lock)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write(CmdTrainBatch);
            writer.Write(experiences.Count);

            foreach (Experience exp in experiences)
            {
                foreach (float f in exp.State) writer.Write(f);
                foreach (float f in exp.Action) writer.Write(f);
                writer.Write(exp.Reward);
            }
            writer.Flush();

            PipeFramer.WriteFrame(_pipe, ms.ToArray());
            byte[] ack = PipeFramer.ReadFrame(_pipe);
            float loss = BitConverter.ToSingle(ack, 0);
            Godot.GD.Print($"[SpireSolver] Trained on {experiences.Count}, loss={loss:F4}");
        }
    }

    public void Dispose() => _pipe.Dispose();
}