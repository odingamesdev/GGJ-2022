using System;

namespace Source.Scripts.Core.UI.Attributes
{
    [System.AttributeUsage(AttributeTargets.Field)]
    public class UIInject : System.Attribute
    {
        public string Name { get; }
        public UIInject(string name)
        {
            Name = name;
        }
    }
}