namespace OptiCalc;

public static class Calculator
{
    public static readonly Operator Factorial = new() { c = '!' };
    public static readonly Operator Exponent = new() { c = '^' };
    public static readonly Operator Multiply = new() { c = '*' };
    public static readonly Operator Divide = new() { c = '/' };
    public static readonly Operator Modulus = new() { c = '%' };
    public static readonly Operator Addition = new() { c = '+' };
    public static readonly Operator Subtraction = new() { c = '-' };

    public static Constant SolveWithoutVariables(List<IEquationOp> ops)
    {
        if (ops.Any(o => o is Variable)) throw new ArgumentException("Argument has variables");

        var arr = ops.Select(o => o is Para p ? SolveWithoutVariables(p.ops) : o).ToList();

        int IfNegOverflow(int i) => i == -1 ? int.MaxValue : i;

        while (arr.Count > 1)
        {
            if (arr.Contains(Factorial))
            {
                var factIndex = arr.IndexOf(Factorial);
                arr[factIndex - 1] = new Constant { data = FactorialEq((long) ((Constant) arr[factIndex - 1]).data) };
                arr.RemoveAt(factIndex);
            }
            else if (arr.Contains(Exponent)) GetSolveAndRemove(arr, arr.IndexOf(Exponent), Math.Pow);
            else if (arr.Contains(Multiply) || arr.Contains(Divide) || arr.Contains(Modulus))
            {
                var multIndex = IfNegOverflow(arr.IndexOf(Multiply));
                var divIndex = IfNegOverflow(arr.IndexOf(Divide));
                var modIndex = IfNegOverflow(arr.IndexOf(Modulus));

                if (multIndex < divIndex && multIndex < modIndex) GetSolveAndRemove(arr, multIndex, (a, b) => a * b);
                else if (divIndex < modIndex) GetSolveAndRemove(arr, divIndex, (a, b) => a / b);
                else GetSolveAndRemove(arr, modIndex, (a, b) => a % b);
            }
            else if (arr.Contains(Addition) || arr.Contains(Subtraction))
            {
                var addIndex = IfNegOverflow(arr.IndexOf(Addition));
                var subIndex = IfNegOverflow(arr.IndexOf(Subtraction));

                if (addIndex < subIndex) GetSolveAndRemove(arr, addIndex, (a, b) => a + b);
                else GetSolveAndRemove(arr, subIndex, (a, b) => a - b);
            }
            else
            {
                var before = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[EQUATION WARNING] equation is: {string.Join(", ", arr)} ASSUMING ADDITION!!");
                Console.ForegroundColor = before;

                return new Constant { data = arr.Select(o => ((Constant) o).data).Sum() };
            }
        }

        return (Constant) arr[0];
    }

    public static void GetSolveAndRemove(List<IEquationOp> ops, int opIndex, Func<double, double, double> eq)
    {
        var res = SolveStepTwoConstant((Constant) ops[opIndex - 1], (Constant) ops[opIndex + 1], eq);
        ops.RemoveRange(opIndex - 1, 3);
        ops.Insert(opIndex - 1, res);
    }

    public static Constant SolveStepTwoConstant(Constant a, Constant b, Func<double, double, double> eq)
    {
        return new Constant { data = eq.Invoke(a.data, b.data) };
    }

    public static double Solve(string equation, out IEnumerable<char> variables, Func<char, double> getVars)
    {
        return Solve(Compile(equation, out variables), getVars).data;
    }

    public static Constant Solve(IEnumerable<IEquationOp> ops, Func<char, double> getVars)
    {
        var arr = ops.Select(o => o is Para p ? Solve(p.ops, getVars) : o)
            .Select(o => o is Variable v ? new Constant { data = getVars.Invoke(v.c) } : o).ToList();
        return SolveWithoutVariables(arr);
    }

    public static List<IEquationOp> Compile(string rawEq, out IEnumerable<char> variables)
    {
        if (rawEq.Count(c => c == '(') != rawEq.Count(c => c == ')'))
            throw new ArgumentException("Uneven amount of ( or )");

        var nRaw = rawEq.Replace(" ", "").Replace(")(", ")*(")
            .Replace("--", "+").Replace(",", "");

        var level = 0;
        var ops = nRaw.Select(c => (IEquationOp) (c switch
        {
            >= '0' and <= '9' or '.' => new TempConstant { s = $"{c}" },
            '+' or '-' or '*' or '!' or '/' or '%' or '^' => new Operator { c = c },
            '(' => new ParaOpen { level = ++level },
            ')' => new ParaClose { level = level-- },
            _ => new Variable { c = c }
        })).ToList();

        var vars = ops.OfType<Variable>().Select(v => v.c).ToList();
        variables = vars.Union(vars).OrderBy(c => c);

        CleanOps(ops);
        CorrectPara<ParaOpen>(ops); // n() => n * ()
        CorrectPara<ParaClose>(ops, 1, 1); // ()n => n * ()
        AddMulti(ops); //34n => 34 * n
        FixOps(ops);
        SplitPara(ops);
        foreach (var para in ops.OfType<Para>()) para.Condense();

        // Console.WriteLine(
        //     $"og: [{rawEq}] before: [{string.Join(";", ops.Select(o => o.GetString()))}] new: [{string.Join(";", nOps.Select(o => o.GetString()))}]");
        return ops;
    }

