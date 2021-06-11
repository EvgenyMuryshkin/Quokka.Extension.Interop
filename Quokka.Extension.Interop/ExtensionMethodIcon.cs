namespace Quokka.Extension.Interop
{
    public partial class ExtensionMethodIcon
    {
    }

    public class ExtensionMethodIcon<TIcon> : ExtensionMethodIcon
        where TIcon : struct
    {
        public ExtensionMethodIcon(TIcon icon)
        {
            Icon = icon;
        }

        public TIcon Icon { get; set; }
    }
}
