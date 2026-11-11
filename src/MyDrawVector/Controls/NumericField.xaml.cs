using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace MyDrawVector.Controls;

public sealed partial class NumericField : UserControl
{
    private double _value;
    private double _minimum = double.NegativeInfinity;
    private double _maximum = double.PositiveInfinity;
    private double _step = 1;

    public NumericField()
    {
        InitializeComponent();
    }

    public event EventHandler<NumericFieldValueChangedEventArgs>? ValueChanged;

    public string Header
    {
        get => HeaderText.Text;
        set
        {
            HeaderText.Text = value;
            HeaderText.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
            AutomationProperties.SetName(InputBox, value);
        }
    }

    public double Minimum
    {
        get => _minimum;
        set => _minimum = value;
    }

    public double Maximum
    {
        get => _maximum;
        set => _maximum = value;
    }

    public double Step
    {
        get => _step;
        set => _step = Math.Abs(value) < 0.0001 ? 1 : value;
    }

    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            UpdateText();
        }
    }

    public void ClearText()
    {
        InputBox.Text = string.Empty;
    }

    private void InputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitText();
    }

    private void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            CommitText();
            e.Handled = true;
        }
    }

    private void MinusButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeBy(-Step);
    }

    private void PlusButton_Click(object sender, RoutedEventArgs e)
    {
        ChangeBy(Step);
    }

    private void CommitText()
    {
        if (!TryRead(out var value))
        {
            UpdateText();
            return;
        }

        var next = Clamp(value);
        var changed = Math.Abs(next - _value) > 0.000001;
        _value = next;
        UpdateText();
        if (changed)
        {
            ValueChanged?.Invoke(this, new NumericFieldValueChangedEventArgs(next));
        }
    }

    private void ChangeBy(double delta)
    {
        var basis = TryRead(out var typed) && !double.IsNaN(typed) ? typed : _value;
        if (double.IsNaN(basis))
        {
            basis = 0;
        }

        var next = Clamp(basis + delta);
        _value = next;
        UpdateText();
        ValueChanged?.Invoke(this, new NumericFieldValueChangedEventArgs(next));
    }

    private bool TryRead(out double value)
    {
        if (double.TryParse(InputBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        return double.TryParse(InputBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }

    private double Clamp(double value)
    {
        if (value < _minimum)
        {
            return _minimum;
        }

        if (value > _maximum)
        {
            return _maximum;
        }

        return value;
    }

    private void UpdateText()
    {
        InputBox.Text = double.IsNaN(_value)
            ? string.Empty
            : _value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}

public sealed class NumericFieldValueChangedEventArgs(double value) : EventArgs
{
    public double Value { get; } = value;
}
