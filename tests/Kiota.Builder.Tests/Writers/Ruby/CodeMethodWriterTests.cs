using System;
using System.IO;
using System.Linq;
using Kiota.Builder.Extensions;
using Kiota.Builder.Tests;
using Xunit;

namespace Kiota.Builder.Writers.Ruby.Tests {
    public class CodeMethodWriterTests : IDisposable {
        private const string DefaultPath = "./";
        private const string DefaultName = "name";
        private readonly StringWriter tw;
        private readonly LanguageWriter writer;
        private readonly CodeMethod method;
        private readonly CodeMethod voidMethod;
        private readonly CodeClass parentClass;
        private const string MethodName = "methodName";
        private const string ReturnTypeName = "Somecustomtype";
        private const string MethodDescription = "some description";
        private const string ParamDescription = "some parameter description";
        private const string ParamName = "paramName";
        public CodeMethodWriterTests()
        {
            writer = LanguageWriter.GetLanguageWriter(GenerationLanguage.Ruby, DefaultPath, DefaultName);
            tw = new StringWriter();
            writer.SetTextWriter(tw);
            var root = CodeNamespace.InitRootNamespace();
            parentClass = new CodeClass(root) {
                Name = "parentClass"
            };
            root.AddClass(parentClass);
            method = new CodeMethod(parentClass) {
                Name = MethodName,
            };
            method.ReturnType = new CodeType(method) {
                Name = ReturnTypeName
            };
            voidMethod = new CodeMethod(parentClass) {
                Name = MethodName,
            };
            voidMethod.ReturnType = new CodeType(voidMethod) {
                Name = "void"
            };
            parentClass.AddMethod(voidMethod);
            parentClass.AddMethod(method);
        }
        public void Dispose()
        {
            tw?.Dispose();
            GC.SuppressFinalize(this);
        }
        private void AddSerializationProperties() {
            var addData = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "additionalData",
                PropertyKind = CodePropertyKind.AdditionalData,
            }).First();
            addData.Type = new CodeType(addData) {
                Name = "string"
            };
            var dummyProp = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "dummyProp",
            }).First();
            dummyProp.Type = new CodeType(dummyProp) {
                Name = "string"
            };
            var dummyCollectionProp = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "dummyColl",
            }).First();
            dummyCollectionProp.Type = new CodeType(dummyCollectionProp) {
                Name = "string",
                CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
            };
            var dummyComplexCollection = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "dummyComplexColl"
            }).First();
            dummyComplexCollection.Type = new CodeType(dummyComplexCollection) {
                Name = "Complex",
                CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
                TypeDefinition = new CodeClass(parentClass.Parent) {
                    Name = "SomeComplexType"
                }
            };
            var dummyEnumProp = parentClass.AddProperty(new CodeProperty(parentClass){
                Name = "dummyEnumCollection",
            }).First();
            dummyEnumProp.Type = new CodeType(dummyEnumProp) {
                Name = "SomeEnum",
                TypeDefinition = new CodeEnum(parentClass.Parent) {
                    Name = "EnumType"
                }
            };
        }
        private void AddInheritanceClass() {
            (parentClass.StartBlock as CodeClass.Declaration).Inherits = new CodeType(parentClass) {
                Name = "someParentClass"
            };
        }
        private void AddRequestBodyParameters() {
            var stringType = new CodeType(method) {
                Name = "string",
            };
            method.AddParameter(new CodeParameter(method) {
                Name = "h",
                ParameterKind = CodeParameterKind.Headers,
                Type = stringType,
            });
            method.AddParameter(new CodeParameter(method){
                Name = "q",
                ParameterKind = CodeParameterKind.QueryParameter,
                Type = stringType,
            });
            method.AddParameter(new CodeParameter(method){
                Name = "b",
                ParameterKind = CodeParameterKind.RequestBody,
                Type = stringType,
            });
            method.AddParameter(new CodeParameter(method){
                Name = "r",
                ParameterKind = CodeParameterKind.ResponseHandler,
                Type = stringType,
            });
        }
        private void AddVoidRequestBodyParameters() {
            var stringType = new CodeType(voidMethod) {
                Name = "string",
            };
            voidMethod.AddParameter(new CodeParameter(voidMethod) {
                Name = "h",
                ParameterKind = CodeParameterKind.Headers,
                Type = stringType,
            });
            voidMethod.AddParameter(new CodeParameter(voidMethod){
                Name = "q",
                ParameterKind = CodeParameterKind.QueryParameter,
                Type = stringType,
            });
            voidMethod.AddParameter(new CodeParameter(voidMethod){
                Name = "b",
                ParameterKind = CodeParameterKind.RequestBody,
                Type = stringType,
            });
            voidMethod.AddParameter(new CodeParameter(voidMethod){
                Name = "r",
                ParameterKind = CodeParameterKind.ResponseHandler,
                Type = stringType,
            });
        }
        [Fact]
        public void WritesRequestBuilder() {
            method.MethodKind = CodeMethodKind.RequestBuilderBackwardCompatibility;
            Assert.Throws<InvalidOperationException>(() => writer.Write(method));
        }
        [Fact]
        public void WritesRequestBodiesThrowOnNullHttpMethod() {
            method.MethodKind = CodeMethodKind.RequestExecutor;
            Assert.Throws<InvalidOperationException>(() => writer.Write(method));
            method.MethodKind = CodeMethodKind.RequestGenerator;
            Assert.Throws<InvalidOperationException>(() => writer.Write(method));
        }
        [Fact]
        public void WritesRequestExecutorBody() {
            method.MethodKind = CodeMethodKind.RequestExecutor;
            method.HttpMethod = HttpMethod.Get;
            AddRequestBodyParameters();
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("request_info", result);
            Assert.Contains("send_async", result);
            AssertExtensions.CurlyBracesAreClosed(result);
        }
        [Fact]
        public void WritesRequestExecutorBodyWithNamespace() {
            voidMethod.MethodKind = CodeMethodKind.RequestExecutor;
            voidMethod.HttpMethod = HttpMethod.Get;
            AddVoidRequestBodyParameters();
            writer.Write(voidMethod);
            var result = tw.ToString();
            Assert.Contains("request_info", result);
            Assert.Contains("send_async", result);
            Assert.Contains("nil", result);
            AssertExtensions.CurlyBracesAreClosed(result);
        }
        [Fact]
        public void WritesRequestGeneratorBody() {
            method.MethodKind = CodeMethodKind.RequestGenerator;
            method.HttpMethod = HttpMethod.Get;
            AddRequestBodyParameters();
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("request_info = MicrosoftKiotaAbstractions::RequestInformation.new()", result);
            Assert.Contains("request_info.set_uri", result);
            Assert.Contains("http_method = :GET", result);
            Assert.Contains("set_query_string_parameters_from_raw_object", result);
            Assert.Contains("set_content_from_parsable", result);
            Assert.Contains("return request_info;", result);
        }
        [Fact]
        public void WritesInheritedDeSerializerBody() {
            method.MethodKind = CodeMethodKind.Deserializer;
            method.IsAsync = false;
            AddSerializationProperties();
            AddInheritanceClass();
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("super.merge({", result);
            AssertExtensions.CurlyBracesAreClosed(result);
        }
        [Fact]
        public void WritesDeSerializerBody() {
            var parameter = new CodeParameter(method){
                Description = ParamDescription,
                Name = ParamName
            };
            parameter.Type = new CodeType(parameter) {
                Name = "string"
            };
            method.MethodKind = CodeMethodKind.Deserializer;
            method.IsAsync = false;
            AddSerializationProperties();
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("get_collection_of_primitive_values", result);
            Assert.Contains("get_collection_of_object_values", result);
            Assert.Contains("get_enum_value", result);
        }
        [Fact]
        public void WritesInheritedSerializerBody() {
            method.MethodKind = CodeMethodKind.Serializer;
            method.IsAsync = false;
            AddSerializationProperties();
            AddInheritanceClass();
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("super", result);
            AssertExtensions.CurlyBracesAreClosed(result);
        }
        [Fact]
        public void WritesSerializerBody() {
            var parameter = new CodeParameter(method){
                Description = ParamDescription,
                Name = ParamName
            };
            parameter.Type = new CodeType(parameter) {
                Name = "string"
            };
            method.MethodKind = CodeMethodKind.Serializer;
            method.IsAsync = false;
            AddSerializationProperties();
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("write_collection_of_primitive_values", result);
            Assert.Contains("write_collection_of_object_values", result);
            Assert.Contains("write_enum_value", result);
            Assert.Contains("write_additional_data", result);
            AssertExtensions.CurlyBracesAreClosed(result);
        }
        [Fact]
        public void WritesTranslatedTypesDeSerializerBody() {
            var dummyCollectionProp1 = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "guidId",
            }).First();
            dummyCollectionProp1.Type = new CodeType(dummyCollectionProp1) {
                Name = "guid",
                CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
            };
            var dummyCollectionProp2 = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "dateTime",
            }).First();
            dummyCollectionProp2.Type = new CodeType(dummyCollectionProp2) {
                Name = "date",
                CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
            };
            var dummyCollectionProp3 = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "isTrue",
            }).First();
            dummyCollectionProp3.Type = new CodeType(dummyCollectionProp3) {
                Name = "boolean",
                CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
            };
            var dummyCollectionProp4 = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "numberTest",
            }).First();
            dummyCollectionProp4.Type = new CodeType(dummyCollectionProp4) {
                Name = "number",
                CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
            };
            var dummyCollectionProp5 = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "DatetimeValueType",
            }).First();
            dummyCollectionProp5.Type = new CodeType(dummyCollectionProp5) {
                Name = "dateTimeOffset",
                CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
            };
            var dummyCollectionProp6 = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "messages",
            }).First();
            dummyCollectionProp6.Type = new CodeType(dummyCollectionProp6) {
                Name = "NewObjectName",
                CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array,
            };
            method.MethodKind = CodeMethodKind.Deserializer;
            method.IsAsync = false;
            AddSerializationProperties();
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("get_collection_of_primitive_values(String)", result);
            Assert.Contains("get_collection_of_primitive_values(\"boolean\")", result);
            Assert.Contains("get_collection_of_primitive_values(Integer)", result);
            Assert.Contains("get_collection_of_primitive_values(Time)", result);
            Assert.Contains("get_collection_of_primitive_values(UUIDTools::UUID)", result);
            Assert.Contains("get_collection_of_primitive_values(NewObjectName)", result);
        }
        [Fact]
        public void WritesMethodSyncDescription() {
            
            method.Description = MethodDescription;
            method.IsAsync = false;
            var parameter = new CodeParameter(method){
                Description = ParamDescription,
                Name = ParamName
            };
            parameter.Type = new CodeType(parameter) {
                Name = "string"
            };
            method.AddParameter(parameter);
            writer.Write(method);
            var result = tw.ToString();
            Assert.DoesNotContain("@return a CompletableFuture of", result);
            AssertExtensions.CurlyBracesAreClosed(result);
        }
        [Fact]
        public void Defensive() {
            var codeMethodWriter = new CodeMethodWriter(new RubyConventionService());
            Assert.Throws<ArgumentNullException>(() => codeMethodWriter.WriteCodeElement(null, writer));
            Assert.Throws<ArgumentNullException>(() => codeMethodWriter.WriteCodeElement(method, null));
            var originalParent = method.Parent;
            method.Parent = CodeNamespace.InitRootNamespace();
            Assert.Throws<InvalidOperationException>(() => codeMethodWriter.WriteCodeElement(method, writer));
        }
        [Fact]
        public void ThrowsIfParentIsNotClass() {
            method.Parent = CodeNamespace.InitRootNamespace();
            Assert.Throws<InvalidOperationException>(() => writer.Write(method));
        }
        private const string TaskPrefix = "CompletableFuture<";
        [Fact]
        public void DoesNotAddAsyncInformationOnSyncMethods() {
            method.IsAsync = false;
            writer.Write(method);
            var result = tw.ToString();
            Assert.DoesNotContain(TaskPrefix, result);
            AssertExtensions.CurlyBracesAreClosed(result);
        }
        [Fact]
        public void WritesGetterToField() {
            method.AddAccessedProperty();
            method.MethodKind = CodeMethodKind.Getter;
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("@some_property", result);
        }
        [Fact]
        public void WritesIndexer() {
            method.MethodKind = CodeMethodKind.IndexerBackwardCompatibility;
            method.PathSegment = "somePath";
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("http_core", result);
            Assert.Contains("path_segment", result);
            Assert.Contains("+ id", result);
            Assert.Contains("return Somecustomtype.new", result);
            Assert.Contains(method.PathSegment, result);
        }
        [Fact]
        public void WritesSetterToField() {
            method.AddAccessedProperty();
            method.MethodKind = CodeMethodKind.Setter;
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains("@some_property =", result);
        }
        [Fact]
        public void WritesConstructor() {
            method.MethodKind = CodeMethodKind.Constructor;
            var defaultValue = "someval";
            var propName = "propWithDefaultValue";
            parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = propName,
                DefaultValue = defaultValue,
                PropertyKind = CodePropertyKind.PathSegment,
            });
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains($"@{propName.ToSnakeCase()} = {defaultValue}", result);
        }
        [Fact]
        public void WritesApiConstructor() {
            method.MethodKind = CodeMethodKind.ClientConstructor;
            var coreProp = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "core",
                PropertyKind = CodePropertyKind.HttpCore,
            }).First();
            coreProp.Type = new CodeType(coreProp) {
                Name = "HttpCore",
                IsExternal = true,
            };
            method.AddParameter(new CodeParameter(method) {
                Name = "core",
                ParameterKind = CodeParameterKind.HttpCore,
                Type = coreProp.Type,
            });
            writer.Write(method);
            var result = tw.ToString();
            Assert.Contains(coreProp.Name, result);
        }
        [Fact]
        public void WritesApiConstructorWithBackingStore() {
            method.MethodKind = CodeMethodKind.ClientConstructor;
            var coreProp = parentClass.AddProperty(new CodeProperty(parentClass) {
                Name = "core",
                PropertyKind = CodePropertyKind.HttpCore,
            }).First();
            coreProp.Type = new CodeType(coreProp) {
                Name = "HttpCore",
                IsExternal = true,
            };
            method.AddParameter(new CodeParameter(method) {
                Name = "core",
                ParameterKind = CodeParameterKind.HttpCore,
                Type = coreProp.Type,
            });
            var backingStoreParam = new CodeParameter(method) {
                Name = "backingStore",
                ParameterKind = CodeParameterKind.BackingStore,
            };
            backingStoreParam.Type = new CodeType(backingStoreParam) {
                Name = "BackingStore",
                IsExternal = true,
            };
            method.AddParameter(backingStoreParam);
            var tempWriter = LanguageWriter.GetLanguageWriter(GenerationLanguage.Java, DefaultPath, DefaultName);
            tempWriter.SetTextWriter(tw);
            tempWriter.Write(method);
            var result = tw.ToString();
            Assert.Contains("enableBackingStore", result);
        }
    }
}
