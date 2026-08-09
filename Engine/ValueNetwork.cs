using TorchSharp;
using static TorchSharp.torch.nn;

namespace Engine;

public sealed class ValueNetwork : Module<torch.Tensor, torch.Tensor>
{
    public const int StateSize = 10;

    // Must match BasicActionEncoder.FeatureCount in the Godot/mod project.
    public const int ActionSize = 44;
    public const int InputSize = StateSize + ActionSize;

    private readonly Module<torch.Tensor, torch.Tensor> _network;

    public ValueNetwork() : base(nameof(ValueNetwork))
    {
        // The richer action representation benefits from a little more capacity
        // than the original 14-input prototype, while remaining intentionally
        // small enough to train/debug quickly.
        _network = Sequential(
            ("input", Linear(InputSize, 128)),
            ("relu1", ReLU()),
            ("hidden1", Linear(128, 64)),
            ("relu2", ReLU()),
            ("hidden2", Linear(64, 32)),
            ("relu3", ReLU()),
            ("output", Linear(32, 1))
        );
        RegisterComponents();
    }

    public override torch.Tensor forward(torch.Tensor input) => _network.forward(input);
}
