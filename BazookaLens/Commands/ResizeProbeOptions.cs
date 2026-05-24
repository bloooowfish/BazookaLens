namespace BazookaLens.Commands;

internal sealed record ResizeProbeOptions(double Scale, ResizeProbeRoute Route)
{
    public override string ToString()
    {
        return $"Scale={this.Scale:0.###}, Route={this.Route}";
    }
}
