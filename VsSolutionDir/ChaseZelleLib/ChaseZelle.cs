using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ChaseZelleLib;

public static partial class ChaseZelle
{
    public static void HtmlToCsv(string htmlPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlPath);

        var htmlExtension = Path.GetExtension(htmlPath);
        var pathWithoutExtension = htmlPath[..^htmlExtension.Length];
        var csvPath = pathWithoutExtension + ".csv";

        HtmlToCsv(htmlPath, csvPath);
    }

    public static void HtmlToCsv(string htmlPath, string csvPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(csvPath);

        var htmlDoc = new HtmlDocument();
        htmlDoc.Load(htmlPath);
        var htmlRoot = htmlDoc.DocumentNode;

        var csvDir = Path.GetDirectoryName(csvPath);
        if (!Directory.Exists(csvDir))
        {
            Directory.CreateDirectory(csvDir);
        }

        using (var csvStream = new FileStream(csvPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            using (var csvWriter = new StreamWriter(csvStream))
            {
                RowData.Headers.WriteTo(csvWriter);
                foreach (var row in ParseHtml(htmlRoot))
                {
                    row.WriteTo(csvWriter);
                }
            }
        }
    }

    private static IEnumerable<RowData> ParseHtml(HtmlNode htmlRoot)
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

        foreach (var (rowNode, id) in activityRows)
        {
            var txNode = rowNode.ChildNodes.Single(n => n.NameEquals("tr") && (n.Attributes["id"]?.Value ?? "").StartsWith("receivedTransaction_"));
            yield return new(
                ID: id,
                Date: ParseCell(txNode, "Date received "),
                Status: ParseCell(txNode, "Status", out var message),
                Message: message,
                Sender: ParseCell(txNode, "Sender"),
                Amount: ParseCell(txNode, "Amount")
            );
        }
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

    /// <summary>
    /// Adds quotes and escapes any internal quotes.
    /// Adapted from https://stackoverflow.com/a/6377656
    /// </summary>
    private static string FormatCsvCell(string? str)
    {
        if (str == null)
        {
            return "";
        }

        var sb = new StringBuilder();
        sb.Append('"');
        foreach (char nextChar in str)
        {
            sb.Append(nextChar);
            if (nextChar == '"')
            {
                sb.Append('"');
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private record class RowData(
        string ID,
        string Date,
        string Status,
        string? Message,
        string Sender,
        string Amount
    )
    {
        public static RowData Headers => new(
            ID: nameof(ID),
            Date: nameof(Date),
            Status: nameof(Status),
            Message: nameof(Message),
            Sender: nameof(Sender),
            Amount: nameof(Amount)
        );

        public void WriteTo(StreamWriter writer)
        {
            var cells = new[]
            {
                FormatCsvCell(ID),
                FormatCsvCell(Date),
                FormatCsvCell(Status),
                FormatCsvCell(Message),
                FormatCsvCell(Sender),
                FormatCsvCell(Amount),
            };

            var rowStr = string.Join(',', cells);
            writer.WriteLine(rowStr);
        }
    }
}
