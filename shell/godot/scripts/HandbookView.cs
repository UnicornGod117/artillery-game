using Godot;
using FiringSolution.Core.Content;

namespace FiringSolution.Shell;

/// <summary>
/// The handbook overlay (design §4): a complete reference of every formula the game
/// can demand, plus a maths cheat-sheet — a knowledge dump, never a worked solution.
/// Content comes straight from the Core's <see cref="Handbook"/> data so the shell
/// authors nothing of its own.
/// </summary>
public partial class HandbookView : Control
{
    public static void Open(Node parent, Palette p, string tierHint)
        => parent.AddChild(new HandbookView { _p = p, _hint = tierHint });

    private Palette _p = Palette.Amber;
    private string _hint = "";

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        // Dim backdrop — clicking it closes the overlay.
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.62f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) QueueFree(); };
        AddChild(dim);

        var panel = Ui.Panel(_p.Panel, _p.AccentDim, pad: 0, borderW: 1);
        panel.CustomMinimumSize = new Vector2(720, 660);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
        AddChild(panel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 0);
        panel.AddChild(col);

        // Header.
        var head = Ui.Panel(_p.PanelDeep, _p.BorderSoft, pad: 14, borderW: 0);
        var hr = new HBoxContainer();
        hr.AddChild(new ColorRect { Color = _p.Accent, CustomMinimumSize = new Vector2(6, 6), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var ht = new MarginContainer(); ht.AddThemeConstantOverride("margin_left", 8);
        ht.AddChild(Ui.Text("▤ HANDBOOK — FORMULA REFERENCE", _p.Text, 14, true));
        hr.AddChild(ht);
        hr.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = Ui.FlatButton(_p, "CLOSE  ✕", _p.TextDim, _p.Border, 10);
        close.Pressed += QueueFree;
        hr.AddChild(close);
        head.AddChild(hr);
        col.AddChild(head);

        // Tier hint banner (which equations apply — never the answer).
        var hintBox = Ui.Panel(_p.Bg, _p.BorderSoft, pad: 11, borderW: 0);
        var hb = new VBoxContainer();
        hb.AddThemeConstantOverride("separation", 2);
        hb.AddChild(Ui.Text("APPLIES TO THIS MISSION", _p.Faint, 8));
        var hl = Ui.Text(_hint, _p.AccentDim, 11);
        hl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        hl.CustomMinimumSize = new Vector2(690, 0);
        hb.AddChild(hl);
        hintBox.AddChild(hb);
        col.AddChild(hintBox);

        // Scrollable sections.
        var scroll = new ScrollContainer();
        scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
        scroll.CustomMinimumSize = new Vector2(0, 520);
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 4);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var listMargin = new MarginContainer();
        listMargin.AddThemeConstantOverride("margin_left", 14);
        listMargin.AddThemeConstantOverride("margin_right", 14);
        listMargin.AddThemeConstantOverride("margin_top", 10);
        listMargin.AddThemeConstantOverride("margin_bottom", 12);
        listMargin.AddChild(list);
        scroll.AddChild(listMargin);
        col.AddChild(scroll);

        foreach (var section in Handbook.Sections)
        {
            list.AddChild(Ui.SectionHeader(_p, section.Title, _p.Accent));
            foreach (var entry in section.Entries)
            {
                var box = Ui.Panel(_p.PanelDeep, _p.BorderSoft, pad: 9, borderW: 1);
                var er = new HBoxContainer();
                er.AddThemeConstantOverride("separation", 12);
                var name = Ui.Text(entry.Name, _p.TextDim, 11);
                name.CustomMinimumSize = new Vector2(230, 0);
                er.AddChild(name);
                er.AddChild(Ui.Text(entry.Formula, _p.Text, 13));
                box.AddChild(er);
                list.AddChild(box);
            }
            list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }
    }
}
