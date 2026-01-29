using ChaseZelleLib;
using System;

namespace ChaseZelleReader;

internal class Program
{
    static void Main(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length != 1)
        {
            throw new ArgumentException("Expected 1 argument; got " + args.Length);
        }

        var htmlPath = args[0];
        ChaseZelle.HtmlToXml(htmlPath);
    }
}
