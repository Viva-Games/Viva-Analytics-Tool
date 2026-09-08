using System.Collections.Generic;
using System.Text;
using Viva.Services.Utils;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Genera el código C# de una clase de evento a partir de su nombre y sus parámetros.
    /// Lo usan el editor de eventos y la importación del catálogo, así que ambos producen exactamente el mismo fichero.
    /// </summary>
    public static class AnalyticsEventCodeGenerator
    {
        public const string NAMESPACE = "Viva.Services.Analytics";

        /// <summary>Tipos que admite el editor de eventos. El tracker de Firebase convierte cada uno a lo que admite Firebase.</summary>
        public static readonly string[] ParameterTypes = { "int", "long", "float", "double", "bool", "string" };

        /// <summary>
        /// Genera la clase del evento. Los nombres de parámetro se normalizan a snake_case.
        /// </summary>
        public static string Generate(string scriptName, IReadOnlyList<EventParameter> parameters)
        {
            return Generate(scriptName, parameters, AnalyticsTargets.Firebase);
        }

        /// <summary>
        /// Genera la clase del evento con sus destinos. Con solo Firebase el fichero es el de siempre; con
        /// Facebook o Singular la clase implementa IRoutedAnalyticsEvent y declara Targets.
        /// </summary>
        public static string Generate(string scriptName, IReadOnlyList<EventParameter> parameters, AnalyticsTargets targets)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return GenerateWithoutParameters(scriptName, targets);
            }

            var eventParameters = new List<EventParameter>(parameters.Count);
            foreach (var parameter in parameters)
            {
                eventParameters.Add(new EventParameter(StringUtils.ToSnakeCase(parameter.Name), parameter.Type));
            }

            var outfile = new StringBuilder();
            outfile.AppendLine("using System.Collections.Generic;");
            outfile.AppendLine("");
            outfile.AppendLine($"namespace {NAMESPACE}");
            outfile.AppendLine("{");
            outfile.AppendLine($"\tpublic class {scriptName} : {InterfaceFor(targets)}");
            outfile.AppendLine("\t{");

            // Lista de parámetros que lee el editor de eventos (solo existe en el editor).
            outfile.AppendLine("#if UNITY_EDITOR");
            outfile.AppendLine("\t\tpublic List<EventParameter> eventParameters = new()");
            outfile.AppendLine(EditorParametersToCode(eventParameters));
            outfile.AppendLine("#endif");
            outfile.AppendLine("");

            outfile.AppendLine(ParameterNamesToCode(eventParameters));
            outfile.AppendLine(ParameterVariablesToCode(eventParameters));
            outfile.AppendLine(ConstructorToCode(scriptName, eventParameters));

            var eventKey = StringUtils.ToSnakeCase(scriptName);
            outfile.AppendLine($"\t\tpublic string GetEventKey() => \"{eventKey}\";");
            outfile.AppendLine("");
            AppendTargets(outfile, targets);
            outfile.AppendLine(TrackingFieldsToCode(eventParameters));
            outfile.AppendLine(TrackMethodToCode(scriptName, eventParameters));
            outfile.AppendLine("\t}");
            outfile.AppendLine("}");

            return outfile.ToString();
        }

        /// <summary>
        /// Genera un evento sin parámetros.
        /// </summary>
        public static string GenerateWithoutParameters(string scriptName)
        {
            return GenerateWithoutParameters(scriptName, AnalyticsTargets.Firebase);
        }

        public static string GenerateWithoutParameters(string scriptName, AnalyticsTargets targets)
        {
            var outfile = new StringBuilder();
            outfile.AppendLine("using System.Collections.Generic;");
            outfile.AppendLine("");
            outfile.AppendLine($"namespace {NAMESPACE}");
            outfile.AppendLine("{");
            outfile.AppendLine($"\tpublic class {scriptName} : {InterfaceFor(targets)}");
            outfile.AppendLine("\t{");

            var eventKey = StringUtils.ToSnakeCase(scriptName);
            outfile.AppendLine($"\t\tpublic string GetEventKey() => \"{eventKey}\";");
            outfile.AppendLine("");
            AppendTargets(outfile, targets);
            outfile.AppendLine("\t\tpublic Dictionary<string, object> GetTrackingFields() => new();");
            outfile.AppendLine("");
            outfile.AppendLine($"\t\tpublic static void Track() => AnalyticsService.TrackEvent(new {scriptName}());");
            outfile.AppendLine("\t}");
            outfile.AppendLine("}");
            return outfile.ToString();
        }

        /// <summary>true si el evento va a algún destino además de Firebase.</summary>
        public static bool HasExtraTargets(AnalyticsTargets targets)
        {
            return (targets & ~AnalyticsTargets.Firebase) != 0;
        }

        private static string InterfaceFor(AnalyticsTargets targets)
        {
            return HasExtraTargets(targets) ? "IRoutedAnalyticsEvent" : "IAnalyticsEvent";
        }

        private static void AppendTargets(StringBuilder outfile, AnalyticsTargets targets)
        {
            if (!HasExtraTargets(targets)) return;
            outfile.AppendLine("\t\t// Destinos del evento: Firebase siempre; el resto, los marcados en el Event Manager.");
            outfile.AppendLine($"\t\tpublic AnalyticsTargets Targets => {EventTargetsCodec.ToCode(targets)};");
            outfile.AppendLine("");
        }

        private static string EditorParametersToCode(List<EventParameter> eventParameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\t{");
            foreach (var param in eventParameters)
            {
                result.AppendLine($"\t\t\tnew EventParameter(\"{param.Name}\", \"{param.Type}\"),");
            }
            result.Append("\t\t};");
            return result.ToString();
        }

        private static string ParameterNamesToCode(List<EventParameter> eventParameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\t// Event Parameters Names");
            foreach (var param in eventParameters)
            {
                result.AppendLine($"\t\tpublic const string {param.Name.ToUpper()} = \"{param.Name}\";");
            }
            return result.ToString();
        }

        private static string ParameterVariablesToCode(List<EventParameter> eventParameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\t// Event Parameters Variables");
            foreach (var param in eventParameters)
            {
                result.AppendLine($"\t\tprivate readonly {param.Type} _{StringUtils.ToLowerCamelCase(param.Name)};");
            }
            return result.ToString();
        }

        private static string ConstructorToCode(string scriptName, List<EventParameter> parameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\t// Constructor");
            result.Append($"\t\tprivate {scriptName}(");
            foreach (var param in parameters)
            {
                result.Append($"{param.Type} {StringUtils.ToLowerCamelCase(param.Name)}, ");
            }
            result.Remove(result.Length - 2, 2);
            result.AppendLine(")");
            result.AppendLine("\t\t{");
            foreach (var param in parameters)
            {
                var variable = StringUtils.ToLowerCamelCase(param.Name);
                result.AppendLine($"\t\t\t_{variable} = {variable};");
            }
            result.AppendLine("\t\t}");
            return result.ToString();
        }

        private static string TrackingFieldsToCode(List<EventParameter> eventParameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\tpublic Dictionary<string, object> GetTrackingFields() => new()");
            result.AppendLine("\t\t{");
            foreach (var param in eventParameters)
            {
                result.AppendLine($"\t\t\t{{ {param.Name.ToUpper()}, _{StringUtils.ToLowerCamelCase(param.Name)} }},");
            }
            result.AppendLine("\t\t};");
            return result.ToString();
        }

        private static string TrackMethodToCode(string scriptName, List<EventParameter> eventParameters)
        {
            var result = new StringBuilder();
            result.Append("\t\tpublic static void Track(");
            foreach (var param in eventParameters)
            {
                result.Append($"{param.Type} {StringUtils.ToLowerCamelCase(param.Name)}, ");
            }
            result.Remove(result.Length - 2, 2);
            result.AppendLine(") =>");
            result.Append($"\t\t\tAnalyticsService.TrackEvent(new {scriptName}(");
            foreach (var param in eventParameters)
            {
                result.Append($"{StringUtils.ToLowerCamelCase(param.Name)}, ");
            }
            result.Remove(result.Length - 2, 2);
            result.Append("));");
            return result.ToString();
        }
    }
}
