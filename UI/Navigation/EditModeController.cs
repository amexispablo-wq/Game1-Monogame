using System;

namespace ColorBlocks;

public sealed class EditModeController<T>
{
    private T? _snapshot;

    public bool IsEditing { get; private set; }

    public void BeginEdit(T snapshot)
    {
        _snapshot = snapshot;
        IsEditing = true;
    }

    public void Confirm()
    {
        IsEditing = false;
        _snapshot = default;
    }

    public void Cancel(Action<T> restore)
    {
        if (IsEditing && _snapshot is not null)
        {
            restore(_snapshot);
        }

        IsEditing = false;
        _snapshot = default;
    }
}

public sealed class EditModeController
{
    public bool IsEditing { get; private set; }

    public void BeginEdit() => IsEditing = true;

    public void Confirm() => IsEditing = false;

    public void Cancel() => IsEditing = false;
}
