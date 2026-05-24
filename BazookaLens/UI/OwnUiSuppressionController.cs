using Dalamud.Interface.Windowing;

namespace BazookaLens.UI;

internal sealed class OwnUiSuppressionController
{
    private readonly List<Window> windows = [];
    private readonly object gate = new();
    private SuppressionScope? activeScope;

    public bool IsSuppressed
    {
        get
        {
            lock (this.gate)
                return this.activeScope is not null;
        }
    }

    public void Register(Window window)
    {
        lock (this.gate)
        {
            if (!this.windows.Contains(window))
                this.windows.Add(window);
        }
    }

    public IDisposable Suppress()
    {
        lock (this.gate)
        {
            if (this.activeScope is not null)
                return this.activeScope.EnterNested();

            this.activeScope = new SuppressionScope(this, this.windows.Select(window => new WindowSnapshot(window, window.IsOpen)).ToArray());
            foreach (var snapshot in this.activeScope.Snapshots)
                snapshot.Window.IsOpen = false;

            return this.activeScope.EnterNested();
        }
    }

    private void Release(SuppressionScope scope)
    {
        lock (this.gate)
        {
            if (!ReferenceEquals(this.activeScope, scope) || !scope.TryRelease())
                return;

            foreach (var snapshot in scope.Snapshots)
                snapshot.Window.IsOpen = snapshot.WasOpen;

            this.activeScope = null;
        }
    }

    private sealed record WindowSnapshot(Window Window, bool WasOpen);

    private sealed class SuppressionScope
    {
        private readonly OwnUiSuppressionController owner;
        private int references;

        public SuppressionScope(OwnUiSuppressionController owner, WindowSnapshot[] snapshots)
        {
            this.owner = owner;
            this.Snapshots = snapshots;
        }

        public WindowSnapshot[] Snapshots { get; }

        public IDisposable EnterNested()
        {
            this.references++;
            return new Lease(this.owner, this);
        }

        public bool TryRelease()
        {
            this.references--;
            return this.references <= 0;
        }
    }

    private sealed class Lease : IDisposable
    {
        private readonly OwnUiSuppressionController owner;
        private readonly SuppressionScope scope;
        private bool disposed;

        public Lease(OwnUiSuppressionController owner, SuppressionScope scope)
        {
            this.owner = owner;
            this.scope = scope;
        }

        public void Dispose()
        {
            if (this.disposed)
                return;

            this.owner.Release(this.scope);
            this.disposed = true;
        }
    }
}
