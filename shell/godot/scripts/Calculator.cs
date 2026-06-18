using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace FiringSolution.Shell;

/// <summary>
/// The scientific calculator (design §4): it does the ARITHMETIC, the player does
/// the PHYSICS. A real recursive-descent evaluator — + − × ÷, powers, parentheses,
/// and the everyday maths functions — with degree-mode trig to match the game's
/// angle convention. Deliberately holds no domain knowledge: it never knows what a
/// trajectory is, so it can't hand over a solution.
/// </summary>
public static class Calculator
{
    // ---- evaluator ---------------------------------------------------------
    public static double Evaluate(string expr)
    {
        int pos = 0;
        double v = ParseExpr(expr, ref pos);
        SkipWs(expr, ref pos);
        if (pos != expr.Length) throw new FormatException($"unexpected '{expr.Substring(pos)}'");
        return v;
    }

    private static void SkipWs(string s, ref int p) { while (p < s.Length && char.IsWhiteSpace(s[p])) p++; }

    private static double ParseExpr(string s, ref int p)   // + -
    {
        double v = ParseTerm(s, ref p);
        while (true)
        {
            SkipWs(s, ref p);
            if (p < s.Length && (s[p] == '+' || s[p] == '-'))
            {
                char op = s[p++];
                double r = ParseTerm(s, ref p);
                v = op == '+' ? v + r : v - r;
            }
            else return v;
        }
    }

    private static double ParseTerm(string s, ref int p)   // * / and × ÷
    {
        double v = ParseFactor(s, ref p);
        while (true)
        {
            SkipWs(s, ref p);
            if (p < s.Length && (s[p] == '*' || s[p] == '/' || s[p] == '×' || s[p] == '÷'))
            {
                char op = s[p++];
                double r = ParseFactor(s, ref p);
                v = (op == '*' || op == '×') ? v * r : v / r;
            }
            else return v;
        }
    }

    private static double ParseFactor(string s, ref int p) // ^ (right-assoc)
    {
        double b = ParseUnary(s, ref p);
        SkipWs(s, ref p);
        if (p < s.Length && s[p] == '^')
        {
            p++;
            double e = ParseFactor(s, ref p);
            return Math.Pow(b, e);
        }
        return b;
    }

    private static double ParseUnary(string s, ref int p)
    {
        SkipWs(s, ref p);
        if (p < s.Length && (s[p] == '+' || s[p] == '-'))
        {
            char op = s[p++];
            double v = ParseUnary(s, ref p);
            return op == '-' ? -v : v;
        }
        return ParsePrimary(s, ref p);
    }

    private static double ParsePrimary(string s, ref int p)
    {
        SkipWs(s, ref p);
        if (p >= s.Length) throw new FormatException("unexpected end");

        if (s[p] == '(')
        {
            p++;
            double v = ParseExpr(s, ref p);
            SkipWs(s, ref p);
            if (p >= s.Length || s[p] != ')') throw new FormatException("missing ')'");
            p++;
            return v;
        }

        if (char.IsLetter(s[p]))
        {
            int start = p;
            while (p < s.Length && char.IsLetter(s[p])) p++;
            string id = s.Substring(start, p - start).ToLowerInvariant();
            SkipWs(s, ref p);
            if (p < s.Length && s[p] == '(')           // function call
            {
                p++;
                double arg = ParseExpr(s, ref p);
                SkipWs(s, ref p);
                if (p >= s.Length || s[p] != ')') throw new FormatException("missing ')'");
                p++;
                return ApplyFunc(id, arg);
            }
            return id switch                            // constant
            {
                "pi" => Math.PI,
                "e" => Math.E,
                _ => throw new FormatException($"unknown name '{id}'"),
            };
        }

        // number
        int ns = p;
        while (p < s.Length && (char.IsDigit(s[p]) || s[p] == '.' || s[p] == 'e' || s[p] == 'E'
               || ((s[p] == '+' || s[p] == '-') && p > ns && (s[p - 1] == 'e' || s[p - 1] == 'E'))))
            p++;
        if (p == ns) throw new FormatException($"unexpected '{s[p]}'");
        return double.Parse(s.Substring(ns, p - ns), CultureInfo.InvariantCulture);
    }

