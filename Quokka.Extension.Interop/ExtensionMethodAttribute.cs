using System;

namespace Quokka.Extension.Interop
{
    [AttributeUsage(AttributeTargets.Method)]
    public partial class ExtensionMethodAttribute : Attribute
    {
        public ExtensionMethodAttribute()
        {
        }

        private ExtensionMethodIcon _icon;
        private bool _onToolbar;
        private string _title;

        public (ExtensionMethodIcon, bool, string) GetValues()
        {
            return (_icon, _onToolbar, _title);
        }
    }
}
