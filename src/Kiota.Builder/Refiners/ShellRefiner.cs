﻿using System;
using System.Collections.Generic;
using System.Linq;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Refiners {
    public class ShellRefiner : CommonLanguageRefiner, ILanguageRefiner
    {
        public ShellRefiner(GenerationConfiguration configuration) : base(configuration) {}
        public override void Refine(CodeNamespace generatedCode)
        {
            // Remove PathSegment field
            // Convert Properties to AddCommand
            

            AddDefaultImports(generatedCode);
            MoveClassesWithNamespaceNamesUnderNamespace(generatedCode);
            ConvertUnionTypesToWrapper(generatedCode);
            AddPropertiesAndMethodTypesImports(generatedCode, false, false, false);
            RemoveModelClasses(generatedCode);
            RemoveEnums(generatedCode);
            RemoveConstructors(generatedCode);
            TurnRequestBuildersIntoCommandBuilders(generatedCode);
            AddAsyncSuffix(generatedCode);
            AddInnerClasses(generatedCode, false);
            CapitalizeNamespacesFirstLetters(generatedCode);
            ReplaceBinaryByNativeType(generatedCode, "Stream", "System.IO");
            MakeEnumPropertiesNullable(generatedCode);
            ReplaceReservedNames(generatedCode, new CSharpReservedNamesProvider(), x => $"@{x.ToFirstCharacterUpperCase()}");
            DisambiguatePropertiesWithClassNames(generatedCode);
            AddConstructorsForDefaultValues(generatedCode, false);
            AddSerializationModulesImport(generatedCode);
        }
        private static void DisambiguatePropertiesWithClassNames(CodeElement currentElement) {
            if(currentElement is CodeClass currentClass) {
                var sameNameProperty = currentClass
                                                .GetChildElements(true)
                                                .OfType<CodeProperty>()
                                                .FirstOrDefault(x => x.Name.Equals(currentClass.Name, StringComparison.OrdinalIgnoreCase));
                if(sameNameProperty != null) {
                    currentClass.RemoveChildElement(sameNameProperty);
                    sameNameProperty.SerializationName ??= sameNameProperty.Name;
                    sameNameProperty.Name = $"{sameNameProperty.Name}_prop";
                    currentClass.AddProperty(sameNameProperty);
                }
            }
            CrawlTree(currentElement, DisambiguatePropertiesWithClassNames);
        }
        private static void MakeEnumPropertiesNullable(CodeElement currentElement) {
            if(currentElement is CodeClass currentClass && currentClass.IsOfKind(CodeClassKind.Model))
                currentClass.GetChildElements(true)
                            .OfType<CodeProperty>()
                            .Where(x => x.Type is CodeType propType && propType.TypeDefinition is CodeEnum)
                            .ToList()
                            .ForEach(x => x.Type.IsNullable = true);
            CrawlTree(currentElement, MakeEnumPropertiesNullable);
        }
        private static void RemoveModelClasses(CodeElement currentElement) {
            if(currentElement is CodeClass currentClass && currentClass.IsOfKind(CodeClassKind.Model)) {
                var codeNamespace = currentClass.Parent as CodeNamespace;
                codeNamespace.RemoveChildElement(currentClass);
            }
            CrawlTree(currentElement, RemoveModelClasses);
        }

        private static void RemoveEnums(CodeElement currentElement)
        {
            if (currentElement is CodeEnum currentEnum)
            {
                var codeNamespace = currentElement.Parent as CodeNamespace;
                codeNamespace.RemoveChildElement(currentEnum);
            }
            CrawlTree(currentElement, RemoveEnums);
        }

        private static void RemoveConstructors(CodeElement currentElement)
        {
            if (currentElement is CodeMethod currentMethod && currentMethod.IsOfKind(CodeMethodKind.Constructor))
            {
                var codeClass = currentElement.Parent as CodeClass;
                codeClass.RemoveChildElement(currentMethod);
            }
            CrawlTree(currentElement, RemoveConstructors);
        }

        private static void TurnRequestBuildersIntoCommandBuilders(CodeElement currentElement)
        {
            if (currentElement is CodeClass currentClass && currentClass.IsOfKind(CodeClassKind.RequestBuilder))
            {

                (currentClass.StartBlock as CodeClass.Declaration).IsStatic = true;

                // Replace Nav Properties with BuildXXXCommand methods
                var navProperties = currentClass.GetChildElements().Where(e => e is CodeProperty prop && prop.IsOfKind(CodePropertyKind.RequestBuilder)).Cast<CodeProperty>();
                foreach (var navProp in navProperties)
                {
                    var method = CreateBuildCommandMethod(navProp, currentClass);
                    currentClass.AddMethod(method);
                    currentClass.RemoveChildElement(navProp);
                }
                // Change signtature of RequestExecutors
                var requestMethods = currentClass.GetChildElements().Where(e => e is CodeMethod method && method.IsOfKind(CodeMethodKind.RequestExecutor)).Cast<CodeMethod>();
                foreach (var requestMethod in requestMethods)
                {
                    requestMethod.IsAsync = false;
                    requestMethod.IsStatic = true;
                    requestMethod.Name = $"Build{requestMethod.Name}Command";
                    requestMethod.ReturnType = CreateCommandType(requestMethod);
                }

                var buildMethod = new CodeMethod(currentClass)
                {
                    Name = "Build",
                    IsStatic = true,
                    IsAsync = false
                };
                buildMethod.AddParameter(new CodeParameter(buildMethod) { Name = "httpCore", Type = new CodeType(buildMethod) { Name = "IHttpCore", IsExternal = true } });
                // Add calls to BuildMethods here..
                buildMethod.ReturnType = new CodeType(buildMethod)
                {
                    Name = "Command",
                    IsExternal = true
                };
                currentClass.AddMethod(buildMethod);

            }
            CrawlTree(currentElement, TurnRequestBuildersIntoCommandBuilders);
        }

        private static CodeType CreateCommandType(CodeElement parent)
        {
            return new CodeType(parent)
            {
                Name = "Command",
                IsExternal = true
            };
        }

        private static CodeMethod CreateBuildCommandMethod(CodeProperty navProperty, CodeClass parent)
        {
            var codeMethod = new CodeMethod(parent);
            codeMethod.IsAsync = false;
            codeMethod.IsStatic = true;
            codeMethod.Name = $"Build{navProperty.Name}Command";
            codeMethod.MethodKind = CodeMethodKind.RequestBuilderBackwardCompatibility;
            codeMethod.ReturnType = CreateCommandType(codeMethod);
            return codeMethod;
        }

        private static readonly string[] defaultNamespacesForClasses = new string[] {"System", "System.Collections.Generic", "System.Linq"};
        private static readonly string[] defaultNamespacesForRequestBuilders = new string[] { "System.Threading.Tasks", "System.IO", "Microsoft.Kiota.Abstractions", "Microsoft.Kiota.Abstractions.Serialization", "System.CommandLine", "System.CommandLine.Invocation" };

        private static void AddDefaultImports(CodeElement current) {
            if(current is CodeClass currentClass) {
                currentClass.AddUsing(defaultNamespacesForClasses.Select(x => new CodeUsing(currentClass) { Name = x }).ToArray());
                if(currentClass.IsOfKind(CodeClassKind.RequestBuilder))
                    currentClass.AddUsing(defaultNamespacesForRequestBuilders.Select(x => new CodeUsing(currentClass) { Name = x }).ToArray());
            }
            CrawlTree(current, AddDefaultImports);
        }
        private static void CapitalizeNamespacesFirstLetters(CodeElement current) {
            if(current is CodeNamespace currentNamespace)
                currentNamespace.Name = currentNamespace.Name?.Split('.')?.Select(x => x.ToFirstCharacterUpperCase())?.Aggregate((x, y) => $"{x}.{y}");
            CrawlTree(current, CapitalizeNamespacesFirstLetters);
        }
        private static void AddAsyncSuffix(CodeElement currentElement) {
            if(currentElement is CodeMethod currentMethod && currentMethod.IsAsync)
                currentMethod.Name += "Async";
            CrawlTree(currentElement, AddAsyncSuffix);
        }
    }
}
