using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace SpireSolver.Engine;

public sealed class ValueNetwork : Module<Tensor, Tensor>
{
    public const int StateSize = 10;
    public const int ActionSize = 4;
    public const int InputSize = StateSize + ActionSize;

    private readonly Module<Tensor, Tensor> _network;

    public ValueNetwork() : base(nameof(ValueNetwork))
    {
        _network = Sequential(
            ("input", Linear(InputSize, 64)),
            ("relu1", ReLU()),
            ("hidden", Linear(64, 32)),
            ("relu2", ReLU()),
            ("output", Linear(32, 1))
        );
        RegisterComponents();
    }

    public override Tensor forward(Tensor input) => _network.forward(input);
}