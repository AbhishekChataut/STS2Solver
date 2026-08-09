using System;
using System.IO;
using System.IO.Pipes;
using Engine;
using Engine;
using TorchSharp;
using static TorchSharp.torch;

const byte CmdInfer = 0;
const byte CmdTrainBatch = 1;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: Engine <pipeName>");
    return 1;
}

string pipeName = args[0];
var net = new ValueNetwork();
var optimizer = torch.optim.Adam(net.parameters(), lr: 1e-3);

Console.WriteLine($"[Engine] Starting, pipe '{pipeName}'");

using var server = new NamedPipeServerStream(
    pipeName, PipeDirection.InOut, 1,
    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

Console.WriteLine("[Engine] Waiting for connection...");
server.WaitForConnection();
Console.WriteLine("[Engine] Connected.");

try
{
    while (server.IsConnected)
    {
        byte[] frame = PipeFramer.ReadFrame(server);
        using var reader = new BinaryReader(new MemoryStream(frame));
        byte cmd = reader.ReadByte();

        switch (cmd)
        {
            case CmdInfer:
            {
                var input = new float[ValueNetwork.InputSize];
                for (int i = 0; i < input.Length; i++)
                    input[i] = reader.ReadSingle();

                net.eval();
                using var x = torch.tensor(input, dtype: ScalarType.Float32)
                                    .reshape(1, ValueNetwork.InputSize);
                using var y = net.forward(x);
                float value = y.item<float>();

                /*
                Console.WriteLine($"[Engine] Infer -> {value:F4}");
                */
                PipeFramer.WriteFrame(server, BitConverter.GetBytes(value));
                break;
            }

            case CmdTrainBatch:
            {
                int count = reader.ReadInt32();
                var inputs = new float[count, ValueNetwork.InputSize];
                var targets = new float[count];

                for (int i = 0; i < count; i++)
                {
                    for (int j = 0; j < ValueNetwork.InputSize; j++)
                        inputs[i, j] = reader.ReadSingle();
                    targets[i] = reader.ReadSingle();
                }

                net.train();
                using var x = torch.tensor(inputs, dtype: ScalarType.Float32);
                using var y = torch.tensor(targets, dtype: ScalarType.Float32)
                                    .reshape(count, 1);
                using var pred = net.forward(x);
                using var loss = torch.nn.functional.mse_loss(pred, y);

                optimizer.zero_grad();
                loss.backward();
                optimizer.step();

                float lossValue = loss.item<float>();
                Console.WriteLine($"[Engine] Trained on {count}, loss={lossValue:F4}");
                PipeFramer.WriteFrame(server, BitConverter.GetBytes(lossValue));
                break;
            }

            default:
                Console.WriteLine($"[Engine] Unknown command {cmd}");
                break;
        }
    }
}
catch (EndOfStreamException) { }

Console.WriteLine("[Engine] Client disconnected, exiting.");
return 0;