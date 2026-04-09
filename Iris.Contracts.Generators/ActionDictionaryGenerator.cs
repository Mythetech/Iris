using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Iris.Contracts.Audit
{
    [Generator]
    public class ActionsDictionaryGenerator : IIncrementalGenerator
    {
        private static readonly Regex SplitByWordRegex = new Regex("(?<!^)([A-Z])");

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider, static (spc, compilation) =>
            {
                var actionsClassSymbol = compilation.Assembly.GetTypeByMetadataName("Iris.Contracts.Audit.Actions");

                if (actionsClassSymbol is null)
                    return;

                var splitRegex = new Regex("(?<!^)([A-Z])");
                var dictionaryCode = new StringBuilder();
                dictionaryCode.AppendLine("namespace Iris.Contracts.Audit");
                dictionaryCode.AppendLine("{");
                dictionaryCode.AppendLine("    public partial class Actions");
                dictionaryCode.AppendLine("    {");
                dictionaryCode.AppendLine("        public static readonly System.Collections.Generic.IReadOnlyDictionary<string, string> DisplayLookup = new System.Collections.Generic.Dictionary<string, string>");
                dictionaryCode.AppendLine("        {");

                foreach (var field in actionsClassSymbol.GetMembers().OfType<IFieldSymbol>())
                {
                    var fieldName = field.Name;
                    var fieldValue = field.ConstantValue?.ToString();
                    var splitFieldName = splitRegex.Replace(fieldName, " $1");
                    dictionaryCode.AppendLine($"            {{ \"{fieldValue}\", \"{splitFieldName}\" }},");
                }

                dictionaryCode.AppendLine("        };");
                dictionaryCode.AppendLine("    }");
                dictionaryCode.AppendLine("}");

                spc.AddSource("Actions.g.cs", SourceText.From(dictionaryCode.ToString(), Encoding.UTF8));
            });
        }
    }
}
