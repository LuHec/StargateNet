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
//                throw new Exception($"Wrong SyntaxReciver!{context.SyntaxReceiver}");
//            }

//            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
//            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(this.networkedAttributeText, Encoding.UTF8)));

//            // Networked标记                        
//            INamedTypeSymbol attributeSymbol = compilation.GetTypeByMetadataName("StargateNet.NetworkedAttribute");

//            List<IFieldSymbol> fieldSymbols = new List<IFieldSymbol>();
//            foreach (FieldDeclarationSyntax field in receiver.CandidateFields)
//            {
//                SemanticModel model = compilation.GetSemanticModel(field.SyntaxTree);
//                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
//                {
//                    // 获取字段符号信息，如果有 Networked 标注则保存
//                    IFieldSymbol fieldSymbol = model.GetDeclaredSymbol(variable) as IFieldSymbol;
//                    if (fieldSymbol.GetAttributes().Any(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
//                    {
//                        fieldSymbols.Add(fieldSymbol);
//                    }
//                }
//            }

//            foreach (IGrouping<INamedTypeSymbol, IFieldSymbol> group in fieldSymbols.GroupBy(f => f.ContainingType))
//            {

//            }
//        }

//        public void Initialize(GeneratorInitializationContext context)
//        {
//            throw new NotImplementedException();
//        }

//        private string ProcessClass(INamedTypeSymbol classSymbol, List<IFieldSymbol> fields, ISymbol attributeSymbol, ISymbol notifySymbol, GeneratorExecutionContext context)
//        {
            
//        }

//        public void ProcessField(StringBuilder source, IFieldSymbol fieldSymbol, ISymbol attributeSymbol)
//        {
//            string fieldName = fieldSymbol.Name;
//            ITypeSymbol fieldType = fieldSymbol.Type;

//            AttributeData attributeData = fieldSymbol.GetAttributes().Single(ad => ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));
//            TypedConstant overridenNameOpt = attributeData.NamedArguments.SingleOrDefault(kvp => kvp.Key == "PropertyName").Value;
//        }
//    }

//    public class SyntaxReceiver : ISyntaxReceiver
//    {
//        public List<FieldDeclarationSyntax> CandidateFields { get; } = new List<FieldDeclarationSyntax>();

//        // 编译中在访问每个语法节点时被调用，我们可以检查节点并保存任何对生成有用的信息
//        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
//        {
//            // 将具有至少一个 Attribute 的任何字段作为候选
//            if (syntaxNode is FieldDeclarationSyntax fieldDeclarationSyntax
//                && fieldDeclarationSyntax.AttributeLists.Count > 0)
//            {
//                CandidateFields.Add(fieldDeclarationSyntax);
//            }
//        }
//    }

//    public unsafe struct NetworkedFieldInfo
//    {
//        // TODO:
//        // 类信息
//        // 字段偏移量
//        // 类型
//        public string className;
//        public long offest;

//    }
//}

//// Armor.Hp   类Armor，字段HP
//// 二进制字节流，一个包包含所有的snapshot。大的snapshot分小snapshot。ClientId->bitmap
//// 客户端也有内存排布信息，每个字段的信息都会被存储(包含类信息，字段偏移量)。
//// 这样反序列化时，客户端只需要查看bitmap，就能用idx找到对应的字段并且去修改。（即同步属性是不分脚本的）
//// 传输过来的snapshot包含所有的同步物体，因此每个bitmap首部都应该是一个entity id。
//// 首部：Tick，同步属性的bitmap
//// 跟随信息：按顺序排布的

//// 需要获取的信息
//// 总共字段的大小
//// 每个字段的偏移量和所在类
//// 生成每个字段的getter和setter，改变字段后自动将bitmap置为1
