using FFXIVClientStructs.FFXIV.Client.UI;

namespace BazookaLens.UI;

internal sealed class UiVisibilityController
{
    private bool? pendingRestoreVisible;

    public unsafe bool TryBeginHideGameUi(out string? error)
    {
        error = null;
        if (this.pendingRestoreVisible.HasValue)
        {
            error = "A game UI visibility transaction is already active.";
            return false;
        }

        var atk = RaptureAtkModule.Instance();
        if (atk is null)
        {
            error = "RaptureAtkModule is not available.";
            return false;
        }

        this.pendingRestoreVisible = atk->IsUiVisible;
        PluginServices.Log.Information("Game UI hide transaction started: PreviousVisible={PreviousVisible}", this.pendingRestoreVisible);

        if (atk->IsUiVisible)
            atk->IsUiVisible = false;

        PluginServices.Log.Information("Game UI hidden for capture.");
        return true;
    }

    public unsafe bool TryRestoreGameUi(out string? error)
    {
        error = null;
        if (this.pendingRestoreVisible is not bool visible)
        {
            PluginServices.Log.Debug("Game UI restore requested with no pending visibility transaction; leaving UI unchanged.");
            return true;
        }

        var atk = RaptureAtkModule.Instance();
        if (atk is null)
        {
            error = "RaptureAtkModule is not available.";
            return false;
        }

        atk->IsUiVisible = visible;
        this.pendingRestoreVisible = null;

        PluginServices.Log.Information("Game UI restored after capture: RestoredVisible={RestoredVisible}", visible);
        return true;
    }
}
