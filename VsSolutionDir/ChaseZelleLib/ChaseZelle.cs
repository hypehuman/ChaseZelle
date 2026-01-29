using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ChaseZelleLib;

public static partial class ChaseZelle
{
    public static void HtmlToXml(string htmlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlPath);

        var htmlExtension = Path.GetExtension(htmlPath);
        var pathWithoutExtension = htmlPath[..^htmlExtension.Length];
        var xmlPath = pathWithoutExtension + ".xml";

        HtmlToXml(htmlPath, xmlPath);
    }

    public static void HtmlToXml(string htmlPath, string xmlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlPath);

        var htmlDoc = new HtmlDocument();
        htmlDoc.Load(htmlPath);

        var xmlRoot = HtmlToXml(htmlDoc.DocumentNode);

        var xmlDir = Path.GetDirectoryName(xmlPath);
        if (!Directory.Exists(xmlDir))
        {
            Directory.CreateDirectory(xmlDir);
        }

        using (var xmlStream = new FileStream(xmlPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            xmlRoot.Save(xmlStream);
        }
    }

    public static XElement HtmlToXml(HtmlNode htmlRoot)
    {
        int tbodyCount = 0;
        int tbodyIdCount = 0;
        int tbodyIdMatchCount = 0;
        var activityRows = new List<(HtmlNode node, string id)>();
        foreach (var tbodyNode in htmlRoot.Descendants("tbody"))
        {
            tbodyCount++;

            var idAttr = tbodyNode.Attributes["id"];
            if (idAttr == null)
            {
                continue;
            }

            tbodyIdCount++;

            var idMatch = TbodyIdPattern().Match(idAttr.Value);
            if (!idMatch.Success)
            {
                continue;
            }

            tbodyIdMatchCount++;

            activityRows.Add((tbodyNode, idMatch.Groups[1].Value));
        }

        Console.WriteLine($"{nameof(tbodyCount)}: {tbodyCount}");
        Console.WriteLine($"{nameof(tbodyIdCount)}: {tbodyIdCount}");
        Console.WriteLine($"{nameof(tbodyIdMatchCount)}: {tbodyIdMatchCount}");
        Console.WriteLine($"Number of unique parents of activity row nodes: {activityRows.Select(pair => pair.node.ParentNode).ToHashSet().Count}");

        var xmlRoot = new XElement("Transactions");
        foreach (var (rowNode, id) in activityRows)
        {
            var txNode = rowNode.ChildNodes.Single(n => n.NameEquals("tr") && (n.Attributes["id"]?.Value ?? "").StartsWith("receivedTransaction_"));
            var txElement = new XElement(
                "Transaction",
                new XElement("ID", id),
                new XElement("Date", ParseCell(txNode, "Date received ")),
                new XElement("Status", ParseCell(txNode, "Status", out var memo)),
                new XElement("Memo", memo),
                new XElement("Sender", ParseCell(txNode, "Sender")),
                new XElement("Amount", ParseCell(txNode, "Amount"))
            );
            xmlRoot.Add(txElement);
        }

        return xmlRoot;
    }

    private static string ParseCell(HtmlNode rowNode, string header)
    {
        return ParseCellAndSubLabel(rowNode, header, false, out _);
    }

    private static string ParseCell(HtmlNode rowNode, string header, out string? subLabel)
    {
        return ParseCellAndSubLabel(rowNode, header, true, out subLabel!);
    }

    private static string ParseCellAndSubLabel(HtmlNode rowNode, string header, bool parseSubLabel, out string? subLabel)
    {
        var cellNode = rowNode.ChildNodes.Single(n => n.NameEquals("td") && n.Attributes["data-th"]?.Value == header);
        var span = cellNode.ChildNodes.Single(n => n.NameEquals("span"));
        var result = span.InnerText;
        if (parseSubLabel)
        {
            var div = cellNode.ChildNodes.SingleOrDefault(n => n.NameEquals("div") && n.Attributes["class"]?.Value == "subLabel");
            subLabel = div?.InnerText;
        }
        else
        {
            subLabel = null;
        }
        return result;
    }

    [GeneratedRegex("^qpReceivedActivity_tBody_(.+)$")]
    private static partial Regex TbodyIdPattern();

    /// <summary>
    /// Extracted from <see cref="HtmlNode.Descendants(string)"/>
    /// </summary>
    internal static bool NameEquals(this HtmlNode node, string name)
    {
        return String.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase);
    }
}
