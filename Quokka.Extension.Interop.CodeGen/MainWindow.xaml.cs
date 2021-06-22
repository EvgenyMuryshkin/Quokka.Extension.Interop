using Microsoft.Win32;
using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace Quokka.Extension.Interop.CodeGen
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static string SolutionLocation(string current = null)
        {
            if (current == "")
                return "";

            current = current ?? Directory.GetCurrentDirectory();
            if (Directory.EnumerateFiles(current, "*.sln").Any())
                return current;

            return SolutionLocation(System.IO.Path.GetDirectoryName(current));
        }

        public string IconsPath => System.IO.Path.Combine(SolutionLocation(), "icons.json");
        public string ValidIconsPath => System.IO.Path.Combine(SolutionLocation(), "validIcons.json");

        public string GeneratedPath => System.IO.Path.Combine(SolutionLocation(), "Quokka.Extension.Interop", "generated.cs");

        string currentCollection = null;
        Dictionary<string, string> collections = null;
        Dictionary<string, List<IconDescription>> allIcons = new Dictionary<string, List<IconDescription>>();
        public MainWindow()
        {
            InitializeComponent();

            if (File.Exists(IconsPath))
            {
                allIcons = JsonConvert.DeserializeObject<Dictionary<string, List<IconDescription>>>(File.ReadAllText(IconsPath));

                var excludedCollections = new string[]
                {
                    //"Ionicons 4",
                    //"Ionicons 5"
                };

                foreach (var c in excludedCollections)
                {
                    allIcons.Remove(c);
                }
            }
        }
        
        string CollectionName(string collection)
        {
            return collection.Replace(" ", "").Replace("-", "_").Replace(".", "_");
        }

        void DownloadCollections()
        {
            testBrowser.FrameLoadEnd += async (s, a) =>
            {
                if (collections == null)
                {
                    var code = await testBrowser.GetBrowser().MainFrame.GetSourceAsync();
                    code = string.Join("", code.Split(new[] { '\n' }).Select(s => s.Trim()));
                    var body = Regex.Match(code, "<ul(?:.*)>(.*)</ul>", RegexOptions.Multiline);
                    collections = IconsDiscovery.Collections(body.Groups[0].Value);
                }
                else
                {
                    var code = await testBrowser.GetBrowser().MainFrame.GetSourceAsync();
                    code = string.Join("", code.Split(new[] { '\n' }).Select(s => s.Trim()));
                    var body = Regex.Match(code, "<main(?:.*)>(.*)</main>", RegexOptions.Multiline);
                    var icons = IconsDiscovery.Icons(body.Groups[0].Value);

                    allIcons[currentCollection] = icons;
                }

                if (collections.Any())
                {
                    var collection = collections.First();
                    currentCollection = collection.Key;

                    collections.Remove(collection.Key);
                    Dispatcher.Invoke(() =>
                    {
                        testBrowser.Address = $"https://react-icons.github.io{collection.Value}";
                    });
                }
                else
                {
                    File.WriteAllText(IconsPath, JsonConvert.SerializeObject(allIcons));
                }
            };

            testBrowser.Address = "https://react-icons.github.io/react-icons";
        }

        void GenerateCode()
        {
            var collections = new List<string>();

            var codeBuilder = new StringBuilder();
            codeBuilder.AppendLine($"using System;");
            codeBuilder.AppendLine($"using System.Collections.Generic;");
            codeBuilder.AppendLine($"namespace Quokka.Extension.Interop");
            codeBuilder.AppendLine($"{{");
            foreach (var collection in allIcons)
            {
                var collectionName = CollectionName(collection.Key);
                codeBuilder.AppendLine($"\t// {collectionName}: {collection.Value.Count} icons");
                codeBuilder.AppendLine($"\tpublic enum {collectionName}");
                codeBuilder.AppendLine($"\t{{");
                foreach (var item in collection.Value)
                {
                    codeBuilder.AppendLine($"\t\t{item.Id},");
                }
                codeBuilder.AppendLine($"\t}}");

                collections.Add(collectionName);
            }
            codeBuilder.AppendLine($"\tpublic static class ExtensionCatalogue");
            codeBuilder.AppendLine($"\t{{");
            codeBuilder.AppendLine($"\t\tpublic static List<Type> IconTypes = new List<Type>()");
            codeBuilder.AppendLine($"\t\t{{");
            foreach (var collection in collections)
            {
                codeBuilder.AppendLine($"\t\t\ttypeof({collection}),");
            }
            codeBuilder.AppendLine($"\t\t}};");
            codeBuilder.AppendLine($"\t\tpublic static int TotalIconsCount = {allIcons.Sum(c => c.Value.Count)};");
            codeBuilder.AppendLine($"\t}}");

            codeBuilder.AppendLine($"\tpublic partial class ExtensionMethodIcon");
            codeBuilder.AppendLine($"\t{{");
            foreach (var collection in collections)
            {
                codeBuilder.AppendLine($"\t\tpublic static implicit operator ExtensionMethodIcon({collection} icon)");
                codeBuilder.AppendLine($"\t\t{{");
                codeBuilder.AppendLine($"\t\t\treturn new ExtensionMethodIcon<{collection}>(icon);");
                codeBuilder.AppendLine($"\t\t}}");
            }
            codeBuilder.AppendLine($"\t}}");

            codeBuilder.AppendLine($"\tpublic partial class ExtensionMethodAttribute");
            codeBuilder.AppendLine($"\t{{");
            foreach (var collection in collections)
            {
                codeBuilder.AppendLine($"\t\tpublic ExtensionMethodAttribute({collection} icon, string title = null)");
                codeBuilder.AppendLine($"\t\t{{");
                codeBuilder.AppendLine($"\t\t\t_icon = icon;");
                codeBuilder.AppendLine($"\t\t\t_title = title;");
                codeBuilder.AppendLine($"\t\t}}");
            }
            codeBuilder.AppendLine($"\t}}");


            codeBuilder.AppendLine($"}}");
            File.WriteAllText(GeneratedPath, codeBuilder.ToString());
        }

        void ConvertToPNG(string svgPath)
        {
            var pngPath = svgPath.Replace(".svg", ".png");

            var SVGimage = new SkiaSharp.Extended.Svg.SKSvg(new SKSize(16, 16));
            using var ms = new MemoryStream(File.ReadAllBytes(svgPath));
            SVGimage.Load(ms);

            var matrix = SKMatrix.CreateScale(1, 1);
            var img = SKImage.FromPicture(SVGimage.Picture, new SKSizeI(16, 16), matrix);
            var ImageQuality = 100;

            // Convert to PNG
            using var skdata = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, ImageQuality);
            using var PNGStream = System.IO.File.OpenWrite(pngPath);
            skdata.SaveTo(PNGStream);
        }

        string IconsLocation => "c:/tmp/toolbar";
        string FailedIconsLocation => "c:/tmp/toolbar/__failed";

        void GeneratePNG()
        {
            int counter = 0;
            var noSvg = allIcons.SelectMany(p => p.Value).Where(i => string.IsNullOrWhiteSpace(i.SVG));

            foreach (var collection in allIcons)
            {
                var collectionName = CollectionName(collection.Key);
                var collectionPath = System.IO.Path.Combine(IconsLocation, collectionName);

                if (!Directory.Exists(collectionPath))
                {
                    Directory.CreateDirectory(collectionPath);
                }

                var proc = Process.Start(new ProcessStartInfo()
                {
                    FileName = "python",
                    Arguments = @"C:\tmp\toolbar\conv.py",
                    UseShellExecute = false,
                    WorkingDirectory = collectionPath,
                    CreateNoWindow = true,
                });

                proc.WaitForExit();

                if (proc.ExitCode != 0)
                    Debugger.Break();

                /*
                foreach (var item in collection.Value)
                {
                    var svgPath = $"{collectionPath}/{item.Id}.svg";
                    File.WriteAllText(svgPath, item.SVG);
                    ConvertToPNG(svgPath);
                    counter++;
                }
                */
            }
        }

        bool IsEmptyPNG(string pngPath)
        {
            var content = File.ReadAllBytes(pngPath);

            var transparentCount = 0;
            using (var pngStream = new MemoryStream(content))
            using (var image = new Bitmap(pngStream))
            {
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        var pixel = image.GetPixel(x, y);
                        if (pixel.A == 0)
                        {
                            transparentCount++;
                        }
                    }
                }
            }

            return transparentCount == 256;
        }

        void RemoveEmptyPNG()
        {
            if (File.Exists(ValidIconsPath))
            {
                allIcons = JsonConvert.DeserializeObject<Dictionary<string, List<IconDescription>>>(File.ReadAllText(ValidIconsPath));
                return;
            }

            int remaining = allIcons.Sum(c => c.Value.Count());

            foreach (var collection in allIcons)
            {
                var collectionName = CollectionName(collection.Key);
                var collectionPath = System.IO.Path.Combine(IconsLocation, collectionName);

                collection.Value.RemoveAll(item =>
                {
                    var pngPath = $"{collectionPath}/{item.Id}.png";
                    remaining--;
                    return IsEmptyPNG(pngPath);
                });
            }

            allIcons = allIcons.Where(p => p.Value.Any()).ToDictionary(k => k.Key, v => v.Value);
            File.WriteAllText(ValidIconsPath, JsonConvert.SerializeObject(allIcons));
        }


        void StoreEmptyPNG()
        {
            if (Directory.Exists(FailedIconsLocation))
                Directory.Delete(FailedIconsLocation, true);

            Directory.CreateDirectory(FailedIconsLocation);

            foreach (var collection in allIcons)
            {
                var collectionName = CollectionName(collection.Key);
                var collectionPath = System.IO.Path.Combine(IconsLocation, collectionName);

                for (var idx = 0; idx < collection.Value.Count; idx++)
                {
                    var item = collection.Value[idx];
                    var svgPath = $"{collectionPath}/{item.Id}.svg";
                    var pngPath = $"{collectionPath}/{item.Id}.png";

                    if (IsEmptyPNG(pngPath))
                    {
                        File.Copy(svgPath, System.IO.Path.Combine(FailedIconsLocation, System.IO.Path.GetFileName(svgPath)));
                        File.Copy(pngPath, System.IO.Path.Combine(FailedIconsLocation, System.IO.Path.GetFileName(pngPath)));
                    }
                }
            }
        }

        void GenerateInteropResources()
        {
            var resourcesLocation = @"C:\code\Quokka.Extension.Interop\Quokka.Extension.Interop\SVG";
            if (Directory.Exists(resourcesLocation))
                Directory.Delete(resourcesLocation);

            Directory.CreateDirectory(resourcesLocation);

            foreach (var collection in allIcons)
            {
                var collectionName = CollectionName(collection.Key);
                var collectionPath = System.IO.Path.Combine(resourcesLocation, collectionName);

                if (!Directory.Exists(collectionPath))
                {
                    Directory.CreateDirectory(collectionPath);
                }

                foreach (var item in collection.Value)
                {
                    var svgPath = $"{collectionPath}/{item.Id}.svg";
                    File.WriteAllText(svgPath, item.SVG);
                }
            }
        }

        int maxPerCollection = 255;
        void GenerateResources()
        {
            var resourcesLocation = @"C:\code\Quokka.Extension.VS2019\Quokka.Extension.VS2019\Resources\";

            foreach (var collection in allIcons)
            {
                var collectionName = CollectionName(collection.Key);
                var collectionPath = System.IO.Path.Combine(IconsLocation, collectionName);

                var remaining = collection.Value;
                var partIndex = 0;
                while(remaining.Any())
                {
                    var iconsToExport = remaining.Take(maxPerCollection).ToList();
                    remaining = remaining.Skip(maxPerCollection).ToList();

                    var combined = new Bitmap(16 * iconsToExport.Count, 16);

                    for (var idx = 0; idx < iconsToExport.Count; idx++)
                    {
                        var item = iconsToExport[idx];
                        var pngPath = $"{collectionPath}/{item.Id}.png";
                        var content = File.ReadAllBytes(pngPath);

                        using (var pngStream = new MemoryStream(content))
                        using (var image = new Bitmap(pngStream))
                        {
                            for (int y = 0; y < 16; y++)
                            {
                                for (int x = 0; x < 16; x++)
                                {
                                    combined.SetPixel(idx * 16 + x, y, image.GetPixel(x, y));
                                }
                            }
                        }
                    }

                    var startIndex = partIndex * maxPerCollection;
                    combined.Save(System.IO.Path.Combine(resourcesLocation, $"{collectionName}_{startIndex}_{startIndex + maxPerCollection - 1}.png"));
                    partIndex++;
                }
            }
        }

        void GenerateSymbols()
        {
            var vsctPath = @"C:\code\Quokka.Extension.VS2019\Quokka.Extension.VS2019\symbols.vsct";
            var csPath = @"C:\code\Quokka.Extension.VS2019\Quokka.Extension.VS2019\DynamicCommandsSet.cs";
            var xDoc = XDocument.Load(vsctPath);
            var allElements = xDoc.Root.RecursiveElements().ToList();
            var symbols = allElements.Single(e => e.Name.LocalName == "Symbols");

            Func<string, XName> makeName = (name) => XName.Get(name, xDoc.Root.GetDefaultNamespace().NamespaceName);

            foreach (var collection in allIcons)
            {
                var remainng = collection.Value;
                var partIndex = 0;
                while (remainng.Any())
                {
                    var startIndex = partIndex * maxPerCollection;
                    var iconsToExport = remainng.Take(maxPerCollection).ToList();
                    remainng = remainng.Skip(maxPerCollection).ToList();
                    var collectionName = $"{CollectionName(collection.Key)}_{startIndex}_{startIndex + maxPerCollection - 1}";
                    var guidColection = $"guid_{collectionName}";

                    var xSymbol = symbols.Elements().FirstOrDefault(s => s.Attribute("name").Value == guidColection);

                    if (xSymbol == null)
                    {
                        xSymbol = new XElement(
                            makeName("GuidSymbol"),
                            new XAttribute("name", $"guid_{collectionName}"),
                            new XAttribute("value", $"{{{Guid.NewGuid()}}}")
                        );

                        symbols.Add(xSymbol);
                    }
                    else
                    {
                        xSymbol.RemoveNodes();
                    }

                    for (var idx = 0; idx < iconsToExport.Count; idx++)
                    {
                        var item = iconsToExport[idx];
                        xSymbol.Add(new XElement(
                            makeName("IDSymbol"),
                            new XAttribute("name", item.Id),
                            new XAttribute("value", idx + 1)
                            )
                        );
                    }

                    partIndex++;
                }
            }


            var dynamicCommandIds = "124f22e4-53e8-405c-b1d7-d43e63d7e24c";
            var xCommands = symbols.Elements().FirstOrDefault(s => s.Attribute("name").Value == "dynamicCommandIds");

            if (xCommands == null)
            {
                xCommands = new XElement(
                    makeName("GuidSymbol"),
                    new XAttribute("name", $"dynamicCommandIds"),
                    new XAttribute("value", $"{{{dynamicCommandIds}}}")
                );

                symbols.Add(xCommands);
            }
            else
            {
                xCommands.RemoveNodes();
            }

            var codeBuilder = new StringBuilder();
            codeBuilder.AppendLine("// generated file;");
            codeBuilder.AppendLine("using System;");
            codeBuilder.AppendLine("namespace Quokka.Extension.VS2019");
            codeBuilder.AppendLine("{");
            codeBuilder.AppendLine($"\tpublic static class guidDynamicCommandsSet");
            codeBuilder.AppendLine("\t{");
            codeBuilder.AppendLine($"\t\tpublic static readonly Guid SetId = Guid.Parse(\"{dynamicCommandIds}\");");
            var id = 1;

            foreach (var collection in allIcons)
            {
                var collectionName = CollectionName(collection.Key);
                foreach (var icon in collection.Value)
                {
                    xCommands.Add(new XElement(
                        makeName("IDSymbol"),
                        new XAttribute("name", $"{collectionName}_{icon.Id}"),
                        new XAttribute("value", $"{id * 100}")
                    ));
                    codeBuilder.AppendLine($"\t\tpublic const uint {collectionName}_{icon.Id} = {id * 100};");
                    id++;
                }
            }
            codeBuilder.AppendLine("\t}");
            codeBuilder.AppendLine("}");

            xDoc.Save(vsctPath);
            File.WriteAllText(csPath, codeBuilder.ToString());
        }

        void GenerateBitmaps()
        {
            var vsctPath = @"C:\code\Quokka.Extension.VS2019\Quokka.Extension.VS2019\bitmaps.vsct";
            var xDoc = XDocument.Load(vsctPath);
            var allElements = xDoc.Root.RecursiveElements().ToList();
            var bitmaps = allElements.Single(e => e.Name.LocalName == "Bitmaps");

            Func<string, XName> makeName = (name) => XName.Get(name, xDoc.Root.GetDefaultNamespace().NamespaceName);

            foreach (var collection in allIcons)
            {
                var remainng = collection.Value;
                var partIndex = 0;
                while (remainng.Any())
                {
                    var startIndex = partIndex * maxPerCollection;

                    var iconsToExport = remainng.Take(maxPerCollection).ToList();
                    remainng = remainng.Skip(maxPerCollection).ToList();

                    var collectionName = $"{CollectionName(collection.Key)}_{startIndex}_{startIndex + maxPerCollection - 1}";
                    var guidColection = $"guid_{collectionName}";
                    var usedList = string.Join(", ", iconsToExport.Select(i => i.Id));

                    XElement xBitmap = bitmaps.Elements().FirstOrDefault(e => e.Attribute("guid").Value == guidColection);

                    if (xBitmap == null)
                    {
                        xBitmap = new XElement(
                            makeName("Bitmap"),
                            new XAttribute("guid", $"guid_{collectionName}"),
                            new XAttribute("href", $"Resources\\{collectionName}.png"),
                            new XAttribute("usedList", usedList)
                        );

                        bitmaps.Add(xBitmap);
                    }
                    else
                    {
                        xBitmap.Attribute("usedList").Value = usedList;
                    }

                    partIndex++;
                }
            }

            xDoc.Save(vsctPath);
        }

        void GenerateButtons()
        {
            var vsctPath = @"C:\code\Quokka.Extension.VS2019\Quokka.Extension.VS2019\buttons.vsct";
            var xDoc = XDocument.Load(vsctPath);
            var allElements = xDoc.Root.RecursiveElements().ToList();
            var buttons = allElements.Single(e => e.Name.LocalName == "Buttons");
            buttons.RemoveNodes();

            Func<string, XName> makeName = (name) => XName.Get(name, xDoc.Root.GetDefaultNamespace().NamespaceName);

            var priority = 0x2000;
            foreach (var collection in allIcons)
            {
                var remainng = collection.Value;//.Take(10);
                var partIndex = 0;
                while (remainng.Any())
                {
                    var startIndex = partIndex * maxPerCollection;

                    var iconsToExport = remainng.Take(maxPerCollection).ToList();
                    remainng = remainng.Skip(maxPerCollection).ToList();

                    var collectionName = $"{CollectionName(collection.Key)}_{startIndex}_{startIndex + maxPerCollection - 1}";
                    var guidColection = $"guid_{collectionName}";

                    foreach (var icon in iconsToExport)
                    {
                        XElement xBitmap = new XElement(
                                makeName("Button"),
                                new XAttribute("guid", $"dynamicCommandIds"),
                                new XAttribute("id", $"{CollectionName(collection.Key)}_{icon.Id}"),
                                new XAttribute("priority", $"{priority}"),
                                new XAttribute("type", $"Button"),
                                new XElement(
                                    makeName("Parent"),
                                    new XAttribute("guid", $"guidQuokkaExtensionVS2019PackageCmdSet"),
                                    new XAttribute("id", $"DynamicMenuControllerGroup")
                                ),
                                new XElement(
                                    makeName("Icon"),
                                    new XAttribute("guid", guidColection),
                                    new XAttribute("id", $"{icon.Id}")
                                ),
                                new XElement(makeName("CommandFlag"), "DynamicItemStart"),
                                new XElement(makeName("CommandFlag"), "DynamicVisibility"),
                                new XElement(makeName("CommandFlag"), "DefaultInvisible"),
                                new XElement(makeName("CommandFlag"), "TextChanges"),
                                new XElement(makeName("Strings"),
                                    new XElement(makeName("ButtonText"), icon.Id)
                                )
                            );

                        buttons.Add(xBitmap);
                        priority++;
                    }

                    partIndex++;
                }
            }

            xDoc.Save(vsctPath);
        }

        void GenerateVSCT()
        {
            GenerateSymbols();
            GenerateBitmaps();
            GenerateButtons();
        }

        void Test()
        {
            ConvertToPNG(@"C:\tmp\toolbar\tmp\AiFillApple.svg");
            ConvertToPNG(@"C:\tmp\toolbar\tmp\1.svg");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //DownloadCollections();

            //GeneratePNG();
            //StoreEmptyPNG();

            //RemoveEmptyPNG();
            GenerateCode();
            GenerateResources();
            GenerateVSCT();

            view.Content = "Done";
        }
    }
}