    private const double D2R = Math.PI / 180.0, R2D = 180.0 / Math.PI;

    private static double ApplyFunc(string f, double x) => f switch
    {
        "sin" => Math.Sin(x * D2R),                     // trig in DEGREES (game convention)
        "cos" => Math.Cos(x * D2R),
        "tan" => Math.Tan(x * D2R),
        "asin" => Math.Asin(x) * R2D,
        "acos" => Math.Acos(x) * R2D,
        "atan" => Math.Atan(x) * R2D,
        "sqrt" => Math.Sqrt(x),
        "ln" => Math.Log(x),
        "log" => Math.Log10(x),
        "exp" => Math.Exp(x),
        "abs" => Math.Abs(x),
        _ => throw new FormatException($"unknown function '{f}'"),
    };

    // ---- UI widget ---------------------------------------------------------

    /// <summary>Build the calculator panel: input line, history, degree-mode trig.</summary>
    public static Control Build(Palette p)
    {
        var calc = Ui.Panel(p.PanelDeep, p.Border, pad: 0, borderW: 1);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 0);

        var head = Ui.Panel(p.Bg, p.BorderSoft, pad: 9, borderW: 0);
        var hr = new HBoxContainer();
        hr.AddChild(Ui.Text("SCIENTIFIC CALCULATOR", p.TextDim, 10));
        hr.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        hr.AddChild(Ui.Text("ARITHMETIC ONLY · deg", p.Faint, 8));
        head.AddChild(hr);
        col.AddChild(head);

        var body = new MarginContainer();
        body.AddThemeConstantOverride("margin_left", 11);
        body.AddThemeConstantOverride("margin_right", 11);
        body.AddThemeConstantOverride("margin_top", 9);
        body.AddThemeConstantOverride("margin_bottom", 9);
        var bv = new VBoxContainer();
        bv.AddThemeConstantOverride("separation", 6);
        body.AddChild(bv);
        col.AddChild(body);

        var history = new VBoxContainer();
        history.AddThemeConstantOverride("separation", 2);
        bv.AddChild(history);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        var input = new LineEdit
        {
            PlaceholderText = "e.g.  570^2 * sin(2*38) / 9.817",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        input.AddThemeColorOverride("font_color", p.Text);
        input.AddThemeFontSizeOverride("font_size", 13);
        var ib = new StyleBoxFlat { BgColor = p.Bg, BorderColor = p.Border };
        ib.SetBorderWidthAll(1);
        ib.ContentMarginLeft = 8; ib.ContentMarginRight = 8;
        ib.ContentMarginTop = 6; ib.ContentMarginBottom = 6;
        input.AddThemeStyleboxOverride("normal", ib);
        input.AddThemeStyleboxOverride("focus", ib);
        var eq = Ui.FlatButton(p, "=", p.Accent, p.Border, 13);
        eq.CustomMinimumSize = new Vector2(38, 0);
        row.AddChild(input);
        row.AddChild(eq);
        bv.AddChild(row);

        void Eval()
        {
            string expr = input.Text.Trim();
            if (expr.Length == 0) return;
            string line;
            try { line = $"{expr} = {Calculator.Evaluate(expr):0.######}"; }
            catch (Exception e) { line = $"{expr} = ERR ({e.Message})"; }

            var lbl = Ui.Text(line, p.TextDim, 11);
            history.AddChild(lbl);
            while (history.GetChildCount() > 4) history.GetChild(0).QueueFree();
            input.Clear();
            input.GrabFocus();
        }
        eq.Pressed += Eval;
        input.TextSubmitted += _ => Eval();

        calc.AddChild(col);
        return calc;
    }
}
