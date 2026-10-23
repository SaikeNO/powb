using System.ComponentModel;

namespace Szkieletyzacja.Core;

public enum ThinningAlgorithm
{
    [Description("Zhang-Suen")]
    ZhangSuen,

    [Description("Guo-Hall")]
    GuoHall,

    [Description("KMM")]
    Kmm,

    [Description("K3M")]
    K3M
}
