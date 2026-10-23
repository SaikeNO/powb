using System.ComponentModel;
using System.Reflection;
using Szkieletyzacja.Core;

namespace Szkieletyzacja;

public sealed class AlgorithmRow : INotifyPropertyChanged
{
    private const string EmptyInfo = "—";

    private bool _isSelected = true;
    private string _info = EmptyInfo;

    public AlgorithmRow(ThinningAlgorithm algorithm)
    {
        Algorithm = algorithm;
        Name = typeof(ThinningAlgorithm)
            .GetField(algorithm.ToString())!
            .GetCustomAttribute<DescriptionAttribute>()!
            .Description;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ThinningAlgorithm Algorithm { get; }

    public string Name { get; }

    public byte[]? Skeleton { get; set; }

    public double Milliseconds { get; set; }

    public int SkeletonPixels { get; set; }

    public bool IsSelected
    {
        get
        {
            return _isSelected;
        }
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public string Info
    {
        get
        {
            return _info;
        }
        set
        {
            _info = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Info)));
        }
    }

    public void Reset()
    {
        Skeleton = null;
        Milliseconds = 0;
        SkeletonPixels = 0;
        Info = EmptyInfo;
    }
}
