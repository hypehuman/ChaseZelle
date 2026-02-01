using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ChaseZelleLib;

public static class ChaseZelle
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

        var rows = new List<CsvRow>
        {
            CsvRow.Headers
        };

        // Chase orders the transactions inconsistently between sessions,
        // so let's sort by ID instead.
        string? prevId = null;
        foreach (var row in ParseHtml(htmlRoot).OrderBy(r => r.ID))
        {
            if (prevId == row.ID)
            {
                // This is a bug in Chase's website, and I've seen this actually happen.
                // In this case, one transaction is duplicated and another is missing.
                throw new InvalidDataException($"Duplicate ID: {row.ID} - this is a bug in Chase's website. Please restart your browser and reload the data.");
            }

            prevId = row.ID;

            rows.Add(row);
        }

        using (var csvStream = new FileStream(csvPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            using (var csvWriter = new StreamWriter(csvStream))
            {
                foreach (var row in rows)
                {
                    row.WriteTo(csvWriter);
                }
            }
        }
    }

    private static IEnumerable<CsvRow> ParseHtml(HtmlNode htmlRoot)
    {
        int tbodyCount = 0;
        int tbodyIdCount = 0;
        int tbodyIdMatchCount = 0;
        var txNodes = new List<(HtmlNode node, string id)>();
        foreach (var tbodyNode in htmlRoot.Descendants("tbody"))
        {
            tbodyCount++;

            var idAttr = tbodyNode.Attributes["id"];
            if (idAttr == null)
            {
                continue;
            }

            tbodyIdCount++;

            var id = idAttr.Value;
            const string idPrefix = "qpReceivedActivity_tBody_";
            if (!id.StartsWith(idPrefix))
            {
                continue;
            }

            tbodyIdMatchCount++;

            txNodes.Add((tbodyNode, id[idPrefix.Length..]));
        }

        Console.WriteLine($"{nameof(tbodyCount)}: {tbodyCount}");
        Console.WriteLine($"{nameof(tbodyIdCount)}: {tbodyIdCount}");
        Console.WriteLine($"{nameof(tbodyIdMatchCount)}: {tbodyIdMatchCount}");
        Console.WriteLine($"Number of unique parents of transaction nodes: {txNodes.Select(pair => pair.node.ParentNode).ToHashSet().Count}");

        foreach (var (txNode, id) in txNodes)
        {
            var rowNode = txNode.ChildNodes.Single(n => n.NameEquals("tr") && (n.Attributes["id"]?.Value ?? "").StartsWith("receivedTransaction_"));
            yield return new(
                ID: id,
                Date: ParseCell(rowNode, "Date received "),
                Status: ParseCell(rowNode, "Status", out var message),
                Message: RemoveOuterQuotes(message),
                Sender: ParseCell(rowNode, "Sender"),
                Amount: ParseCell(rowNode, "Amount"),
                TransactionNumber: ParseTransactionNumber(txNode)
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

    private static string? RemoveOuterQuotes(string? input)
    {
        if (input == null || !input.StartsWith('"') || !input.EndsWith('"'))
        {
            return input;
        }

        return input[1..^1];
    }

    private static string? ParseTransactionNumber(HtmlNode txNode)
    {
        var detailsNode = txNode.ChildNodes.SingleOrDefault(n => n.NameEquals("tr") && (n.Attributes["id"]?.Value ?? "").StartsWith("showDetailTr_"));
        if (detailsNode == null)
        {
            // Transaction details are collapsed, so the transaction number isn't shown.
            return null;
        }

        var dataNode = detailsNode.Descendants("span").Single(n => n.Attributes["class"]?.Value == "DATA");
        return dataNode.InnerText;
    }

    /// <summary>
    /// Extracted from <see cref="HtmlNode.Descendants(string)"/>
    /// </summary>
    internal static bool NameEquals(this HtmlNode node, string name)
    {
        return String.Equals(node.Name, name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// If <paramref name="str"/> is null, returns an empty string.
    /// Otherwise, returns <paramref name="str"/> wrapped in quotes and with any internal quotes escaped.
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

    /// <summary>
    /// Each property represents one cell of the CSV row.
    /// </summary>
    /// <param name="TransactionNumber">
    /// For transactions from Chase to Chase, this appears to be the same as <paramref name="ID"/>.
    /// For transactions from other banks, this is different from <paramref name="ID"/> and may include letters.
    /// Only visible if the transaction details were expanded on the website.
    /// </param>
    private record class CsvRow(
        string ID,
        string Date,
        string Status,
        string? Message,
        string Sender,
        string Amount,
        string? TransactionNumber
    )
    {
        public static CsvRow Headers => new(
            ID: nameof(ID),
            Date: nameof(Date),
            Status: nameof(Status),
            Message: nameof(Message),
            Sender: nameof(Sender),
            Amount: nameof(Amount),
            TransactionNumber: nameof(TransactionNumber)
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
                FormatCsvCell(TransactionNumber),
            };

            var rowStr = string.Join(',', cells);
            writer.WriteLine(rowStr);
        }
    }
}
