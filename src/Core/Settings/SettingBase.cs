using Newtonsoft.Json;
using System;

namespace Cats.Telescope.VsExtension.Core.Settings;

interface IStringSetting
{
    public string Collection { get; }

    public string Key { get; }

    public string Value { get; }
}

internal abstract class StringSetting : IStringSetting
{
    public abstract string Collection { get; }

    public virtual string Key => this.GetType().Name;

    public virtual string Value { get; set; }

    public static string ConvertValue<T>(T value)
    {
        if (typeof(T).IsPrimitive)
            return value.ToString();

        return JsonConvert.SerializeObject(value);
    }

    public static T ParseValue<T>(string value)
    {
        if (typeof(T).IsPrimitive)
            return (T)Convert.ChangeType(value, typeof(T));

        return JsonConvert.DeserializeObject<T>(value);
    }
}

internal abstract class SettingBase<T> : StringSetting
{
    public SettingBase()
    {

    }

    public SettingBase(T value)
    {
        OriginalValue = value;
    }

    private string _stringValue;
    private T _originalValue;


    public T OriginalValue
    {
        get => _originalValue;
        set
        {
            _originalValue = value;
            _stringValue = ConvertValue(value);
        }
    }

    public override string Value
    {
        get => _stringValue;
        set
        {
            _stringValue = value;
            _originalValue = ParseValue<T>(value);
        }
    }
}

internal abstract class UISetting<T> : SettingBase<T>
{
    public UISetting()
    {

    }

    public UISetting(T value)
        : base(value)
    {

    }

    public override string Collection => "UI";
}

internal abstract class FilterSetting<T> : SettingBase<T>
{
    public FilterSetting()
    {

    }

    public FilterSetting(T value)
        : base(value)
    {

    }
    public override string Collection => "Filter";
}
