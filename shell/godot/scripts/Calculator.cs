using System;
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
    // Hard cap on parser recursion. A StackOverflowException cannot be caught in .NET
    // and takes the whole process down, so deeply nested / pathological input (e.g. a
    // long run of unary signs or nested parens) must be rejected as a normal, catchable
    // error before it can overflow the stack.
    private const int MaxDepth = 256;

    public static double Evaluate(string expr)
    {
        int pos = 0, depth = 0;
        double v = ParseExpr(expr, ref pos, depth);
        SkipWs(expr, ref pos);
        if (pos != expr.Length) throw new FormatException($"unexpected '{expr.Substring(pos)}'");
        if (double.IsNaN(v)) throw new FormatException("not a number (check domain)");
        if (double.IsInfinity(v)) throw new FormatException("overflow / divide by zero");
        return v;
    }

    private static void SkipWs(string s, ref int p) { while (p < s.Length && char.IsWhiteSpace(s[p])) p++; }

    private static double ParseExpr(string s, ref int p, int d)   // + -
    {
        if (++d > MaxDepth) throw new FormatException("expression too deeply nested");
        double v = ParseTerm(s, ref p, d);
        while (true)
        {
            SkipWs(s, ref p);
            if (p < s.Length && (s[p] == '+' || s[p] == '-'))
            {
                char op = s[p++];
                double r = ParseTerm(s, ref p, d);
                v = op == '+' ? v + r : v - r;
            }
            else return v;
        }
    }

    private static double ParseTerm(string s, ref int p, int d)   // * / and × ÷
    {
        if (++d > MaxDepth) throw new FormatException("expression too deeply nested");
        double v = ParseFactor(s, ref p, d);
        while (true)
        {
            SkipWs(s, ref p);
            if (p < s.Length && (s[p] == '*' || s[p] == '/' || s[p] == '×' || s[p] == '÷'))
            {
                char op = s[p++];
                double r = ParseFactor(s, ref p, d);
                v = (op == '*' || op == '×') ? v * r : v / r;
            }
            else return v;
        }
    }

    private static double ParseFactor(string s, ref int p, int d) // ^ (right-assoc)
    {
        if (++d > MaxDepth) throw new FormatException("expression too deeply nested");
        double b = ParseUnary(s, ref p, d);
        SkipWs(s, ref p);
        if (p < s.Length && s[p] == '^')
        {
            p++;
            double e = ParseFactor(s, ref p, d);
            return Math.Pow(b, e);
        }
        return b;
    }

    private static double ParseUnary(string s, ref int p, int d)
    {
        if (++d > MaxDepth) throw new FormatException("expression too deeply nested");
        SkipWs(s, ref p);
        if (p < s.Length && (s[p] == '+' || s[p] == '-'))
        {
            char op = s[p++];
            double v = ParseUnary(s, ref p, d);
            return op == '-' ? -v : v;
        }
        return ParsePrimary(s, ref p, d);
    }

    private static double ParsePrimary(string s, ref int p, int d)
    {
        if (++d > MaxDepth) throw new FormatException("expression too deeply nested");
        SkipWs(s, ref p);
        if (p >= s.Length) throw new FormatException("unexpected end");

        if (s[p] == '(')
        {
            p++;
            double v = ParseExpr(s, ref p, d);
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
                double arg = ParseExpr(s, ref p, d);
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
        // asin/acos are only defined on [-1, 1]; outside it Math.* returns NaN, which is
        // surfaced as a clean "domain" error rather than a silent NaN result.
        "asin" => Math.Abs(x) <= 1 ? Math.Asin(x) * R2D : throw new FormatException("asin domain is [-1, 1]"),
        "acos" => Math.Abs(x) <= 1 ? Math.Acos(x) * R2D : throw new FormatException("acos domain is [-1, 1]"),
        "atan" => Math.Atan(x) * R2D,
        "sqrt" => x >= 0 ? Math.Sqrt(x) : throw new FormatException("sqrt of a negative number"),
        "ln" => x > 0 ? Math.Log(x) : throw new FormatException("ln needs a positive argument"),
        "log" => x > 0 ? Math.Log10(x) : throw new FormatException("log needs a positive argument"),
        "exp" => Math.Exp(x),
        "abs" => Math.Abs(x),
        _ => throw new FormatException($"unknown function '{f}'"),
    };

}
