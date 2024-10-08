//using Microsoft.CodeAnalysis;
//using Microsoft.CodeAnalysis.CSharp;
//using Microsoft.CodeAnalysis.CSharp.Syntax;
//using Microsoft.CodeAnalysis.Text;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;

//namespace SgNetSourceGenerator
//{
//    [Generator]
//    public class NetworkedSourceGenerator : ISourceGenerator
//    {
//        public string networkedAttributeText = @"using System;

//namespace StargateNet
//{
//    public class NetworkedAttribute : Attribute
//    {
//        public NetworkedAttribute()
//        {
//        }

//        public Action onValueChanged;
//    }
//}";

//        public void Execute(GeneratorExecutionContext context)
//        {
//            context.AddSource("NetworkedAttribute", SourceText.From(this.networkedAttributeText, Encoding.UTF8));

//            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
//            {
//                throw new Exception($"Wrong SyntaxReceiver! {context.SyntaxReceiver}");
//            }

//            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
//            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(this.networkedAttributeText, Encoding.UTF8)));

//            INamedTypeSymbol attributeSymbol = compilation.GetTypeByMetadataName("StargateNet.NetworkedAttribute");

//            List<IFieldSymbol> fieldSymbols = new List<IFieldSymbol>();
//            foreach (FieldDeclarationSyntax field in receiver.CandidateFields)
//            {
//                SemanticModel model = compilation.GetSemanticModel(field.SyntaxTree);
//                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
//                {
//                    IFieldSymbol fieldSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
//                    if (fieldSymbol.GetAttributes().Any(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
//                    {
//                        fieldSymbols.Add(fieldSymbol);
//                    }
//                }
//            }

//            var allFieldsInfo = new StringBuilder();
//            allFieldsInfo.AppendLine(@"
//using System;
//using System.Collections.Generic;

//namespace StargateNet
//{
//    public static class NetworkedFieldInfoRegistry
//    {
//        public static readonly List<NetworkedFieldInfo> FieldInfos = new List<NetworkedFieldInfo>();

//        static NetworkedFieldInfoRegistry()
//        {");

//            foreach (var field in fieldSymbols)
//            {
//                allFieldsInfo.AppendLine($@"
//            FieldInfos.Add(new NetworkedFieldInfo
//            {{
//                ClassName = ""{field.ContainingType.ToDisplayString()}"",
//                FieldName = ""{field.Name}"",
//                FieldType = typeof({field.Type.ToDisplayString()}),
//                Offset = (IntPtr)System.Runtime.CompilerServices.Unsafe.OffsetOf<{field.ContainingType.ToDisplayString()}>(nameof({field.ContainingType.ToDisplayString()}.{field.Name}))
//            }});");
//            }

//            allFieldsInfo.AppendLine(@"
//        }
//    }

//    public struct NetworkedFieldInfo
//    {
//        public string ClassName;
//        public string FieldName;
//        public Type FieldType;
//        public IntPtr Offset;
//    }
//}");

//            context.AddSource("NetworkedFieldInfoRegistry.cs", SourceText.From(allFieldsInfo.ToString(), Encoding.UTF8));

//            foreach (IGrouping<INamedTypeSymbol, IFieldSymbol> group in fieldSymbols.GroupBy(f => f.ContainingType))
//            {
//                string classSource = ProcessClass(group.Key, group.ToList(), attributeSymbol);
//                context.AddSource($"{group.Key.Name}_NetworkedFields.cs", SourceText.From(classSource, Encoding.UTF8));
//            }
//        }

//        public void Initialize(GeneratorInitializationContext context)
//        {
//            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
//        }

//        private string ProcessClass(INamedTypeSymbol classSymbol, List<IFieldSymbol> fields, INamedTypeSymbol attributeSymbol)
//        {
//            StringBuilder source = new StringBuilder($@"
//using System;

//namespace {classSymbol.ContainingNamespace}
//{{
//    public partial class {classSymbol.Name}
//    {{
//        private struct NetworkedBitmap
//        {{
//            public ulong Bitmap;
//        }}

//        private NetworkedBitmap _networkedBitmap = new NetworkedBitmap();

//");

//            foreach (var field in fields)
//            {
//                string fieldType = field.Type.ToString();
//                string fieldName = field.Name;
//                int fieldIndex = fields.IndexOf(field);

//                source.Append($@"
//        private {fieldType} _{fieldName};
//        public {fieldType} {fieldName}
//        {{
//            get;
//            set;
//        }}
//");
//            }

//            source.Append(@"
//    }
//}
//");
//            return source.ToString();
//        }

//        public class SyntaxReceiver : ISyntaxReceiver
//        {
//            public List<FieldDeclarationSyntax> CandidateFields { get; } = new List<FieldDeclarationSyntax>();

//            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
//            {
//                if (syntaxNode is FieldDeclarationSyntax fieldDeclarationSyntax
//                    && fieldDeclarationSyntax.AttributeLists.Count > 0)
//                {
//                    CandidateFields.Add(fieldDeclarationSyntax);
//                }
//            }
//        }
//    }
//}