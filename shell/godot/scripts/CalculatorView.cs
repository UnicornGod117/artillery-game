using Godot;
using System;

namespace FiringSolution.Shell;

/// <summary>
/// The scientific calculator as a pop-up keypad (design §4): a real button panel —
/// digits, operators, parentheses and the everyday functions — with a display you
/// can also type into. It does the ARITHMETIC; it holds no physics, so it can never
/// predict a trajectory or hand over a firing solution. Degree-mode trig matches the
/// game's angle convention. Evaluation is the same engine as <see cref="Calculator"/>.
/// </summary>
public partial class CalculatorView : Control
{
    public static void Open(Node parent, Palette p)
        => parent.AddChild(new CalculatorView { _p = p });

    private Palette _p = Palette.Amber;
    private LineEdit _input = null!;
    private Label _result = null!;
    private VBoxContainer _history = null!;
    private string _ans = "0";

    private enum Act { Insert, Func, Clear, Back, Equals, Ans }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.62f) };
        dim.SetAnchorsPreset(LayoutPreset.FullRect);
        dim.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) QueueFree(); };
        AddChild(dim);

        var panel = Ui.Panel(_p.Panel, _p.AccentDim, pad: 0, borderW: 1);
        panel.CustomMinimumSize = new Vector2(430, 560);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.KeepSize);
        AddChild(panel);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 0);
        panel.AddChild(col);

        // Header.
        var head = Ui.Panel(_p.PanelDeep, _p.BorderSoft, pad: 12, borderW: 0);
        var hr = new HBoxContainer();
        hr.AddChild(Ui.Text("▦  SCIENTIFIC CALCULATOR", _p.Text, 13, true));
        hr.AddChild(Ui.Text("ARITHMETIC ONLY · deg", _p.Faint, 8));
        hr.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = Ui.FlatButton(_p, "CLOSE  ✕", _p.TextDim, _p.Border, 10);
        close.Pressed += QueueFree;
        hr.AddChild(close);
        head.AddChild(hr);
        col.AddChild(head);

        var body = new MarginContainer();
        foreach (var (k, val) in new[] { ("margin_left", 14), ("margin_right", 14), ("margin_top", 12), ("margin_bottom", 14) })
            body.AddThemeConstantOverride(k, val);
        var bv = new VBoxContainer();
        bv.AddThemeConstantOverride("separation", 9);
        body.AddChild(bv);
        col.AddChild(body);

        // Display: small history, an editable expression line, and the running result.
        var display = Ui.Panel(_p.PanelDeep, _p.Border, pad: 10, borderW: 1);
        var dv = new VBoxContainer();
        dv.AddThemeConstantOverride("separation", 4);
        _history = new VBoxContainer();
        _history.AddThemeConstantOverride("separation", 1);
        dv.AddChild(_history);
        _input = new LineEdit { PlaceholderText = "0", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _input.AddThemeColorOverride("font_color", _p.Text);
        _input.AddThemeFontSizeOverride("font_size", 20);
        var ib = new StyleBoxFlat { BgColor = _p.Bg, BorderColor = _p.Border };
        ib.SetBorderWidthAll(1); ib.ContentMarginLeft = 8; ib.ContentMarginRight = 8;
        ib.ContentMarginTop = 6; ib.ContentMarginBottom = 6;
        _input.AddThemeStyleboxOverride("normal", ib);
        _input.AddThemeStyleboxOverride("focus", ib);
        _input.TextSubmitted += _ => Equals();
        dv.AddChild(_input);
        _result = Ui.Text("= 0", _p.Accent, 15, true);
        _result.HorizontalAlignment = HorizontalAlignment.Right;
        dv.AddChild(_result);
        display.AddChild(dv);
        bv.AddChild(display);

        // Keypad.
        var grid = new GridContainer { Columns = 5 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bv.AddChild(grid);

        // (label, token, action, colour-role) — role: 0 dim/func, 1 digit, 2 operator, 3 special
        var keys = new (string label, string token, Act act, int role)[]
        {
            ("sin","sin(",Act.Func,0), ("cos","cos(",Act.Func,0), ("tan","tan(",Act.Func,0), ("(","(",Act.Insert,0), (")",")",Act.Insert,0),
            ("asin","asin(",Act.Func,0), ("acos","acos(",Act.Func,0), ("atan","atan(",Act.Func,0), ("xʸ","^",Act.Insert,0), ("√","sqrt(",Act.Func,0),
            ("ln","ln(",Act.Func,0), ("log","log(",Act.Func,0), ("eˣ","exp(",Act.Func,0), ("π","pi",Act.Insert,0), ("e","e",Act.Insert,0),
            ("7","7",Act.Insert,1), ("8","8",Act.Insert,1), ("9","9",Act.Insert,1), ("÷","/",Act.Insert,2), ("×","*",Act.Insert,2),
            ("4","4",Act.Insert,1), ("5","5",Act.Insert,1), ("6","6",Act.Insert,1), ("+","+",Act.Insert,2), ("−","-",Act.Insert,2),
            ("1","1",Act.Insert,1), ("2","2",Act.Insert,1), ("3","3",Act.Insert,1), (".",".",Act.Insert,1), ("Ans","",Act.Ans,0),
            ("0","0",Act.Insert,1), ("00","00",Act.Insert,1), ("C","",Act.Clear,3), ("⌫","",Act.Back,3), ("=","",Act.Equals,2),
        };
        foreach (var k in keys)
            grid.AddChild(MakeKey(k.label, k.token, k.act, k.role));
    }

    private Button MakeKey(string label, string token, Act act, int role)
    {
        Color fg = role switch { 1 => _p.Text, 2 => _p.Accent, 3 => _p.Red, _ => _p.AccentDim };
        var b = Ui.FlatButton(_p, label, fg, _p.Border, role == 1 ? 16 : 13);
        b.CustomMinimumSize = new Vector2(0, 46);
        b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        b.Pressed += () => Apply(token, act);
        return b;
    }

    private void Apply(string token, Act act)
    {
        switch (act)
        {
            case Act.Insert:
            case Act.Func:
                _input.Text += token;
                break;
            case Act.Ans:
                _input.Text += _ans;
                break;
            case Act.Clear:
                _input.Clear();
                _result.Text = "= 0";
                break;
            case Act.Back:
                if (_input.Text.Length > 0) _input.Text = _input.Text[..^1];
                break;
            case Act.Equals:
                Equals();
                break;
        }
        _input.GrabFocus();
        _input.CaretColumn = _input.Text.Length;
    }

    private void Equals()
    {
        string expr = _input.Text.Trim();
        if (expr.Length == 0) return;
        try
        {
            double v = Calculator.Evaluate(expr);
            _ans = v.ToString("0.##########");
            _result.Text = "= " + _ans;
            AddHistory($"{expr} = {_ans}", _p.TextDim);
        }
        catch (Exception e)
        {
            _result.Text = "= ERR";
            AddHistory($"{expr}  — {e.Message}", _p.Red);
        }
    }

    private void AddHistory(string line, Color col)
    {
        var l = Ui.Text(line, col, 10);
        l.HorizontalAlignment = HorizontalAlignment.Right;
        _history.AddChild(l);
        while (_history.GetChildCount() > 3) _history.GetChild(0).QueueFree();
    }
}
