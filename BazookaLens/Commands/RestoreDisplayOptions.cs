namespace BazookaLens.Commands;

internal sealed record RestoreDisplayOptions(bool ForceWindowRefresh)
{
    public override string ToString()
    {
        return this.ForceWindowRefresh ? "ForceWindowRefresh=True" : "ForceWindowRefresh=False";
    }
}
