using HtmlAgilityPack;

namespace ChaseZelleLib;

public static class Extensions
{
    public static string InnerTextDecoded(this HtmlNode node)
    {
        return HtmlEntity.DeEntitize(node.InnerText);
    }
}
