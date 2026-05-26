using Dalamud.Interface;

namespace BazookaLens.UI;

internal static class UiBuilderVisibilityPolicy
{
    public static void Apply(IUiBuilder uiBuilder)
    {
        // Bazooka Lens is primarily controlled inside GPose, so Dalamud's automatic GPose UI hide must not suppress it.
        uiBuilder.DisableGposeUiHide = true;
    }
}
