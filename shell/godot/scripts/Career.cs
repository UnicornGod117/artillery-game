// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using Godot;

namespace FiringSolution.Shell;

/// <summary>
/// The persisted points / career score (design §9, §12). A single running total,
/// shared across both stations and saved to the Godot user data directory so it
/// survives station switches, new missions, and app restarts.
/// </summary>
public static class Career
{
    private const string Path = "user://career.save";
    private static int _points = -1;

    public static int Points
    {
        get { if (_points < 0) Load(); return _points; }
        private set { _points = value; }
    }

    public static void Add(int delta)
    {
        Points = Points + delta;
        Save();
    }

    private static void Load()
    {
        _points = 12840; // default starting balance for a fresh career
        if (!FileAccess.FileExists(Path)) return;
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        if (f is null) return;
        if (int.TryParse(f.GetAsText().Trim(), out int v)) _points = v;
    }

    private static void Save()
    {
        using var f = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        f?.StoreString(_points.ToString());
    }
}
