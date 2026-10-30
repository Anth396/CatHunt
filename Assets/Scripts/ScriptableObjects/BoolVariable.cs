using UnityEngine;

[CreateAssetMenu(fileName = "BoolVariable", menuName = "ScriptableObjects/BoolVariable", order = 7)]
public class BoolVariable : Variable<bool>
{
    public override void SetValue(bool value)
    {
        _value = value;
    }

    public void SetValue(BoolVariable value) { SetValue(value.Value); }

    public void Toggle() { _value = !_value; }
}
