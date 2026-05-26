using System.Reflection;
using BazookaLens.UI;
using Dalamud.Interface;

namespace BazookaLens.Tests;

public sealed class UiBuilderVisibilityPolicyTests
{
    [Fact]
    public void ApplyDisablesDalamudGposeUiHide()
    {
        var uiBuilder = UiBuilderSpy.Create(out var spy);

        UiBuilderVisibilityPolicy.Apply(uiBuilder);

        Assert.True(spy.DisableGposeUiHide);
    }

    private class UiBuilderSpy : DispatchProxy
    {
        public bool DisableGposeUiHide { get; private set; }

        public static IUiBuilder Create(out UiBuilderSpy spy)
        {
            var uiBuilder = Create<IUiBuilder, UiBuilderSpy>();
            spy = (UiBuilderSpy)(object)uiBuilder;
            return uiBuilder;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "set_DisableGposeUiHide")
            {
                this.DisableGposeUiHide = args is [{ } value] && (bool)value;
                return null;
            }

            var returnType = targetMethod?.ReturnType;
            if (returnType is null || returnType == typeof(void))
            {
                return null;
            }

            return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
        }
    }
}