    public static double FactorialEq(long i)
    {
        switch (i)
        {
            case < 0:
                return double.NaN;
            case < 2:
                return 1;
        }

        var val = 2d;
        for (var j = 3; j <= i; j++) val *= j;
        return val;
    }

    public static void FixOps(List<IEquationOp> ops)
    {
        for (var i = 0; i < ops.Count; i++)
        {
            if (ops[i] is not Operator) continue;
            if (i != 0 && ops[i - 1] is ParaOpen)
            {
                ops.RemoveAt(i--);
                i--;
                continue;
            }

            if (i != ops.Count - 1 && ops[i + 1] is ParaClose)
            {
                ops.RemoveAt(i--);
                i--;
                continue;
            }

            if (i == 0) ops.RemoveAt(i--);
            if (i != ops.Count - 1) continue;
            ops.RemoveAt(i--);
            i--;
        }
    }

    public static void SplitPara(List<IEquationOp> ops)
    {
        while (ops.Any(o => o is ParaOpen))
        {
            var startLevel = ops.OfType<ParaOpen>().Select(p => p.level).Max();
            var startIndex = ops.IndexOf(new ParaOpen { level = startLevel });
            var endIndex = ops.IndexOf(new ParaClose { level = startLevel });
            var para = new Para { ops = ops.GetRange(startIndex + 1, endIndex - startIndex - 1) };
            ops.RemoveRange(startIndex, endIndex - startIndex + 1);
            ops.Insert(startIndex, para);
        }
    }

    private static void AddMulti(List<IEquationOp> ops)
    {
        var offset = 0;
        var varIndexes = GetSpecificIndexesOfType<Variable>(ops);
        foreach (var oi in varIndexes)
        {
            var i = oi + offset;

            if (i != ops.Count - 1 && ops[i + 1] is Constant or Variable)
            {
                ops.Insert(i + 1, Multiply);
                offset++;
            }

            if (i == 0 || ops[i - 1] is not (Constant or Variable or Operator)) continue;
            if (ops[i - 1] is Operator o && o.c != '!') continue;
            ops.Insert(i, Multiply);
            offset++;
        }
    }

    public static void CorrectPara<T>(List<IEquationOp> ops, int whereVar = -1, int varOff = 0)
    {
        var offset = 0;
        var openIndexes = GetSpecificIndexesOfType<T>(ops);
        foreach (var i in from oi in openIndexes
                 where oi != 0
                 select oi + offset into i
                 where ops[i + whereVar] is not (Operator or T)
                 select i)
        {
            ops.Insert(i + varOff, Multiply);
            offset++;
        }
    }

    public static void CleanOps(List<IEquationOp> ops)
    {
        var constPos = -1;
        for (var i = 0; i < ops.Count; i++)
        {
            if (ops[i] is TempConstant && constPos == -1) constPos = i;
            else if (ops[i] is not TempConstant && constPos != -1)
            {
                var s = string.Join("", ops.GetRange(constPos, i - constPos).Select(o => ((TempConstant) o).s));
                while (s.Count(c => c == '.') > 1) s = s.Remove(s.LastIndexOf('.'), 1);
                ops.RemoveRange(constPos, i - constPos);
                ops.Insert(constPos, new Constant { data = double.Parse(s) });
                i = constPos;
                constPos = -1;
            }
        }

        // foreach (var op in ops)
        // {
        //     if (op is Constant c)
        //     {
        //         if (!nOps.Any() || nOps[^1] is not Constant cc)
        //         {
        //             nOps.Add(new Constant { String = c.String });
        //             continue;
        //         }
        //
        //         if (c.String is "." && cc.String.Contains('.')) continue;
        //         cc.String += c.String;
        //     }
        //     else nOps.Add(op);
        // }
    }

    public static IEnumerable<int> GetSpecificIndexesOfType<T>(List<IEquationOp> ops)
    {
        return ops.Select((o, i) => (o, i)).Where(c => c.o is T).Select(c => c.i).ToList();
    }
}

public interface IEquationOp
{
    public string GetString();
}

public record Constant : IEquationOp
{
    public double data;

    public string GetString() => $"{data}";
}

public record TempConstant : IEquationOp
{
    public string s;

    public Constant Value() => new() { data = double.Parse(s) };
    public string GetString() => s;
}

public record Variable : IEquationOp
{
    public char c;
    public string GetString() => $"{c}";
}

public record Para : IEquationOp
{
    public List<IEquationOp> ops;

    public void Condense()
    {
        for (var i = 0; i < ops.Count; i++)
        {
            if (ops[i] is not Para p) continue;
            p.Condense();
            if (p.ops.Any(o => o is Variable)) continue;
            ops[i] = Calculator.SolveWithoutVariables(p.ops);
        }

        // todo: work on further optimizing equations
        // [...]
    }

    public string GetString() => $"({string.Join(" ", ops.Select(o => o.GetString()))})";
}

public record ParaOpen : IEquationOp
{
    public int level;
    public string GetString() => "(";
}

public record ParaClose : IEquationOp
{
    public int level;
    public string GetString() => ")";
}

public record Operator : IEquationOp
{
    public char c;
    public string GetString() => $"{c}";
}