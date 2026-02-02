using System.ComponentModel;
using System.Reflection;

namespace GameData.Core.Enums
{
    // -1 from client enchantment slot number


    // masks for ITEM_FIELD_FLAGS field

    // stored in character_pet.slot

    // There might be a lot more
}

public static class EnumCustomAttributeHelper
{
    public static string GetDescription(this Enum value)
    {
        Type type = value.GetType();
        string name = Enum.GetName(type, value);
        if (name != null)
        {
            FieldInfo field = type.GetField(name);
            if (field != null)
            {
                DescriptionAttribute attr =
                       Attribute.GetCustomAttribute(field,
                         typeof(DescriptionAttribute)) as DescriptionAttribute;
                if (attr != null)
                {
                    return attr.Description;
                }
            }
        }
        return null;
    }
}
