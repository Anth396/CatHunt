using UnityEngine;

[CreateAssetMenu(fileName = "StringVariable", menuName = "ScriptableObjects/StringVariable", order = 8)]
public class StringVariable : Variable<string>
{
    public override void SetValue(string value)
    {
        _value = value;
    }

    public void SetValue(StringVariable value) { SetValue(value.Value); }
}
