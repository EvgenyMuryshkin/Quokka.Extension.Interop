using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Quokka.Extension.Interop.CodeGen
{
    public class IconDescription
    {
        public string Id;
        public string SVG;
    }

    public static class IconsDiscovery
    {
        public static IEnumerable<XElement> RecursiveElements(this XElement root)
        {
            return new[] { root }.Concat(root.Elements().SelectMany(e => e.RecursiveElements()));
        }

        public static Dictionary<string, string> Collections(string content)
        {
            var x = XDocument.Parse(content);
            var flat = x.Root.RecursiveElements();
            var menu = flat.Where(e => e.Name.LocalName == "ul").Single();
            var flatMenu = menu.RecursiveElements();
            var flatA = flatMenu.Where(e => e.Name.LocalName == "a" && e.Value != "Home");
            var icons = flatA.ToDictionary(a => a.Value, a => a.Attribute("href").Value);

            return icons;
        }


        public static List<IconDescription> Icons(string collectionContent)
        {
            var result = new List<IconDescription>();

            var xPage = XDocument.Parse(collectionContent);
            var items = xPage.Root.RecursiveElements().Where(e => e.Name.LocalName == "div" && e.Attribute("class").Value == "item");
            foreach (var item in items)
            {
                result.Add(new IconDescription()
                {
                    Id = item.Elements().Last().Value,
                    SVG = item.Elements().First().LastNode.ToString()
                });
            }

            return result;
        }

        public static async Task<List<IconDescription>> CollectIcons()
        {
            var result = new List<IconDescription>();
            var url = "https://react-icons.github.io";
            var client = new HttpClient();
            var content = await client.GetStringAsync($"{url}/react-icons");
            var x = XDocument.Parse(content);
            var flat = x.Root.RecursiveElements();
            var menu = flat.Where(e => e.Name.LocalName == "ul").Single();
            var flatMenu = menu.RecursiveElements();
            var flatA = flatMenu.Where(e => e.Name.LocalName == "a" && e.Value != "Home");
            var icons = flatA.ToDictionary(a => a.Value, a => a.Attribute("href").Value);

            foreach (var collection in icons)
            {
                var collectionUrl = $"{url}{collection.Value}";
                var page = await client.GetStringAsync(collectionUrl);
                var xPage = XDocument.Parse(page);
                var items = xPage.Root.RecursiveElements().Where(e => e.Name.LocalName == "div" && e.Attribute("class").Value == "item");
                foreach (var item in items)
                {
                    result.Add(new IconDescription()
                    {
                        Id = item.Elements().Last().Value,
                        SVG = item.Elements().First().Value
                        
                    });
                }
            }

            return result;
        }
    }
}
