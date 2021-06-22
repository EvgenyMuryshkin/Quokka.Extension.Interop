using System;

namespace Quokka.Extension.Interop
{
    public partial class ExtensionMethodIcon
    {
        public ExtensionMethodIcon()
        {

        }

        public ExtensionMethodIcon(Type iconType, int iconValue)
        {
            IconType = iconType;
            IconValue = iconValue;
        }

        public Type IconType { get; set; }
        public int IconValue { get; set; }

        public static implicit operator ExtensionMethodIcon(TopLevelIcon icon)
        {
            return new ExtensionMethodIcon<TopLevelIcon>(icon);
        }

        public override string ToString()
        {
            return $"{IconType.Name}.{IconValue}";
        }
    }

    public class ExtensionMethodIcon<TIcon> : ExtensionMethodIcon
        where TIcon : struct
    {
        public ExtensionMethodIcon(TIcon icon)
        {
            Icon = icon;
            IconType = typeof(TIcon);
            IconValue = (int)(object)icon;
        }

        public TIcon Icon { get; set; }
    }
}
