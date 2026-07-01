// Copyright (c) 2026 Unicorn God. Licensed under the MIT License.
using System;
using FiringSolution.Shell;
using Xunit;

namespace FiringSolution.Core.Tests;

/// <summary>
/// The scientific calculator does the player's arithmetic, so a parser bug feeds
/// wrong numbers into a hand-derived firing solution and the miss looks like the
/// player's fault. These tests pin the evaluator: operator precedence and
/// associativity, the degree-mode trig that matches the game's angle convention,
/// domain-error reporting, and the recursion guard that keeps pathological input
/// from taking down the process.
/// </summary>
public class CalculatorTests
{
    [Theory]
    [InlineData("1+2*3", 7)]            // × binds tighter than +
    [InlineData("(1+2)*3", 9)]          // parentheses override precedence
    [InlineData("2+3-4", 1)]            // +/- left-assoc
    [InlineData("20/4/5", 1)]           // / left-assoc: (20/4)/5
    [InlineData("2*3+4*5", 26)]
    [InlineData("-3+5", 2)]             // leading unary minus
    [InlineData("3 + 4", 7)]            // whitespace is ignored
    [InlineData("2×3÷6", 1)]            // unicode × ÷ are honoured
    public void Arithmetic_RespectsPrecedence(string expr, double expected)
        => Assert.Equal(expected, Calculator.Evaluate(expr), 9);

    [Theory]
    [InlineData("2^3", 8)]
    [InlineData("2^3^2", 512)]          // ^ is right-assoc: 2^(3^2) = 2^9
    [InlineData("-2^2", 4)]             // the sign is part of the base: (-2)^2
    public void Power_IsRightAssociative(string expr, double expected)
        => Assert.Equal(expected, Calculator.Evaluate(expr), 9);

    [Theory]
    [InlineData("sin(30)", 0.5)]        // trig is in DEGREES (game convention)
    [InlineData("cos(60)", 0.5)]
    [InlineData("tan(45)", 1.0)]
    [InlineData("asin(0.5)", 30.0)]     // inverse trig returns degrees
    [InlineData("atan(1)", 45.0)]
    public void Trig_IsInDegrees(string expr, double expected)
        => Assert.Equal(expected, Calculator.Evaluate(expr), 6);

    [Theory]
    [InlineData("sqrt(144)", 12)]
    [InlineData("ln(e)", 1)]
    [InlineData("log(1000)", 3)]
    [InlineData("exp(0)", 1)]
    [InlineData("abs(-7)", 7)]
    public void Functions_Evaluate(string expr, double expected)
        => Assert.Equal(expected, Calculator.Evaluate(expr), 9);

    [Theory]
    [InlineData("pi", Math.PI)]
    [InlineData("e", Math.E)]
    [InlineData("2*pi", 2 * Math.PI)]
    public void Constants_AreKnown(string expr, double expected)
        => Assert.Equal(expected, Calculator.Evaluate(expr), 9);

    [Fact]
    public void ScientificNotation_Parses()
        => Assert.Equal(3e8, Calculator.Evaluate("3e8"), 0);

    // A representative firing-solution snippet: time of flight for a vacuum lob,
    // t = 2 v sin(θ) / g. The calculator must chain trig, division and grouping
    // exactly the way the player would type it.
    [Fact]
    public void RealisticFiringExpression_IsCorrect()
    {
        double expected = 2 * 800 * Math.Sin(40 * Math.PI / 180) / 9.81;
        Assert.Equal(expected, Calculator.Evaluate("2*800*sin(40)/9.81"), 6);
    }

    [Theory]
    [InlineData("1/0")]                 // divide by zero → infinity, rejected
    [InlineData("sqrt(-1)")]            // domain error
    [InlineData("asin(2)")]             // outside [-1, 1]
    [InlineData("ln(0)")]               // non-positive argument
    [InlineData("log(-5)")]
    public void DomainAndOverflowErrors_AreRejected(string expr)
        => Assert.Throws<FormatException>(() => Calculator.Evaluate(expr));

    [Theory]
    [InlineData("1+")]                  // dangling operator
    [InlineData("(1+2")]                // unbalanced parens
    [InlineData("1+2)")]
    [InlineData("3 4")]                 // trailing garbage
    [InlineData("foo(2)")]              // unknown function
    [InlineData("xyz")]                 // unknown name
    [InlineData("")]                    // empty input
    public void MalformedInput_IsRejected(string expr)
        => Assert.Throws<FormatException>(() => Calculator.Evaluate(expr));

    [Fact]
    public void PathologicallyNestedInput_FailsCleanly_NoStackOverflow()
    {
        // A long run of unary signs would recurse without bound; the parser caps
        // depth and surfaces a normal, catchable error instead of crashing.
        string bomb = new string('-', 5000) + "1";
        Assert.Throws<FormatException>(() => Calculator.Evaluate(bomb));
    }
}
