// See https://aka.ms/new-console-template for more information

using OptiCalc;

var raw = "+322,.2.53(x23+5(*4+(4+2))f---)-d!w((et)33)sdf+";
var f = Calculator.Compile(raw, out var vars);

Console.WriteLine($"Raw: [{raw}]");
Console.WriteLine($"Format: [{string.Join(" ", f.Select(o => o.GetString()))}]");
Console.WriteLine($"Variables: [{string.Join(", ", vars)}]");
Console.WriteLine("setting variables: d = 4, f = 5, s = 6, w = 7, and all other variables are 3");

var solved = Calculator.Solve(f, c =>
    c switch
    {
        'd' => 4,
        'f' => 5,
        's' => 6,
        'w' => 7,
        _ => 3
    });

Console.WriteLine($"Answer: {solved.data}");