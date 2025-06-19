using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Iris.Contracts.Audit
{
    [Generator]
    public partial class ActionsDictionaryGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var actionsClassSymbol = context.Compilation.Assembly.GetTypeByMetadataName("Iris.Contracts.Audit.Actions");

            if (actionsClassSymbol == null)
            {
                return;
            }

            // Generate the dictionary code.
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
                var splitFieldName = splitByWord().Replace(fieldName, " $1");
                dictionaryCode.AppendLine($"            {{ \"{fieldValue}\", \"{splitFieldName}\" }},");
            }
            dictionaryCode.AppendLine("        };");
            dictionaryCode.AppendLine("    }");
            dictionaryCode.AppendLine("}");

            context.AddSource("Actions.g.cs", SourceText.From(dictionaryCode.ToString(), Encoding.UTF8));
        }

        [System.Text.RegularExpressions.GeneratedRegex("(?<!^)([A-Z])")]
        private static partial System.Text.RegularExpressions.Regex splitByWord();
    }
}
