using UnityEngine;

namespace Viva.Services.Ads
{
    /// <summary>
    /// Marca un campo string como placement de un formato: en el inspector se muestra como desplegable con los
    /// placements declarados en Viva > Ads > Ad Units.
    /// </summary>
    public sealed class PlacementNameAttribute : PropertyAttribute
    {
        public AdFormat Format { get; }

        public PlacementNameAttribute(AdFormat format)
        {
            Format = format;
        }
    }
}
