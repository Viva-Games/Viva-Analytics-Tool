#if UNITY_EDITOR
namespace Viva.Services.Analytics
{
    public class EventParameter
    {
        public string Name { get; set; }
        public string Type { get; set; }

        public EventParameter(string name, string type)
        {
            Name = name;
            Type = type;
        }
    }
}
#endif